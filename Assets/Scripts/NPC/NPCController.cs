using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Han.CheckIn;

public enum NPCState
{
    Idle,
    Wandering,
    MovingToInteractable,
    Sitting,
    LyingDown,
    TalkingToPlayer,
    TalkingToNPC,
    /// <summary>Driven by an external director (check-in walk, walking out). No autonomous behaviour.</summary>
    Scripted,
    /// <summary>Murdered. Lies on the ground and can be inspected by the player.</summary>
    Dead
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
    private NavMeshPath cachedPath;
    private float nextObstacleCheckTime = 0f;

    public static readonly List<NPCController> ActiveNPCs = new List<NPCController>();
    private static HashSet<NPCController> chattingNPCs = new HashSet<NPCController>();

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        billboard = GetComponentInChildren<BillboardSprite>();
        speechBubble = GetComponentInChildren<SpeechBubble>();
        spawnPosition = transform.position;
        cachedPath = new NavMeshPath();

        // A kinematic Rigidbody prevents NavMeshAgent from updating transform.position
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            Destroy(rb);
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void OnEnable()
    {
        if (!ActiveNPCs.Contains(this))
        {
            ActiveNPCs.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveNPCs.Remove(this);
        chattingNPCs.Remove(this);
    }

    // ------------------------------------------------------------------
    //  Night-loop API (used by HanGameDirector)
    // ------------------------------------------------------------------
    public static event System.Action<NPCController> OnBodyInspected;

    public GuestData Guest { get; private set; }
    public SpeechBubble Bubble => speechBubble;
    public NavMeshAgent Agent => agent;
    public bool IsDead => CurrentState == NPCState.Dead;
    public bool IsFreeToAct => CurrentState == NPCState.Wandering || CurrentState == NPCState.Idle;
    public bool CanBeKilled => CurrentState == NPCState.Wandering || CurrentState == NPCState.Idle ||
                               CurrentState == NPCState.Sitting || CurrentState == NPCState.LyingDown;

    /// <summary>Binds a generated guest to this NPC and dresses the billboard with its body sprite. Call before the NPC is activated.</summary>
    public void AssignGuest(GuestData guest, bool startScripted)
    {
        Guest = guest;
        if (guest != null) guest.npc = this;
        if (startScripted) CurrentState = NPCState.Scripted;

        if (guest != null && guest.bodySprite != null)
        {
            var sr = GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null)
            {
                const float figureHeight = 1.75f;
                sr.sprite = guest.bodySprite;
                float parentScale = sr.transform.parent != null ? Mathf.Max(0.01f, sr.transform.parent.lossyScale.y) : 1f;
                sr.transform.localScale = Vector3.one * (figureHeight / 2f / parentScale);
                sr.transform.localPosition = new Vector3(0f, (figureHeight * 0.5f + 0.02f) / parentScale, 0f);
                sr.color = Color.white;
            }
        }
    }

    private Vector3 pendingDestination;
    private bool hasPendingDestination;

    public void SetHome(Vector3 position)
    {
        spawnPosition = position;
    }

    /// <summary>Walks to a world position. If hold is true the NPC stays under external control until ReleaseScripted().</summary>
    public void DirectTo(Vector3 position, bool hold)
    {
        if (IsDead) return;
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            position = hit.position;

        pendingDestination = position;
        hasPendingDestination = true;

        if (agent != null && agent.enabled)
        {
            if (!agent.isOnNavMesh)
            {
                if (NavMesh.SamplePosition(transform.position, out NavMeshHit selfHit, 5f, NavMesh.AllAreas))
                    agent.Warp(selfHit.position);
            }

            if (agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(position);
            }
        }
        CurrentState = hold ? NPCState.Scripted : NPCState.Wandering;
        stuckTimer = 0f;
    }

    public void ReleaseScripted()
    {
        hasPendingDestination = false;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        if (CurrentState == NPCState.Scripted) EnterWandering();
    }

    public void Say(string line, float duration = 3.5f)
    {
        if (speechBubble != null && !string.IsNullOrEmpty(line))
            speechBubble.ShowMessage(Format(line), duration);
    }

    public void Die()
    {
        if (IsDead) return;
        if (currentSmartObject != null)
        {
            currentSmartObject.Release(gameObject);
            currentSmartObject = null;
        }
        chattingNPCs.Remove(this);
        CurrentState = NPCState.Dead;
        hasPendingDestination = false;
        if (speechBubble != null) speechBubble.Hide();
        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
        }
        if (billboard != null) billboard.SetLyingMode(true);

        // Replace standing capsule collider with flat horizontal body collider on the floor
        var cc = GetComponent<CapsuleCollider>();
        if (cc != null) cc.enabled = false;
        var bc = GetComponent<BoxCollider>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider>();
        bc.enabled = true;
        bc.center = new Vector3(0f, 0.2f, 0f);
        bc.size = new Vector3(1.2f, 0.4f, 1.8f);
    }

    private string Format(string line)
    {
        if (string.IsNullOrEmpty(line) || line.IndexOf('{') < 0) return line;
        string victim = string.IsNullOrEmpty(HanGameDirector.VictimName) ? "the poor soul" : HanGameDirector.VictimName;
        string suspect = HanGameDirector.PickSuspectName(Guest != null ? Guest.guestName : null);
        return line.Replace("{victim}", victim).Replace("{suspect}", suspect);
    }

    private IEnumerator Start()
    {
        // Wait one frame to ensure NavMesh is fully loaded and agent is initialized
        yield return null;

        // Snap to NavMesh to ensure agent initializes correctly
        if (agent != null && agent.enabled && NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
        }

        if (hasPendingDestination && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.SetDestination(pendingDestination);
        }
        else if (CurrentState != NPCState.Scripted && CurrentState != NPCState.Dead)
        {
            EnterWandering();
        }
    }

    private void Update()
    {
        if (CurrentState == NPCState.Dead) return;

        if (agent != null && agent.enabled && !agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                if (hasPendingDestination && agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(pendingDestination);
                }
            }
            else
            {
                // Wait until placed on a valid NavMesh
                return;
            }
        }

        if (CurrentState == NPCState.Scripted) return;

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
            if (agent.isOnNavMesh) agent.isStopped = false;
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
        if (Time.time < nextObstacleCheckTime) return;
        nextObstacleCheckTime = Time.time + 0.1f;

        Vector3 moveDir = agent.velocity;
        if (moveDir.sqrMagnitude < 0.05f) return;
        moveDir.y = 0f;
        moveDir.Normalize();

        Vector3 sphereOrigin = transform.position + Vector3.up * 0.8f;
        float checkDist = agent.radius + 0.35f;

        if (Physics.SphereCast(sphereOrigin, 0.25f, moveDir, out RaycastHit hit, checkDist, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null)
            {
                string cName = hit.collider.name;
                if (cName.IndexOf("platform", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    cName.IndexOf("floor", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    !cName.Equals("terrain", System.StringComparison.OrdinalIgnoreCase) &&
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

        // Open closed door along path to room/bed
        CheckDoorAlongPath();

        bool arrived = !agent.pathPending && (agent.remainingDistance <= agent.stoppingDistance || 
            (agent.hasPath && agent.remainingDistance <= 1.2f && agent.velocity.sqrMagnitude < 0.01f));

        if (arrived)
        {
            StartUsingSmartObject();
        }
    }

    private void CheckDoorAlongPath()
    {
        if (Time.time < nextObstacleCheckTime) return;
        var door = FindClosestRoomDoor(2.5f);
        if (door != null && !door.IsOpen && !door.IsMoving)
        {
            door.OpenDoor();
        }
    }

    private EasyDoorSystem.EasyDoor FindClosestRoomDoor(float maxDist = 8f)
    {
        var doors = FindObjectsByType<EasyDoorSystem.EasyDoor>(FindObjectsSortMode.None);
        EasyDoorSystem.EasyDoor best = null;
        float bestSq = maxDist * maxDist;
        for (int i = 0; i < doors.Length; i++)
        {
            var d = doors[i];
            if (d == null || d.name.IndexOf("chest", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
            float sq = (d.transform.position - transform.position).sqrMagnitude;
            if (sq < bestSq)
            {
                bestSq = sq;
                best = d;
            }
        }
        return best;
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

            // Close the room door behind them when going to sleep
            var door = FindClosestRoomDoor(7.5f);
            if (door != null && door.IsOpen && !door.IsMoving)
            {
                door.CloseDoor();
            }
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
            bool murder = HanGameDirector.MurderDiscovered;
            string msg = Format(CurrentState == NPCState.Sitting ? dialogueData.GetSittingThought(murder) : dialogueData.GetSleepyMurmur(murder));
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
            // Open the door before leaving the bedroom
            if (currentSmartObject.ObjectType == SmartObjectType.Bed)
            {
                var door = FindClosestRoomDoor(7.5f);
                if (door != null && !door.IsOpen && !door.IsMoving)
                {
                    door.OpenDoor();
                }
            }

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
        if (CurrentState == NPCState.Dead)
        {
            OnBodyInspected?.Invoke(this);
            return;
        }
        if (CurrentState == NPCState.Scripted) return;
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
            string msg = Format(dialogueData.GetGreeting(HanGameDirector.MurderDiscovered));
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

        float chatRangeSqr = npcChatRange * npcChatRange;
        for (int i = 0; i < ActiveNPCs.Count; i++)
        {
            var otherNPC = ActiveNPCs[i];
            if (otherNPC == null || otherNPC == this) continue;
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
            convo = dialogueData.GetConversation(HanGameDirector.MurderDiscovered);
        }

        string lineA = convo != null ? Format(convo.lineA) : "Hello!";
        string lineB = convo != null ? otherNPC.Format(convo.lineB) : "Hi there!";

        speechBubble.ShowTypewriter(lineA, 30f);
        yield return new WaitForSeconds(speechBubbleDuration + (lineA.Length / 30f));
        speechBubble.Hide();

        if (otherNPC != null)
        {
            otherNPC.speechBubble.ShowTypewriter(lineB, 30f);
            yield return new WaitForSeconds(speechBubbleDuration + (lineB.Length / 30f));
            if (otherNPC != null) otherNPC.speechBubble.Hide();
        }

        lastChatTime = Time.time;
        if (otherNPC != null) otherNPC.lastChatTime = Time.time;

        chattingNPCs.Remove(this);
        chattingNPCs.Remove(otherNPC);

        if (CurrentState == NPCState.TalkingToNPC) EnterWandering();
        if (otherNPC != null && otherNPC.CurrentState == NPCState.TalkingToNPC) otherNPC.EnterWandering();
    }

    private bool RandomNavMeshPoint(Vector3 center, float range, out Vector3 result)
    {
        if (cachedPath == null) cachedPath = new NavMeshPath();
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
                    if (NavMesh.CalculatePath(transform.position, hit.position, NavMesh.AllAreas, cachedPath))
                    {
                        if (cachedPath.status == NavMeshPathStatus.PathComplete)
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
