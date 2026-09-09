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

    private EasyDoor currentDoor;
    private NPCController currentNPC;
    private DoorHighlighter currentHighlighter;
    private static DoorInteractionController instance;

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
            // Disable highlight on previous door
            if (currentHighlighter != null)
            {
                currentHighlighter.SetHighlight(false);
            }

            currentDoor = detectedDoor;
            currentHighlighter = null;

            if (currentDoor != null)
            {
                currentHighlighter = currentDoor.GetComponent<DoorHighlighter>();
                if (currentHighlighter == null)
                {
                    currentHighlighter = currentDoor.gameObject.AddComponent<DoorHighlighter>();
                }
            }
        }

        // Manage highlight based on actionable state and global toggle
        if (currentDoor != null && currentHighlighter != null)
        {
            if (!GlobalHighlightEnabled || currentDoor.IsMoving)
            {
                // Highlight disappears while the door is moving or when globally disabled
                currentHighlighter.SetHighlight(false);
            }
            else
            {
                // Reappears when the door is in actionable position
                currentHighlighter.SetHighlight(true);
            }
        }
    }

    private NPCController FindNPCWithinRange()
    {
        Ray ray = new Ray(targetCamera.transform.position, targetCamera.transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            NPCController npc = hit.collider.GetComponentInParent<NPCController>();
            if (npc != null) return npc;
        }

        if (Physics.SphereCast(ray, 0.35f, out hit, interactDistance))
        {
            NPCController npc = hit.collider.GetComponentInParent<NPCController>();
            if (npc != null) return npc;
        }

        // Proximity check for NPC
        NPCController[] allNPCs = FindObjectsByType<NPCController>(FindObjectsSortMode.None);
        NPCController closestNPC = null;
        float minDistance = interactDistance;

        Vector3 camPos = targetCamera.transform.position;
        Vector3 camForward = targetCamera.transform.forward;

        foreach (NPCController npc in allNPCs)
        {
            if (npc == null) continue;

            float dist = Vector3.Distance(camPos, npc.transform.position);
            if (dist <= interactDistance && dist < minDistance)
            {
                Vector3 toNPC = (npc.transform.position - camPos).normalized;
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
        Ray ray = new Ray(targetCamera.transform.position, targetCamera.transform.forward);
        RaycastHit hit;

        // 1. Direct raycast up to 4 units
        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            EasyDoor door = ResolveEasyDoor(hit.collider);
            if (door != null) return door;
        }

        // 2. Thick SphereCast up to 4 units for forgiving aiming
        if (Physics.SphereCast(ray, 0.35f, out hit, interactDistance))
        {
            EasyDoor door = ResolveEasyDoor(hit.collider);
            if (door != null) return door;
        }

        // 3. Proximity check for any EasyDoor within 4 units in front of player
        EasyDoor[] allDoors = FindObjectsByType<EasyDoor>(FindObjectsSortMode.None);
        EasyDoor closestDoor = null;
        float minDistance = interactDistance;

        Vector3 camPos = targetCamera.transform.position;
        Vector3 camForward = targetCamera.transform.forward;

        foreach (EasyDoor door in allDoors)
        {
            if (door == null) continue;

            Collider col = door.GetComponentInChildren<Collider>();
            Vector3 doorPos = col != null ? col.bounds.center : door.transform.position;

            float dist = Vector3.Distance(camPos, doorPos);
            if (dist <= interactDistance && dist < minDistance)
            {
                Vector3 toDoor = (doorPos - camPos).normalized;
                float angle = Vector3.Angle(camForward, toDoor);
                if (angle <= 50f)
                {
                    minDistance = dist;
                    closestDoor = door;
                }
            }
        }

        return closestDoor;
    }

    private EasyDoor ResolveEasyDoor(Collider col)
    {
        if (col == null) return null;
        EasyDoor door = col.GetComponentInParent<EasyDoor>();
        if (door == null) door = col.GetComponentInChildren<EasyDoor>();
        if (door == null) door = col.GetComponent<EasyDoor>();
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
