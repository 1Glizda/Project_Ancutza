using System.Collections.Generic;
using UnityEngine;
using EasyDoorSystem;

public class DoorInteractionController : MonoBehaviour
{
    [Header("Global Highlight Toggle")]
    [Tooltip("Global toggle to enable or disable door highlighting across the entire game.")]
    [SerializeField] private bool enableHighlight = true;

    [Tooltip("Optional keyboard shortcut to toggle highlight globally at runtime (e.g. KeyCode.H). Set to None to disable.")]
    [SerializeField] private KeyCode toggleHighlightKey = KeyCode.H;

    public static bool GlobalHighlightEnabled { get; set; } = true;

    [Header("Interaction Settings")]
    [Tooltip("Maximum distance to interact with and highlight doors.")]
    [SerializeField] private float interactDistance = 4.0f;

    [Tooltip("Camera used for raycasting. If null, auto-detects Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Header("Optional Key Controls")]
    [Tooltip("Allow E key interaction in addition to Left Click.")]
    [SerializeField] private bool allowKeyE = true;

    private struct CachedDoorInfo
    {
        public EasyDoor door;
        public Transform root;
        public Vector3 center;
        public bool lastOpenState;
        public bool wasMoving;
    }

    private EasyDoor currentDoor;
    private NPCController currentNPC;
    private DoorHighlighter currentHighlighter;
    private static DoorInteractionController instance;

    private readonly RaycastHit[] raycastBuffer = new RaycastHit[16];
    private readonly RaycastHit[] sphereCastBuffer = new RaycastHit[16];
    private readonly RaycastHit[] losBuffer = new RaycastHit[16];
    private readonly List<CachedDoorInfo> cachedDoors = new List<CachedDoorInfo>(32);
    private readonly Dictionary<Collider, EasyDoor> colliderToDoorCache = new Dictionary<Collider, EasyDoor>(64);

    private void Awake()
    {
        instance = this;
        GlobalHighlightEnabled = enableHighlight;
    }

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null)
            {
                targetCamera = GetComponentInChildren<Camera>();
            }
        }
        RefreshDoorCache();
    }

    public void RefreshDoorCache()
    {
        cachedDoors.Clear();
        colliderToDoorCache.Clear();
        EasyDoor[] found = FindObjectsByType<EasyDoor>(FindObjectsSortMode.None);
        for (int i = 0; i < found.Length; i++)
        {
            EasyDoor door = found[i];
            if (door == null) continue;
            Transform root = GetInteractableRoot(door);
            cachedDoors.Add(new CachedDoorInfo
            {
                door = door,
                root = root,
                center = ComputeInteractableCenter(root),
                lastOpenState = door.IsOpen,
                wasMoving = door.IsMoving
            });
        }
    }

    private static void SortHitsByDistance(RaycastHit[] buffer, int count)
    {
        for (int i = 1; i < count; i++)
        {
            RaycastHit key = buffer[i];
            float keyDist = key.distance;
            int j = i - 1;
            while (j >= 0 && buffer[j].distance > keyDist)
            {
                buffer[j + 1] = buffer[j];
                j--;
            }
            buffer[j + 1] = key;
        }
    }

    private void Update()
    {
        HandleGlobalToggleInput();

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) return;
        }

        UpdateDetection();
        HandleInteraction();
    }

    private void HandleGlobalToggleInput()
    {
        // Check if inspector checkbox changed
        if (enableHighlight != GlobalHighlightEnabled)
        {
            SetGlobalHighlight(enableHighlight);
        }

        // Optional runtime key toggle
        if (toggleHighlightKey != KeyCode.None && Input.GetKeyDown(toggleHighlightKey))
        {
            ToggleGlobalHighlight();
        }
    }

    /// <summary>
    /// Global API to enable or disable door highlighting from code, UI, or settings menus.
    /// </summary>
    public static void SetGlobalHighlight(bool enable)
    {
        GlobalHighlightEnabled = enable;
        if (instance != null)
        {
            instance.enableHighlight = enable;
            if (!enable && instance.currentHighlighter != null)
            {
                instance.currentHighlighter.SetHighlight(false);
            }
        }
    }

    /// <summary>
    /// Global API to toggle door highlighting.
    /// </summary>
    public static void ToggleGlobalHighlight()
    {
        SetGlobalHighlight(!GlobalHighlightEnabled);
    }

    private void UpdateDetection()
    {
        EasyDoor detectedDoor = FindDoorWithinRange();
        currentNPC = FindNPCWithinRange();

        if (detectedDoor != currentDoor)
        {
            // Disable highlight on previous door / chest
            if (currentHighlighter != null)
            {
                currentHighlighter.SetHighlight(false);
            }

            currentDoor = detectedDoor;
            currentHighlighter = null;

            if (currentDoor != null)
            {
                // Look for DoorHighlighter on parent (for chests), children, or self
                currentHighlighter = currentDoor.GetComponentInParent<DoorHighlighter>();
                if (currentHighlighter == null)
                {
                    currentHighlighter = currentDoor.GetComponentInChildren<DoorHighlighter>();
                }
                if (currentHighlighter == null)
                {
                    currentHighlighter = currentDoor.GetComponent<DoorHighlighter>();
                }

                // If not found, add to root so the entire chest or door gets highlighted
                if (currentHighlighter == null)
                {
                    Transform rootTransform = GetInteractableRoot(currentDoor);
                    currentHighlighter = rootTransform.gameObject.AddComponent<DoorHighlighter>();
                }
            }
        }

        // Manage highlight based on actionable state and global toggle
        if (currentDoor != null && currentHighlighter != null)
        {
            if (!GlobalHighlightEnabled || currentDoor.IsMoving)
            {
                // Highlight disappears while the door/chest is moving or when globally disabled
                currentHighlighter.SetHighlight(false);
            }
            else
            {
                // Reappears when the door/chest is in actionable position
                currentHighlighter.SetHighlight(true);
            }
        }
    }

    private NPCController FindNPCWithinRange()
    {
        Ray ray = new Ray(targetCamera.transform.position, targetCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            NPCController npc = hit.collider.GetComponentInParent<NPCController>();
            if (npc != null) return npc;
        }

        if (Physics.SphereCast(ray, 0.35f, out hit, interactDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            NPCController npc = hit.collider.GetComponentInParent<NPCController>();
            if (npc != null) return npc;
        }

        // Proximity check for NPC using cached ActiveNPCs list
        var allNPCs = NPCController.ActiveNPCs;
        NPCController closestNPC = null;
        float minDistance = interactDistance;

        Vector3 camPos = targetCamera.transform.position;
        Vector3 camForward = targetCamera.transform.forward;

        for (int i = 0; i < allNPCs.Count; i++)
        {
            NPCController npc = allNPCs[i];
            if (npc == null) continue;

            Vector3 offset = npc.transform.position - camPos;
            float sqrDist = offset.sqrMagnitude;
            if (sqrDist <= minDistance * minDistance)
            {
                float dist = Mathf.Sqrt(sqrDist);
                Vector3 toNPC = offset / Mathf.Max(dist, 0.0001f);
                float angle = Vector3.Angle(camForward, toNPC);
                if (angle <= 50f)
                {
                    minDistance = dist;
                    closestNPC = npc;
                }
            }
        }

        return closestNPC;
    }

    private EasyDoor FindDoorWithinRange()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
            if (targetCamera == null) targetCamera = GetComponentInChildren<Camera>();
            if (targetCamera == null) return null;
        }

        Vector3 camPos = targetCamera.transform.position;
        Vector3 camForward = targetCamera.transform.forward;
        Ray ray = new Ray(camPos, camForward);

        // 1. Direct raycast up to interactDistance using RaycastNonAlloc to bypass player colliders
        int hitCount = Physics.RaycastNonAlloc(ray, raycastBuffer, interactDistance, ~0, QueryTriggerInteraction.Ignore);
        SortHitsByDistance(raycastBuffer, hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = raycastBuffer[i];
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform) continue;

            EasyDoor directDoor = ResolveEasyDoor(hit.collider);
            if (directDoor != null)
            {
                return directDoor;
            }

            // Hit an opaque non-door obstacle in direct line of sight (e.g. wall)
            break;
        }

        // 2. Thick SphereCast up to interactDistance for forgiving aiming
        int sHitCount = Physics.SphereCastNonAlloc(ray, 0.25f, sphereCastBuffer, interactDistance, ~0, QueryTriggerInteraction.Ignore);
        SortHitsByDistance(sphereCastBuffer, sHitCount);

        for (int i = 0; i < sHitCount; i++)
        {
            RaycastHit sHit = sphereCastBuffer[i];
            if (sHit.collider == null) continue;
            if (sHit.collider.transform.IsChildOf(transform) || sHit.collider.transform == transform) continue;

            EasyDoor sDoor = ResolveEasyDoor(sHit.collider);
            if (sDoor != null)
            {
                return sDoor;
            }
        }

        // 3. Aim-based candidate selection using cached door list
        if (cachedDoors.Count == 0)
        {
            RefreshDoorCache();
        }

        EasyDoor bestCandidate = null;
        float bestScore = float.MaxValue;
        const float maxAimAngle = 45f; // Must be within reasonable view cone in front of camera
        float maxDistSqr = interactDistance * interactDistance;

        for (int i = 0; i < cachedDoors.Count; i++)
        {
            CachedDoorInfo info = cachedDoors[i];
            EasyDoor door = info.door;
            if (door == null) continue;

            if (door.IsMoving || info.wasMoving || info.lastOpenState != door.IsOpen)
            {
                info.center = ComputeInteractableCenter(info.root);
                info.lastOpenState = door.IsOpen;
                info.wasMoving = door.IsMoving;
                cachedDoors[i] = info;
            }

            Vector3 toTarget = info.center - camPos;
            float distSqr = toTarget.sqrMagnitude;
            if (distSqr > maxDistSqr) continue;

            float dist = Mathf.Sqrt(distSqr);
            Vector3 dirToTarget = toTarget / Mathf.Max(dist, 0.0001f);
            float angle = Vector3.Angle(camForward, dirToTarget);
            if (angle > maxAimAngle) continue;

            // Check line-of-sight to ensure not targeting through walls
            if (!HasLineOfSight(camPos, info.center, info.root, dist)) continue;

            // Score: primarily the angle (camera aim direction), with a small distance factor
            float score = angle + (dist / interactDistance) * 2.0f;
            if (score < bestScore)
            {
                bestScore = score;
                bestCandidate = door;
            }
        }

        return bestCandidate;
    }

    private Transform GetInteractableRoot(EasyDoor door)
    {
        if (door == null) return null;
        if (door.transform.parent != null && door.transform.parent.name.IndexOf("chest", System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return door.transform.parent;
        }
        return door.transform;
    }

    private Vector3 ComputeInteractableCenter(Transform root)
    {
        if (root == null) return Vector3.zero;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            bool foundFirst = false;
            foreach (Renderer r in renderers)
            {
                if (r == null || r.name.IndexOf("BlurOutline", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!foundFirst)
                {
                    b = r.bounds;
                    foundFirst = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }
            if (foundFirst) return b.center;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>();
        if (colliders != null && colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !colliders[i].isTrigger)
                {
                    b.Encapsulate(colliders[i].bounds);
                }
            }
            return b.center;
        }

        return root.position;
    }

    private bool HasLineOfSight(Vector3 camPos, Vector3 targetCenter, Transform root, float maxDist)
    {
        Vector3 dir = targetCenter - camPos;
        float dist = dir.magnitude;
        if (dist < 0.05f) return true;

        int hitCount = Physics.RaycastNonAlloc(camPos, dir / dist, losBuffer, dist, ~0, QueryTriggerInteraction.Ignore);
        SortHitsByDistance(losBuffer, hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = losBuffer[i];
            if (hit.collider == null) continue;
            if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform) continue;

            // If hit belongs to the interactable, line of sight is clear
            if (hit.collider.transform.IsChildOf(root) || hit.collider.transform == root)
            {
                return true;
            }

            // Hit an obstacle in between before reaching target
            if (hit.distance < dist - 0.15f)
            {
                return false;
            }
        }

        return true;
    }

    private EasyDoor ResolveEasyDoor(Collider col)
    {
        if (col == null) return null;
        if (colliderToDoorCache.TryGetValue(col, out EasyDoor cached))
        {
            return cached;
        }

        // 1. Direct component on collider
        EasyDoor door = col.GetComponent<EasyDoor>();
        if (door == null)
        {
            // 2. Child component (e.g. col is chest, EasyDoor is on chest_top)
            door = col.GetComponentInChildren<EasyDoor>();
        }
        if (door == null)
        {
            // 3. Parent component (e.g. col is Barn wood, EasyDoor is on _movable)
            door = col.GetComponentInParent<EasyDoor>();
        }

        colliderToDoorCache[col] = door;
        return door;
    }

    private void HandleInteraction()
    {
        if (IsLeftClickTriggered() || (allowKeyE && IsKeyETriggered()))
        {
            // Handle Door Interaction
            if (currentDoor != null && !currentDoor.IsMoving)
            {
                currentDoor.ToggleDoor();
                Han.CheckIn.HanGameDirector.Instance?.NotifyDoorToggled(currentDoor);

                if (currentHighlighter != null)
                {
                    currentHighlighter.SetHighlight(false);
                }
                return; // Prioritize door if both are targeted (rare, but good to handle)
            }

            // Handle NPC Interaction
            if (currentNPC != null)
            {
                currentNPC.StartPlayerConversation();
            }
        }
    }

    private bool IsLeftClickTriggered()
    {
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        try
        {
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    private bool IsKeyETriggered()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }

        try
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                return true;
            }
        }
        catch { }

        return false;
    }

    private void OnValidate()
    {
        GlobalHighlightEnabled = enableHighlight;
    }

    private void OnDisable()
    {
        if (currentHighlighter != null)
        {
            currentHighlighter.SetHighlight(false);
        }
        currentDoor = null;
        currentHighlighter = null;
    }
}
