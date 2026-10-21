using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum NPCState
{
    Idle,
    Wandering,
    MovingToInteractable,
    Sitting,
    LyingDown,
    TalkingToPlayer,
    TalkingToNPC
}

[RequireComponent(typeof(NavMeshAgent))]
public class NPCController : MonoBehaviour
{
    [SerializeField] private DialogueData dialogueData;
    [SerializeField] private float wanderRadius = 15f;
    [SerializeField] private float npcChatRange = 4f;
    [SerializeField] private float npcChatCooldown = 30f;
    [SerializeField] private float idlePauseMin = 2f;
    [SerializeField] private float idlePauseMax = 6f;
    [SerializeField] private float smartObjectSearchInterval = 10f;
    [SerializeField] private float smartObjectChance = 0.3f;
    [SerializeField] private float speechBubbleDuration = 3f;

    public NPCState CurrentState { get; private set; } = NPCState.Idle;

    private NavMeshAgent agent;
    private BillboardSprite billboard;
    private SpeechBubble speechBubble;
    
    private Vector3 spawnPosition;
    private float stateTimer = 0f;
    private float searchTimer = 0f;
    private float lastChatTime = -9999f;
    private float speechTimer = 0f;
    private float stuckTimer = 0f;
    
    private SmartObject currentSmartObject;
    private Transform playerTransform;

    private static HashSet<NPCController> chattingNPCs = new HashSet<NPCController>();

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        billboard = GetComponentInChildren<BillboardSprite>();
        speechBubble = GetComponentInChildren<SpeechBubble>();
        spawnPosition = transform.position;
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private IEnumerator Start()
    {
        // Wait one frame to ensure NavMesh is fully loaded and agent is initialized
        yield return null;

        // Snap to NavMesh to ensure agent initializes correctly
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }

        EnterWandering();
    }

    private void Update()
    {
        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                // Wait until placed on a valid NavMesh
                return;
            }
        }

        switch (CurrentState)
        {
            case NPCState.Idle:
                UpdateIdle();
                break;
            case NPCState.Wandering:
                UpdateWandering();
                break;
            case NPCState.MovingToInteractable:
                UpdateMovingToInteractable();
                break;
            case NPCState.Sitting:
            case NPCState.LyingDown:
                UpdateSittingOrLying();
                break;
            case NPCState.TalkingToPlayer:
                UpdateTalkingToPlayer();
                break;
            case NPCState.TalkingToNPC:
                // Handled in coroutine
                break;
        }

        CheckForNPCChat();
    }

    private void EnterIdle()
    {
        CurrentState = NPCState.Idle;
        stateTimer = Random.Range(idlePauseMin, idlePauseMax);
        if (agent.enabled)
        {
            agent.ResetPath();
        }
    }

    private void UpdateIdle()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            searchTimer += (idlePauseMax + idlePauseMin) / 2f;
            if (searchTimer >= smartObjectSearchInterval)
            {
                searchTimer = 0f;
                if (Random.value <= smartObjectChance)
                {
                    if (TryFindAndMoveToSmartObject())
                    {
                        return;
                    }
                }
            }
            EnterWandering();
        }
    }

    private void EnterWandering()
    {
        CurrentState = NPCState.Wandering;
        if (!agent.enabled || !agent.isOnNavMesh)
        {
            EnterIdle();
            return;
        }

        Vector3 randomPoint;
        if (RandomNavMeshPoint(spawnPosition, wanderRadius, out randomPoint))
        {
            agent.SetDestination(randomPoint);
        }
        else
        {
            EnterIdle();
        }
    }

    private void UpdateWandering()
    {
        // Check for forward obstacle to ensure NPC never penetrates any gameobject collider
        CheckForwardObstacle();

        // Check if agent is stuck trying to push into an obstacle
        if (agent.hasPath && !agent.pathPending && agent.velocity.sqrMagnitude < 0.05f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > 1.2f)
            {
                stuckTimer = 0f;
                if (agent.enabled) agent.ResetPath();
                EnterIdle();
                return;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!agent.hasPath || agent.velocity.sqrMagnitude == 0f)
            {
                EnterIdle();
            }
        }
    }

    private void CheckForwardObstacle()
    {
        if (!agent.enabled || !agent.hasPath) return;

        Vector3 moveDir = agent.velocity;
        if (moveDir.sqrMagnitude < 0.05f) return;
        moveDir.y = 0f;
        moveDir.Normalize();

        Vector3 sphereOrigin = transform.position + Vector3.up * 0.8f;
        float checkDist = agent.radius + 0.35f;

        if (Physics.SphereCast(sphereOrigin, 0.25f, moveDir, out RaycastHit hit, checkDist))
        {
            if (hit.collider != null && !hit.collider.isTrigger)
            {
                string cName = hit.collider.name.ToLower();
                if (!cName.Contains("platform") && !cName.Contains("floor") && cName != "terrain" &&
                    !hit.collider.transform.IsChildOf(transform) && !hit.collider.CompareTag("Player"))
                {
                    // Solid gameobject in immediate path: stop immediately to never go past or clip into it
                    if (agent.enabled) agent.ResetPath();
                    EnterIdle();
                }
            }
        }
    }

    private bool TryFindAndMoveToSmartObject()
    {
        // Try to find any available smart object nearby (check all types)
        SmartObject obj = SmartObject.FindNearest(transform.position, SmartObjectType.Chair, wanderRadius);
        if (obj == null) obj = SmartObject.FindNearest(transform.position, SmartObjectType.Bench, wanderRadius);
        if (obj == null) obj = SmartObject.FindNearest(transform.position, SmartObjectType.Bed, wanderRadius);

        if (obj != null && obj.TryReserve(gameObject))
        {
            currentSmartObject = obj;
            CurrentState = NPCState.MovingToInteractable;
            if (agent.enabled)
            {
                Vector3 dest = obj.InteractionAnchor.position;
                if (NavMesh.SamplePosition(dest, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                {
                    dest = hit.position;
                }
                agent.SetDestination(dest);
            }
            return true;
        }
        return false;
    }

    private void UpdateMovingToInteractable()
    {
        if (currentSmartObject == null)
        {
            EnterWandering();
            return;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        bool arrived = !agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance || 
            (agent.hasPath && agent.remainingDistance <= 1.2f && agent.velocity.sqrMagnitude < 0.01f));

        if (arrived)
        {
            StartUsingSmartObject();
        }
    }

    private void StartUsingSmartObject()
    {
        agent.enabled = false;
        transform.position = currentSmartObject.InteractionAnchor.position;
        transform.rotation = currentSmartObject.InteractionAnchor.rotation;
        
        stateTimer = currentSmartObject.GetRandomUseDuration();
        speechTimer = Random.Range(3f, 8f);

        if (currentSmartObject.ObjectType == SmartObjectType.Chair || currentSmartObject.ObjectType == SmartObjectType.Bench)
        {
            CurrentState = NPCState.Sitting;
            billboard.SetSitMode(true, -0.3f);
        }
        else if (currentSmartObject.ObjectType == SmartObjectType.Bed)
        {
            CurrentState = NPCState.LyingDown;
            billboard.SetLyingMode(true);
        }
        else
        {
            CurrentState = NPCState.Sitting;
            billboard.SetSitMode(true, -0.3f);
        }
    }

    private void UpdateSittingOrLying()
    {
        stateTimer -= Time.deltaTime;
        speechTimer -= Time.deltaTime;

        if (speechTimer <= 0f && dialogueData != null)
        {
            speechTimer = Random.Range(10f, 20f);
            string msg = CurrentState == NPCState.Sitting ? dialogueData.GetRandom(dialogueData.sittingThoughts) : dialogueData.GetRandom(dialogueData.sleepyMurmurs);
            if (!string.IsNullOrEmpty(msg))
            {
                speechBubble.ShowMessage(msg, speechBubbleDuration);
            }
        }

        if (stateTimer <= 0f)
        {
            StopUsingSmartObject();
        }
    }

    private void StopUsingSmartObject()
    {
        if (currentSmartObject != null)
        {
            currentSmartObject.Release(gameObject);
            currentSmartObject = null;
        }

        billboard.SetSitMode(false, 0f);
        billboard.SetLyingMode(false);

        agent.enabled = true;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }

        EnterWandering();
    }

    public void StartPlayerConversation()
    {
        if (CurrentState == NPCState.TalkingToPlayer || CurrentState == NPCState.TalkingToNPC || !agent.enabled) return;

        CurrentState = NPCState.TalkingToPlayer;
        if (agent.enabled) agent.ResetPath();

        if (playerTransform != null)
        {
            Vector3 lookPos = playerTransform.position;
            lookPos.y = transform.position.y;
            transform.LookAt(lookPos);
        }

        if (dialogueData != null)
        {
            string msg = dialogueData.GetRandom(dialogueData.greetings);
            speechBubble.ShowMessage(msg, speechBubbleDuration);
        }
        
        stateTimer = speechBubbleDuration;
    }

    private void UpdateTalkingToPlayer()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            EnterWandering();
        }
    }

    private void CheckForNPCChat()
    {
        if (Time.time - lastChatTime < npcChatCooldown) return;
        if (CurrentState != NPCState.Wandering && CurrentState != NPCState.Idle) return;
        if (chattingNPCs.Contains(this)) return;

        NPCController[] allNPCs = FindObjectsByType<NPCController>(FindObjectsSortMode.None);
        float chatRangeSqr = npcChatRange * npcChatRange;
        foreach (var otherNPC in allNPCs)
        {
            if (otherNPC == this) continue;
            if ((otherNPC.transform.position - transform.position).sqrMagnitude > chatRangeSqr) continue;
            if (Time.time - otherNPC.lastChatTime < otherNPC.npcChatCooldown) continue;
            if (otherNPC.CurrentState != NPCState.Wandering && otherNPC.CurrentState != NPCState.Idle) continue;
            if (chattingNPCs.Contains(otherNPC)) continue;

            StartCoroutine(ConverseWithNPC(otherNPC));
            break;
        }
    }

    private IEnumerator ConverseWithNPC(NPCController otherNPC)
    {
        chattingNPCs.Add(this);
        chattingNPCs.Add(otherNPC);

        CurrentState = NPCState.TalkingToNPC;
        otherNPC.CurrentState = NPCState.TalkingToNPC;

        if (agent.enabled) agent.ResetPath();
        if (otherNPC.agent.enabled) otherNPC.agent.ResetPath();

        Vector3 lookThis = otherNPC.transform.position; lookThis.y = transform.position.y; transform.LookAt(lookThis);
        Vector3 lookOther = transform.position; lookOther.y = otherNPC.transform.position.y; otherNPC.transform.LookAt(lookOther);

        DialogueData.ConversationExchange convo = null;
        if (dialogueData != null)
        {
            convo = dialogueData.GetRandomConversation();
        }

        string lineA = convo != null ? convo.lineA : "Hello!";
        string lineB = convo != null ? convo.lineB : "Hi there!";

        speechBubble.ShowTypewriter(lineA, 30f);
        yield return new WaitForSeconds(speechBubbleDuration + (lineA.Length / 30f));
        speechBubble.Hide();

        otherNPC.speechBubble.ShowTypewriter(lineB, 30f);
        yield return new WaitForSeconds(speechBubbleDuration + (lineB.Length / 30f));
        otherNPC.speechBubble.Hide();

        lastChatTime = Time.time;
        otherNPC.lastChatTime = Time.time;

        chattingNPCs.Remove(this);
        chattingNPCs.Remove(otherNPC);

        EnterWandering();
        otherNPC.EnterWandering();
    }

    private bool RandomNavMeshPoint(Vector3 center, float range, out Vector3 result)
    {
        NavMeshPath path = new NavMeshPath();
        for (int i = 0; i < 30; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * range;
            Vector3 randomPoint = center + new Vector3(randomCircle.x, 0, randomCircle.y);
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomPoint, out hit, 1.5f, NavMesh.AllAreas))
            {
                // Ensure sampled point is not inside or immediately adjacent to any collider
                if (!Physics.CheckSphere(hit.position + Vector3.up * 0.8f, 0.35f, ~0, QueryTriggerInteraction.Ignore))
                {
                    // Verify the point is actually reachable
                    if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, path))
                    {
                        if (path.status == NavMeshPathStatus.PathComplete)
                        {
                            result = hit.position;
                            return true;
                        }
                    }
                }
            }
        }
        result = Vector3.zero;
        return false;
    }
}
