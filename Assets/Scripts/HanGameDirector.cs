using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using AZE.AdvancedFirstPerson;

namespace Han.CheckIn
{
    public enum HanPhase
    {
        Idle,
        CheckIn,
        Night,
        Accusation,
        Ended
    }

    [System.Serializable]
    public class HanRoom
    {
        public int roomNumber;
        public SmartObject bed;
        public EasyDoorSystem.EasyDoor door;
        public Vector3 roomCenter;
    }

    /// <summary>
    /// Runs the full night loop of the Han:
    ///   Evening check-ins at the desk  ->  Night (a monster may murder)  ->  Accusation (kick out 2)  ->  Dawn (departures)  -> ...
    /// Win: no monster left in the Han after a kick-out. Defeat: monsters >= humans, or the night limit is reached.
    /// All logic is coroutine / event driven. Update() only polls one key and nothing allocates per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class HanGameDirector : MonoBehaviour
    {
        // ------------------------------------------------------------------
        //  Static state & events (hook your UI / audio / camera effects here)
        // ------------------------------------------------------------------
        public static HanGameDirector Instance { get; private set; }
        public static bool MurderDiscovered { get; private set; }
        public static string VictimName { get; private set; } = "";
        private static readonly List<string> NamesNearVictim = new List<string>();

        public static event Action<GuestData> OnGuestCheckedIn;
        public static event Action<GuestData, GuestData> OnMurderCommitted;   // victim, killer
        public static event Action<GuestData> OnBodyDiscovered;
        public static event Action<int> OnNightStarted;
        public static event Action<IReadOnlyList<GuestData>> OnAccusationPhaseStarted;
        public static event Action<GuestData> OnGuestKickedOut;
        public static event Action OnVictory;
        public static event Action<string> OnDefeat;
        public static event Action<HanPhase> OnPhaseChanged;

        /// <summary>Returns a name seen near the victim, excluding the speaker. Used by dialogue tokens.</summary>
        public static string PickSuspectName(string exclude)
        {
            int count = 0;
            for (int i = 0; i < NamesNearVictim.Count; i++)
                if (NamesNearVictim[i] != exclude && NamesNearVictim[i] != VictimName) count++;
            if (count == 0) return "someone";
            int pick = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < NamesNearVictim.Count; i++)
            {
                if (NamesNearVictim[i] == exclude || NamesNearVictim[i] == VictimName) continue;
                if (pick-- == 0) return NamesNearVictim[i];
            }
            return "someone";
        }

        // ------------------------------------------------------------------
        //  Inspector
        // ------------------------------------------------------------------
        [Header("Scene References")]
        [Tooltip("NPC prefab (or disabled scene NPC) cloned for every traveler. If empty, the first NPC found in the scene is used as the template and all scene NPCs are disabled.")]
        [SerializeField] private NPCController npcTemplate;
        [Tooltip("Where travelers appear (outside the gate). Must be on/near the NavMesh.")]
        [SerializeField] private Transform spawnPoint;
        [Tooltip("The fixed check-in desk. Travelers walk here to check in.")]
        [SerializeField] private Transform deskPoint;
        [Tooltip("Where kicked-out / departing guests walk before disappearing. Defaults to the spawn point.")]
        [SerializeField] private Transform exitPoint;
        [Tooltip("Guests wander around this point once checked in. Defaults to the desk.")]
        [SerializeField] private Transform homePoint;
        [SerializeField] private GuestBookUI bookUI;
        [SerializeField] private CheckInBookViewController bookViewController;

        [Header("Stables & Horses")]
        [Tooltip("Courtyard stable horses. Assigned and activated at nightfall for travelers with horses.")]
        [SerializeField] private List<GameObject> stableHorses = new List<GameObject>();

        [Header("Rules")]
        [SerializeField, Min(1)] private int minGuests = 6;
        [SerializeField, Min(1)] private int maxGuests = 10;
        [SerializeField, Min(1)] private int kickCount = 2;
        [Tooltip("If monsters are still alive after this many nights, the player loses.")]
        [SerializeField, Min(1)] private int maxNights = 7;
        [SerializeField, Min(1)] private int maxMurdersPerNight = 1;

        [Header("Timing (seconds)")]
        [SerializeField] private float checkInInterval = 5f;
        [SerializeField] private float nightDuration = 180f;
        [Tooltip("Earliest/latest moment of the night when a monster starts hunting.")]
        [SerializeField] private Vector2 murderDelayRange = new Vector2(25f, 70f);
        [SerializeField] private float murderAttemptInterval = 3f;

        [Header("Murder Opportunity")]
        [SerializeField] private float killRange = 2.5f;
        [Tooltip("A murder never happens if the player is this close, or looking at the scene within viewDistance.")]
        [SerializeField] private float noKillNearPlayer = 6f;
        [SerializeField] private float viewDistance = 16f;

        [Header("Check-in Desk & Cinematic Sequence")]
        [Tooltip("If true, a traveler waits at the desk until the player stands within deskRadius.")]
        [SerializeField] private bool requirePlayerNearDesk = false;
        [SerializeField] private float deskRadius = 3.5f;
        [SerializeField] private Vector3 deskPlayerPosition = new Vector3(-36.25f, 0.35f, 94.60f);
        [SerializeField] private float doorFacingYaw = 180f;
        [SerializeField] private float npcArrivalTweenDuration = 0.65f;
        [SerializeField] private float bookInspectionPitch = 22f;
        [SerializeField] private float characterFocusPitch = 1.5f;
        [SerializeField] private float bookToCharacterPanDuration = 0.85f;

        [Header("Flow")]
        [SerializeField] private bool autoStart = true;
        [SerializeField] private bool showHud = true;

        [Header("Events (optional Inspector hooks)")]
        public UnityEvent onNightStarted;
        public UnityEvent onMurderDiscovered;
        public UnityEvent onAccusationStarted;
        public UnityEvent onVictory;
        public UnityEvent onDefeat;

        // ------------------------------------------------------------------
        //  Runtime state
        // ------------------------------------------------------------------
        public HanPhase Phase { get; private set; } = HanPhase.Idle;
        public int Night { get; private set; }
        public IReadOnlyList<GuestData> Guests => guests;

        private readonly List<GuestData> guests = new List<GuestData>();
        private readonly HashSet<string> usedNames = new HashSet<string>();
        private readonly HashSet<int> usedRooms = new HashSet<int>();
        private readonly List<GuestData> accusationCandidates = new List<GuestData>();
        private readonly List<GuestData> selected = new List<GuestData>();
        private readonly List<MonoBehaviour> playerControls = new List<MonoBehaviour>();

        private readonly Dictionary<int, HanRoom> rooms = new Dictionary<int, HanRoom>();
        private HanRoom pendingDiscoveryRoom;
        private GuestData pendingDiscoveryVictim;
        private int allocatedStalls = 0;

        private GuestData activeDeskGuest;
        private NPCController activeDeskNpc;
        private bool checkInApproved;
        private Camera snapshotCamera;
        private RenderTexture snapshotRT;
        private PlayerMovementStateMachine playerMovementSM;
        private PlayerCameraController playerCameraCtrl;
        private float shutterFlashAlpha = 0f;
        private Texture2D whiteFlashTex;

        private bool endNightRequested;
        private bool accusationConfirmed;
        private int murdersTonight;
        private float nightElapsed;
        private int kickTarget;
        private string hudText = "";
        private string toastText = "";
        private float toastUntil;
        private string endText = "";
        private bool gameEnded;
        private CursorLockMode savedLock;
        private bool savedCursorVisible;
        private bool managedByGameLoop;
        private Coroutine activeNightRoutine;

        public bool RequiresCursorUnlocked => (Phase == HanPhase.CheckIn && activeDeskGuest != null) || Phase == HanPhase.Accusation || gameEnded;

        private GUIStyle labelStyle, boxStyle, buttonStyle, toggleStyle, titleStyle;

        private const float Tick = 0.5f;

        // ------------------------------------------------------------------
        //  Unity lifecycle
        // ------------------------------------------------------------------
        private void Awake()
        {
            Application.runInBackground = true;
            Instance = this;
            MurderDiscovered = false;
            VictimName = "";
            NamesNearVictim.Clear();

            if (bookUI == null) bookUI = FindFirstObjectByType<GuestBookUI>(FindObjectsInactive.Include);
            if (bookUI != null) bookUI.SuppressDefaultGuests = true;

            if (bookViewController == null)
                bookViewController = FindFirstObjectByType<CheckInBookViewController>(FindObjectsInactive.Include);

            EnsureStableHorses();
            BuildRoomMap();

            if (npcTemplate == null)
                npcTemplate = FindFirstObjectByType<NPCController>(FindObjectsInactive.Include);

            // Pre-placed scene NPCs are replaced by generated travelers.
            var sceneNpcs = FindObjectsByType<NPCController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < sceneNpcs.Length; i++)
                if (sceneNpcs[i] != null && sceneNpcs[i].Guest == null) sceneNpcs[i].gameObject.SetActive(false);

            CachePlayerControls();
        }

        private void OnEnable()
        {
            NPCController.OnBodyInspected += HandleBodyInspected;
            if (bookUI != null) bookUI.OnShutterFlash += TriggerShutterFlash;
        }

        private void OnDisable()
        {
            NPCController.OnBodyInspected -= HandleBodyInspected;
            if (bookUI != null) bookUI.OnShutterFlash -= TriggerShutterFlash;
        }

        private void TriggerShutterFlash()
        {
            shutterFlashAlpha = 0.85f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SetUiMode(false);
            if (snapshotCamera != null) Destroy(snapshotCamera.gameObject);
            if (snapshotRT != null) snapshotRT.Release();
            if (whiteFlashTex != null) Destroy(whiteFlashTex);
        }

        private void Start()
        {
            RefreshBook(0);
            if (autoStart && !managedByGameLoop && GameLoopManager.Instance == null)
                activeNightRoutine = StartCoroutine(GameLoop());
        }

        private void Update()
        {
            if (GameLoopManager.Instance != null && !GameLoopManager.Instance.IsGameplayActive)
                return;

            if (shutterFlashAlpha > 0f)
            {
                shutterFlashAlpha = Mathf.MoveTowards(shutterFlashAlpha, 0f, Time.deltaTime * 3.2f);
            }

            if (Phase == HanPhase.Night && !endNightRequested && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)
                RequestEndNight();
        }

        // ------------------------------------------------------------------
        //  Public API
        // ------------------------------------------------------------------
        public void SetManagedByGameLoop(bool managed)
        {
            managedByGameLoop = managed;
        }

        public void SetHudVisible(bool visible)
        {
            showHud = visible;
            RefreshHud();
        }

        public void BeginGame()
        {
            if (Phase == HanPhase.Idle)
                activeNightRoutine = StartCoroutine(GameLoop());
        }

        /// <summary>
        /// Called by GameLoopManager when transitioning from the Night Teaser into the Desk POV.
        /// Starts the check-in sequence for the specified night, bringing the first NPC to the desk.
        /// </summary>
        public void StartNightCheckInFromGameLoop(int nightNumber)
        {
            managedByGameLoop = true;
            if (activeNightRoutine != null)
            {
                StopCoroutine(activeNightRoutine);
                activeNightRoutine = null;
            }

            // Clean up any interrupted desk check-in state if transitioning early
            if (activeDeskGuest != null)
            {
                activeDeskGuest = null;
                activeDeskNpc = null;
                checkInApproved = false;
                if (bookViewController != null)
                    bookViewController.SetDeskMode(false);
            }

            if (Night > 0 && Phase != HanPhase.Idle)
            {
                ResolveDawn();
            }

            Night = Mathf.Max(1, nightNumber);
            activeNightRoutine = StartCoroutine(SingleNightManagedRoutine());
        }

        private IEnumerator SingleNightManagedRoutine()
        {
            if (npcTemplate == null)
            {
                Debug.LogError("[HanGameDirector] No NPC template found. Assign one in the Inspector.");
                yield break;
            }

            yield return CheckInPhase();
            yield return NightPhase();
            yield return AccusationPhase();
            if (EvaluateEnd()) yield break;
            ResolveDawn();

            activeNightRoutine = null;
            if (GameLoopManager.Instance != null)
            {
                GameLoopManager.Instance.CompleteCurrentNight();
            }
        }

        /// <summary>Call from a bell / clock / desk interaction to end the night early.</summary>
        public void RequestEndNight()
        {
            if (Phase == HanPhase.Night) endNightRequested = true;
        }

        /// <summary>Toggles a guest in the kick-out selection. Returns false if the limit is reached.</summary>
        public bool ToggleKickSelection(GuestData g)
        {
            if (Phase != HanPhase.Accusation || g == null || !accusationCandidates.Contains(g)) return false;
            if (selected.Remove(g)) return true;
            if (selected.Count >= kickTarget) return false;
            selected.Add(g);
            return true;
        }

        public bool IsSelectedForKick(GuestData g) => selected.Contains(g);

        /// <summary>Confirms the kick-out choice. Only succeeds with exactly the required number selected.</summary>
        public bool ConfirmKickOut()
        {
            if (Phase != HanPhase.Accusation || selected.Count != kickTarget) return false;
            accusationConfirmed = true;
            return true;
        }

        // ------------------------------------------------------------------
        //  Main loop
        // ------------------------------------------------------------------
        private IEnumerator GameLoop()
        {
            if (npcTemplate == null)
            {
                Debug.LogError("[HanGameDirector] No NPC template found. Assign one in the Inspector.");
                yield break;
            }

            Night = 0;
            while (!gameEnded)
            {
                Night++;
                yield return CheckInPhase();
                yield return NightPhase();
                yield return AccusationPhase();
                if (EvaluateEnd()) yield break;
                ResolveDawn();
            }
        }

        // ---------------- Evening check-ins ----------------
        private IEnumerator CheckInPhase()
        {
            SetPhase(HanPhase.CheckIn);
            MurderDiscovered = false;
            VictimName = "";
            NamesNearVictim.Clear();
            pendingDiscoveryRoom = null;
            pendingDiscoveryVictim = null;

            allocatedStalls = 0;
            for (int s = 0; s < stableHorses.Count; s++)
                if (stableHorses[s] != null) stableHorses[s].SetActive(false);

            int target = Mathf.Min(UnityEngine.Random.Range(minGuests, maxGuests + 1), maxGuests);
            int requiredMonsters = target <= 7 ? 1 : 2;
            int existing = guests.Count;
            int needed = Mathf.Max(0, target - existing);
            int newMonsters = Mathf.Clamp(requiredMonsters - CountAlive(GuestRole.Monster), 0, needed);

            var roles = new List<GuestRole>(needed);
            for (int i = 0; i < needed; i++) roles.Add(i < newMonsters ? GuestRole.Monster : GuestRole.Human);
            for (int i = roles.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (roles[i], roles[j]) = (roles[j], roles[i]);
            }

            Debug.Log($"[Han] Night {Night} evening: target {target} guests ({existing} staying, {needed} arriving, {requiredMonsters} monster(s) required).");
            Toast($"Night {Night}: travelers arrive at the Han desk...");
            RefreshHud();

            var wait = new WaitForSeconds(0.25f);
            for (int i = 0; i < roles.Count; i++)
            {
                yield return CheckInOne(roles[i], wait);
                if (i < roles.Count - 1)
                    yield return new WaitForSeconds(Mathf.Min(checkInInterval, 1.2f));
            }

            // All check-ins concluded: unlock player movement and restore normal exploration camera/cursor
            EnsurePlayerControllers();
            if (playerMovementSM != null)
                playerMovementSM.SetMovementLocked(false);
            if (playerCameraCtrl != null)
                playerCameraCtrl.SetFreeLookLocked(false);
            if (bookViewController != null)
                bookViewController.SetDeskMode(false);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private IEnumerator CheckInOne(GuestRole role, WaitForSeconds wait)
        {
            EnsurePlayerControllers();

            // =========================================================================
            // 1. PLAYER LOCK & INITIAL ALIGNMENT
            //    - Lock player movement via StudiosAZE (PlayerMovementStateMachine)
            //    - Fix player's position in place and align view to face directly toward the door
            //    - Keep free look disabled during the entrance sequence
            // =========================================================================
            if (playerMovementSM != null)
            {
                playerMovementSM.enabled = true;
                playerMovementSM.SetMovementLocked(true, deskPlayerPosition);
            }

            if (playerCameraCtrl != null)
            {
                playerCameraCtrl.enabled = true;
                playerCameraCtrl.SetFreeLookLocked(true);
                playerCameraCtrl.SetViewAlignment(doorFacingYaw, 0f);
            }

            if (bookViewController != null)
            {
                bookViewController.SetDeskMode(false);
            }

            GuestData g = GuestGenerator.Create(role, Night, usedNames, usedRooms, Mathf.Max(maxGuests, 10));

            // Assign horse stall if traveler brought a horse
            if (g.hasHorse)
            {
                if (allocatedStalls < stableHorses.Count)
                {
                    g.horseStallIndex = allocatedStalls++;
                    g.notes += $" (Cal înregistrat la grajd: Boxa #{g.horseStallIndex + 1})";
                }
                else
                {
                    g.notes += " (Grajdul e plin - cal priponit afară)";
                }
            }

            Vector3 desk = deskPoint != null ? deskPoint.position : new Vector3(-36.25f, 0.21f, 91.80f);
            Vector3 entranceStart = desk + new Vector3(0f, 0f, -7.2f);

            NPCController npc = SpawnNpcAt(g, entranceStart, Quaternion.Euler(0f, 0f, 0f));
            if (npc == null) yield break;

            // =========================================================================
            // 2. NPC ARRIVAL
            //    - Smoothly and quickly translate the character into position in front
            //      of the desk (using a fast tween/lerp coroutine)
            // =========================================================================
            yield return TranslateNpcToDeskCoroutine(npc, entranceStart, desk, npcArrivalTweenDuration);
            if (npc == null) yield break;

            // =========================================================================
            // 3. BOOK INSPECTION & CINEMATIC SEQUENCE
            //    - Capture mugshot of character at the desk
            //    - Transition/cut the camera focus directly onto the check-in book
            //    - Animate the character's details typing out smoothly onto the page
            //    - Display the character's picture with a camera shutter/screenshot flash effect
            //    - Smoothly pan the camera cinematically from the book up to the character
            // =========================================================================
            yield return null;
            Sprite capturedMugshot = CaptureMugshot(npc);
            if (capturedMugshot != null) g.portrait = capturedMugshot;

            // Cut camera focus directly onto the check-in book
            if (playerCameraCtrl != null)
            {
                playerCameraCtrl.SetViewAlignment(doorFacingYaw, bookInspectionPitch);
            }
            if (bookViewController != null)
            {
                bookViewController.CutToBookFocus();
            }

            // Animate character's details typing out smoothly onto the page + shutter flash portrait reveal
            if (bookUI != null)
            {
                yield return bookUI.TypewriteGuest(g);
            }
            else
            {
                TriggerShutterFlash();
                yield return new WaitForSeconds(0.6f);
            }

            // Smoothly pan the camera cinematically from the book up to the character standing at the desk
            yield return PanCameraFromBookToCharacterCoroutine(bookInspectionPitch, characterFocusPitch, bookToCharacterPanDuration);

            if (npc == null) yield break;

            // =========================================================================
            // 4. DIALOGUE UI & 5. CAMERA & INPUT STATE
            //    - As soon as the book typing sequence finishes and the dialogue appears,
            //      unlock free camera look/rotation
            //    - Keep character body translation strictly locked via StudiosAZE
            //    - Anchor the dialogue option floating directly above the character
            //    - Add a small "Go on" button positioned directly below the dialogue text
            //    - Do not show any approval or denial options at this stage
            // =========================================================================
            if (playerMovementSM != null)
            {
                playerMovementSM.SetMovementLocked(true, deskPlayerPosition);
            }
            if (playerCameraCtrl != null)
            {
                playerCameraCtrl.enabled = true;
                playerCameraCtrl.SetFreeLookLocked(false);
            }

            // Show cursor so the player can click "Go on" while freely rotating the camera
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            activeDeskGuest = g;
            activeDeskNpc = npc;
            checkInApproved = false;

            string[] dialogueLines = (g.checkInSmallTalk != null && g.checkInSmallTalk.Length > 0)
                ? g.checkInSmallTalk
                : new string[] { "Good evening, innkeeper. I seek a warm room for the night." };

            Toast($"{g.guestName}: Click \"Approve\" to check them in (Free Look unlocked).");

            for (int lineIdx = 0; lineIdx < dialogueLines.Length && npc != null; lineIdx++)
            {
                bool isLastLine = (lineIdx == dialogueLines.Length - 1);
                string buttonText = isLastLine ? "Approve >>" : "Go on >>";
                bool goOnClicked = false;
                if (npc.Bubble != null)
                {
                    npc.Bubble.ShowInteractiveDialogue(dialogueLines[lineIdx], () => goOnClicked = true, buttonText);
                }
                else
                {
                    goOnClicked = true;
                }

                while (!goOnClicked && npc != null)
                {
                    // Ensure player movement stays strictly locked via StudiosAZE while free look remains unlocked
                    if (playerMovementSM != null && !playerMovementSM.IsMovementLocked)
                        playerMovementSM.SetMovementLocked(true, deskPlayerPosition);
                    yield return null;
                }
            }

            if (npc != null && npc.Bubble != null)
            {
                npc.Bubble.Hide();
            }

            activeDeskGuest = null;
            activeDeskNpc = null;

            if (npc == null) yield break;

            // Complete check-in entry in the ledger
            if (bookUI != null)
                bookUI.StampCheckedIn();

            g.isCheckedIn = true;
            guests.Add(g);
            RefreshBook(guests.Count - 1);

            npc.Say(g.checkInApprovedLine, 2.8f);
            Debug.Log($"[Han] Check-in completed: {g.guestName} ({g.personality}), Room #{g.roomNumber}, Horse: {g.hasHorse} (stall {g.horseStallIndex}). (role: {g.role})");
            OnGuestCheckedIn?.Invoke(g);
            RefreshHud();

            // Make sure the approved NPC moves on away from the desk to make space for a new NPC
            yield return MoveGuestAwayFromDeskCoroutine(npc, g);
        }

        private IEnumerator MoveGuestAwayFromDeskCoroutine(NPCController npc, GuestData g)
        {
            if (npc == null) yield break;

            Vector3 desk = deskPoint != null ? deskPoint.position : new Vector3(-36.25f, 0.21f, 91.80f);

            // Determine traveler's destination (their assigned room bed, or home point / interior)
            Vector3 targetDestination;
            if (rooms.TryGetValue(g.roomNumber, out var room) && room != null && room.bed != null)
            {
                targetDestination = room.bed.transform.position;
            }
            else if (homePoint != null)
            {
                targetDestination = homePoint.position;
            }
            else
            {
                targetDestination = new Vector3(-25f, 4.21f, 95f);
            }

            npc.SetHome(targetDestination);

            NavMeshAgent agent = npc.Agent;
            if (agent != null)
            {
                if (!agent.enabled) agent.enabled = true;
                if (NavMesh.SamplePosition(npc.transform.position, out NavMeshHit hit, 3.5f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
                agent.isStopped = false;
                agent.speed = Mathf.Max(agent.speed, 2.6f);
                agent.angularSpeed = 360f;
                agent.acceleration = 12f;
                agent.ResetPath();
            }

            // Direct the NPC to walk away towards their destination
            npc.DirectTo(targetDestination, hold: false);

            // Distance the NPC must travel away from the desk to completely clear the counter for the next guest
            float clearDistance = 3.2f;
            float timeout = 4.0f;
            float elapsed = 0f;

            // Clear departure waypoint toward the eastern hall/staircase corridor
            Vector3 departureWaypoint = new Vector3(-31.5f, desk.y, 89.5f);

            while (npc != null && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                Vector3 flatNpcPos = new Vector3(npc.transform.position.x, desk.y, npc.transform.position.z);
                float distFromDesk = Vector3.Distance(flatNpcPos, desk);

                // If agent is slow to turn or start pathfinding near the counter, assist smooth stepping towards hallway
                if (distFromDesk < clearDistance && elapsed > 0.6f && (agent == null || agent.velocity.sqrMagnitude < 0.15f))
                {
                    Vector3 moveDir = (departureWaypoint - npc.transform.position).normalized;
                    moveDir.y = 0f;
                    npc.transform.position += moveDir * (2.4f * Time.deltaTime);
                    if (agent != null && agent.isOnNavMesh)
                    {
                        agent.nextPosition = npc.transform.position;
                        agent.isStopped = false;
                        agent.SetDestination(targetDestination);
                    }
                }

                if (distFromDesk >= clearDistance)
                {
                    Debug.Log($"[Han] Guest {g.guestName} moved on ({distFromDesk:F1}m away). Counter space cleared for next NPC.");
                    break;
                }

                yield return null;
            }

            // Brief pacing pause so the counter area feels open before the new traveler approaches
            yield return new WaitForSeconds(0.4f);
        }

        private IEnumerator TranslateNpcToDeskCoroutine(NPCController npc, Vector3 fromPos, Vector3 toPos, float duration)
        {
            if (npc == null) yield break;

            NavMeshAgent agent = npc.Agent;
            if (agent != null) agent.enabled = false;

            npc.transform.position = fromPos;
            Vector3 lookToPlayer = new Vector3(deskPlayerPosition.x - toPos.x, 0f, deskPlayerPosition.z - toPos.z);
            Quaternion targetRot = lookToPlayer.sqrMagnitude > 0.001f ? Quaternion.LookRotation(lookToPlayer.normalized) : Quaternion.identity;
            npc.transform.rotation = targetRot;

            float elapsed = 0f;
            float dur = Mathf.Max(0.1f, duration);
            while (elapsed < dur && npc != null)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / dur);
                // Fast Cubic Ease-Out tween
                float eased = 1f - Mathf.Pow(1f - u, 3f);
                npc.transform.position = Vector3.LerpUnclamped(fromPos, toPos, eased);
                npc.transform.rotation = targetRot;
                yield return null;
            }

            if (npc == null) yield break;
            npc.transform.position = toPos;
            npc.transform.rotation = targetRot;

            if (agent != null)
            {
                agent.enabled = true;
                if (NavMesh.SamplePosition(toPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
                if (agent.isOnNavMesh)
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                }
            }
        }

        private IEnumerator PanCameraFromBookToCharacterCoroutine(float startPitch, float endPitch, float duration)
        {
            float elapsed = 0f;
            float dur = Mathf.Max(0.1f, duration);

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / dur);
                float smoothT = Mathf.SmoothStep(0f, 1f, u);

                float currentPitch = Mathf.Lerp(startPitch, endPitch, smoothT);
                if (playerCameraCtrl != null)
                {
                    playerCameraCtrl.SetViewAlignment(doorFacingYaw, currentPitch);
                }

                if (bookViewController != null)
                {
                    // Smoothly lower book from close-up reading pose (1) to desk rest pose (0) as camera pans up to character
                    bookViewController.SetCinematicDeskBlend(1f - smoothT);
                }

                yield return null;
            }

            if (playerCameraCtrl != null)
            {
                playerCameraCtrl.SetViewAlignment(doorFacingYaw, endPitch);
            }
            if (bookViewController != null)
            {
                bookViewController.ReleaseCinematicToDeskRest();
            }
        }

        private NPCController SpawnNpcAt(GuestData g, Vector3 pos, Quaternion rot)
        {
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 4f, NavMesh.AllAreas)) pos = hit.position;

            NPCController npc = Instantiate(npcTemplate, pos, rot);
            npc.name = "Guest_" + g.guestName;
            npc.AssignGuest(g, true);
            npc.gameObject.SetActive(true);
            return npc;
        }

        private Sprite CaptureMugshot(NPCController npc)
        {
            if (npc == null) return null;

            if (snapshotRT == null)
            {
                snapshotRT = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
                snapshotRT.filterMode = FilterMode.Bilinear;
                snapshotRT.name = "Mugshot_RT";
            }

            if (snapshotCamera == null)
            {
                var go = new GameObject("HanSnapshotCamera");
                snapshotCamera = go.AddComponent<Camera>();
                snapshotCamera.enabled = false;
                snapshotCamera.clearFlags = CameraClearFlags.Color;
                snapshotCamera.backgroundColor = new Color(0.14f, 0.12f, 0.10f, 1f);
                snapshotCamera.nearClipPlane = 0.1f;
                snapshotCamera.farClipPlane = 20f;
                snapshotCamera.fieldOfView = 28f; // Close passport framing
                DontDestroyOnLoad(go);
            }

            // Position at player eye level
            Transform player = GetPlayer();
            Vector3 playerEye = player != null ? player.position + Vector3.up * 1.55f : new Vector3(-36.25f, 1.90f, 94.60f);
            snapshotCamera.transform.position = playerEye;

            // Aim directly at traveler face / upper chest
            Vector3 npcFace = npc.transform.position + Vector3.up * 1.40f;
            snapshotCamera.transform.LookAt(npcFace);

            snapshotCamera.targetTexture = snapshotRT;
            snapshotCamera.Render();
            snapshotCamera.targetTexture = null;

            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = snapshotRT;

            Texture2D tex = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            tex.Apply();

            RenderTexture.active = prevActive;

            Sprite mugshot = Sprite.Create(tex, new Rect(0, 0, 256, 256), new Vector2(0.5f, 0.5f), 256f);
            mugshot.name = "Mugshot_" + (npc.Guest != null ? npc.Guest.guestName : "Traveler");
            return mugshot;
        }

        public void AskQuestion(int q)
        {
            if (activeDeskGuest == null || activeDeskNpc == null) return;

            string answer = "";
            switch (q)
            {
                case 1:
                    answer = !string.IsNullOrEmpty(activeDeskGuest.responsePurpose) 
                        ? activeDeskGuest.responsePurpose 
                        : "Just looking for a warm bed tonight, innkeeper.";
                    break;
                case 2:
                    answer = !string.IsNullOrEmpty(activeDeskGuest.responseHorse) 
                        ? activeDeskGuest.responseHorse 
                        : (activeDeskGuest.hasHorse ? "My horse is stabled outside." : "I traveled on foot.");
                    break;
                case 3:
                    answer = !string.IsNullOrEmpty(activeDeskGuest.responseRoad) 
                        ? activeDeskGuest.responseRoad 
                        : "The mountain road was bitterly cold and dark.";
                    break;
                case 4:
                    answer = !string.IsNullOrEmpty(activeDeskGuest.responseIdentity) 
                        ? activeDeskGuest.responseIdentity 
                        : "My name is written truthfully on your ledger.";
                    break;
            }

            activeDeskNpc.Say(answer, 5.0f);
            Toast($"\"{answer}\"");
        }

        public void ApproveCheckIn()
        {
            if (activeDeskGuest == null || checkInApproved) return;
            checkInApproved = true;
        }

        private NPCController SpawnNpc(GuestData g)
        {
            Vector3 pos = spawnPoint != null ? spawnPoint.position : transform.position;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 6f, NavMesh.AllAreas)) pos = hit.position;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

            NPCController npc = Instantiate(npcTemplate, pos, rot);
            npc.name = "Guest_" + g.guestName;
            npc.AssignGuest(g, true);
            npc.gameObject.SetActive(true);
            return npc;
        }

        // ---------------- Night ----------------
        private IEnumerator NightPhase()
        {
            SetPhase(HanPhase.Night);
            murdersTonight = 0;
            endNightRequested = false;
            nightElapsed = 0f;
            float nextAttempt = UnityEngine.Random.Range(murderDelayRange.x, murderDelayRange.y);

            // Spawn assigned horses in the stables
            int horseCount = 0;
            for (int i = 0; i < guests.Count; i++)
                if (guests[i].hasHorse && guests[i].isAlive) horseCount++;

            horseCount = Mathf.Min(horseCount, stableHorses.Count);
            for (int i = 0; i < stableHorses.Count; i++)
            {
                if (stableHorses[i] != null)
                    stableHorses[i].SetActive(i < horseCount);
            }
            Debug.Log($"[Han] Nightfall: {horseCount} of {stableHorses.Count} stable horse(s) spawned in the courtyard barn.");

            Debug.Log($"[Han] Night {Night} begins. Press N (or use the bell) to end the night early.");
            Toast($"Night {Night} falls. Watch the guests, carry the ledger, press N to end the night.");
            OnNightStarted?.Invoke(Night);
            onNightStarted?.Invoke();

            var wait = new WaitForSeconds(Tick);
            while (nightElapsed < nightDuration && !endNightRequested)
            {
                yield return wait;
                nightElapsed += Tick;
                if (murdersTonight < maxMurdersPerNight && nightElapsed >= nextAttempt && CountAlive(GuestRole.Monster) > 0)
                {
                    TryMurder();
                    nextAttempt = nightElapsed + murderAttemptInterval;
                }
                RefreshHud();
            }
        }

        private bool IsObserved(Vector3 point)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            Vector3 d = point - cam.transform.position;
            float sq = d.sqrMagnitude;
            if (sq < noKillNearPlayer * noKillNearPlayer) return true;
            if (sq > viewDistance * viewDistance) return false;
            return Vector3.Dot(cam.transform.forward, d.normalized) > 0.35f;
        }

        private void Murder(GuestData killer, GuestData victim)
        {
            victim.isAlive = false;
            murdersTonight++;
            VictimName = victim.guestName;

            NamesNearVictim.Clear();
            Vector3 vp = victim.npc.transform.position;
            for (int i = 0; i < guests.Count; i++)
            {
                GuestData g = guests[i];
                if (g == victim || !g.isAlive || g.npc == null) continue;
                if ((g.npc.transform.position - vp).sqrMagnitude <= 8f * 8f) NamesNearVictim.Add(g.guestName);
            }

            victim.npc.Die();
            Debug.Log($"[Han] MURDER: {killer.guestName} (Monster) killed {victim.guestName}. Witness pool: {NamesNearVictim.Count}.");
            OnMurderCommitted?.Invoke(victim, killer);
            RefreshHud();
        }

        private void TryMurder()
        {
            GuestData killer = null, victim = null;
            float best = float.MaxValue;

            for (int i = 0; i < guests.Count; i++)
            {
                GuestData m = guests[i];
                if (!m.isAlive || m.role != GuestRole.Monster || m.npc == null || !m.npc.IsFreeToAct) continue;
                for (int j = 0; j < guests.Count; j++)
                {
                    GuestData h = guests[j];
                    if (!h.isAlive || h.role != GuestRole.Human || h.npc == null || !h.npc.CanBeKilled) continue;
                    float sq = (m.npc.transform.position - h.npc.transform.position).sqrMagnitude;
                    if (sq < best) { best = sq; killer = m; victim = h; }
                }
            }

            if (killer == null || victim == null) return;

            // Scenario: Monster lures victim to the victim's bedroom
            if (rooms.TryGetValue(victim.roomNumber, out HanRoom victimRoom) && victimRoom.bed != null)
            {
                if (!IsObserved(victimRoom.roomCenter) && !IsObserved(killer.npc.transform.position))
                {
                    StartCoroutine(LureAndMurderRoutine(killer, victim, victimRoom));
                    return;
                }
            }

            // Fallback direct kill if unobserved in close range
            if (best <= killRange * killRange &&
                !IsObserved(killer.npc.transform.position) && !IsObserved(victim.npc.transform.position))
            {
                Murder(killer, victim);
                return;
            }

            killer.npc.DirectTo(victim.npc.transform.position, false);
        }

        private IEnumerator LureAndMurderRoutine(GuestData killer, GuestData victim, HanRoom victimRoom)
        {
            Debug.Log($"[Han] Monster {killer.guestName} is luring {victim.guestName} into Odaia #{victimRoom.roomNumber}...");

            killer.npc.Say("Walk with me to your room... I have something urgent to show you.", 3.5f);
            yield return new WaitForSeconds(1.2f);
            victim.npc.Say("To my room? Alright... what is this about?", 3f);

            if (victimRoom.door != null && !victimRoom.door.IsOpen && !victimRoom.door.IsMoving)
                victimRoom.door.OpenDoor();

            Vector3 bedPos = victimRoom.bed.transform.position;
            killer.npc.DirectTo(victimRoom.roomCenter, true);
            victim.npc.DirectTo(bedPos, true);

            float t = 0f;
            while (t < 10f && killer.npc != null && victim.npc != null)
            {
                if ((victim.npc.transform.position - bedPos).sqrMagnitude <= 4f) break;
                yield return new WaitForSeconds(0.25f);
                t += 0.25f;
            }

            // Close door behind them!
            if (victimRoom.door != null && victimRoom.door.IsOpen && !victimRoom.door.IsMoving)
                victimRoom.door.CloseDoor();

            yield return new WaitForSeconds(1.8f);

            // Execute murder inside the room behind closed doors
            victim.isAlive = false;
            murdersTonight++;
            VictimName = victim.guestName;

            NamesNearVictim.Clear();
            Vector3 vp = victim.npc.transform.position;
            for (int i = 0; i < guests.Count; i++)
            {
                GuestData g = guests[i];
                if (g == victim || !g.isAlive || g.npc == null) continue;
                if ((g.npc.transform.position - vp).sqrMagnitude <= 12f * 12f) NamesNearVictim.Add(g.guestName);
            }

            victim.npc.Die();
            pendingDiscoveryRoom = victimRoom;
            pendingDiscoveryVictim = victim;
            MurderDiscovered = false; // Secret! Door is closed!

            Debug.Log($"[Han] MURDER COMMITTED behind closed doors in Odaia #{victimRoom.roomNumber}. Waiting for player to open the door.");
            OnMurderCommitted?.Invoke(victim, killer);

            yield return new WaitForSeconds(1.2f);
            if (victimRoom.door != null && !victimRoom.door.IsOpen && !victimRoom.door.IsMoving)
                victimRoom.door.OpenDoor();

            killer.npc.DirectTo(homePoint != null ? homePoint.position : transform.position, false);
            yield return new WaitForSeconds(1.5f);

            // Monster shuts the door behind them leaving the corpse inside
            if (victimRoom.door != null && victimRoom.door.IsOpen && !victimRoom.door.IsMoving)
                victimRoom.door.CloseDoor();

            killer.npc.ReleaseScripted();
        }

        /// <summary>
        /// Called when the player opens a door in the Han.
        /// If this door belongs to the room where a secret murder occurred, panic triggers immediately!
        /// </summary>
        public void NotifyDoorToggled(EasyDoorSystem.EasyDoor door)
        {
            if (door == null) return;
            if (pendingDiscoveryRoom != null && pendingDiscoveryRoom.door == door && !MurderDiscovered)
            {
                TriggerDoorMurderPanic(pendingDiscoveryVictim, pendingDiscoveryRoom);
                pendingDiscoveryRoom = null;
                pendingDiscoveryVictim = null;
            }
        }

        private void TriggerDoorMurderPanic(GuestData victim, HanRoom room)
        {
            if (victim == null) return;
            MurderDiscovered = true;
            VictimName = victim.guestName;

            Debug.Log($"[Han] DOOR OPENED: Player discovered the corpse of {victim.guestName} in Odaia #{room.roomNumber}! PANIC!");
            Toast($"You opened the door to Odaia #{room.roomNumber}... and found {victim.guestName} DEAD! Panic erupts across the Han!");

            if (victim.isCheckedIn)
            {
                victim.isCheckedIn = false;
                victim.notes = $"[DECEASED - found dead in Odaia #{room.roomNumber}] " + victim.notes;
                int idx = guests.IndexOf(victim);
                RefreshBook(idx >= 0 ? idx : 0);
            }

            TriggerPanicNear(room.door != null ? room.door.transform.position : room.roomCenter);
            OnBodyDiscovered?.Invoke(victim);
            onMurderDiscovered?.Invoke();
            RefreshHud();
        }

        private void HandleBodyInspected(NPCController body)
        {
            GuestData g = body != null ? body.Guest : null;
            if (g == null || g.isAlive) return;

            if (!MurderDiscovered)
            {
                MurderDiscovered = true;
                VictimName = g.guestName;
                Debug.Log($"[Han] BODY DISCOVERED: {g.guestName}. The whole Han is in panic.");
                Toast($"You found the body of {g.guestName}. Everyone in the Han is on edge.");
                TriggerPanicNear(body.transform.position);
                OnBodyDiscovered?.Invoke(g);
                onMurderDiscovered?.Invoke();
            }

            if (g.isCheckedIn)
            {
                g.isCheckedIn = false;
                g.notes = "[DECEASED - found dead] " + g.notes;
                int idx = guests.IndexOf(g);
                RefreshBook(idx >= 0 ? idx : 0);
            }
        }

        private void TriggerPanicNear(Vector3 center)
        {
            int shouted = 0;
            var all = NPCController.ActiveNPCs;
            for (int i = 0; i < all.Count && shouted < 3; i++)
            {
                NPCController n = all[i];
                if (n == null || n.IsDead || n.Guest == null) continue;
                if ((n.transform.position - center).sqrMagnitude > 12f * 12f) continue;
                n.Say(n.Guest.role == GuestRole.Monster ? "Terrible... terrible. Who could do this?" : "A body! There is a body!");
                shouted++;
            }
        }

        // ---------------- Accusation ----------------
        private IEnumerator AccusationPhase()
        {
            SetPhase(HanPhase.Accusation);

            // If a secret murder occurred in a bedroom and the door was never opened, reveal the body now
            if (!MurderDiscovered && pendingDiscoveryVictim != null)
            {
                TriggerDoorMurderPanic(pendingDiscoveryVictim, pendingDiscoveryRoom);
                pendingDiscoveryRoom = null;
                pendingDiscoveryVictim = null;
            }

            accusationCandidates.Clear();
            selected.Clear();
            accusationConfirmed = false;
            for (int i = 0; i < guests.Count; i++)
                if (guests[i].isAlive) accusationCandidates.Add(guests[i]);

            kickTarget = Mathf.Min(kickCount, accusationCandidates.Count);
            Debug.Log($"[Han] Night {Night} ends. Choose {kickTarget} guest(s) to throw out.");
            OnAccusationPhaseStarted?.Invoke(accusationCandidates);
            onAccusationStarted?.Invoke();

            if (kickTarget > 0)
            {
                SetUiMode(true);
                if (bookViewController != null) bookViewController.SetForceOpen(true);
                RefreshBook(0);
                while (!accusationConfirmed) yield return null;
                if (bookViewController != null) bookViewController.SetForceOpen(false);
                SetUiMode(false);

                for (int i = 0; i < selected.Count; i++) KickOut(selected[i]);
            }
            selected.Clear();
            accusationCandidates.Clear();
            RefreshBook(0);
        }

        private void KickOut(GuestData g)
        {
            guests.Remove(g);
            Debug.Log($"[Han] Kicked out: {g.guestName} (was {g.role}).");
            OnGuestKickedOut?.Invoke(g);
            Despawn(g, true);
        }

        // ---------------- End conditions ----------------
        private bool EvaluateEnd()
        {
            int monsters = CountAlive(GuestRole.Monster);
            int humans = CountAlive(GuestRole.Human);

            if (monsters == 0)
            {
                EndGame(true, "All monsters have been driven out of the Han. The village is safe!");
                return true;
            }
            if (monsters >= humans)
            {
                EndGame(false, "The monsters now outnumber the living. The Han belongs to them.");
                return true;
            }
            if (Night >= maxNights)
            {
                EndGame(false, $"{maxNights} nights have passed and the monsters still lurk among your guests.");
                return true;
            }
            return false;
        }

        private void EndGame(bool victory, string message)
        {
            gameEnded = true;
            endText = message;
            SetPhase(HanPhase.Ended);
            SetUiMode(true);
            if (victory)
            {
                Debug.Log("[Han] VICTORY: " + message);
                OnVictory?.Invoke();
                onVictory?.Invoke();
            }
            else
            {
                Debug.Log("[Han] DEFEAT: " + message);
                OnDefeat?.Invoke(message);
                onDefeat?.Invoke();
            }
            hudText = victory ? "VICTORY" : "DEFEAT";
        }

        // ---------------- Dawn ----------------
        private void ResolveDawn()
        {
            for (int i = guests.Count - 1; i >= 0; i--)
            {
                GuestData g = guests[i];
                if (!g.isAlive)
                {
                    guests.RemoveAt(i);
                    Despawn(g, false);
                    continue;
                }
                g.nightsStayed++;
                if (g.nightsStayed >= g.staysNights)
                {
                    guests.RemoveAt(i);
                    Debug.Log($"[Han] {g.guestName} finished their stay and leaves quietly.");
                    Despawn(g, true);
                }
            }
            RefreshBook(0);
            Toast("Dawn. Some guests have quietly left. A new evening approaches...");
        }

        private void Despawn(GuestData g, bool walkOut)
        {
            usedNames.Remove(g.guestName);
            usedRooms.Remove(g.roomNumber);
            if (g.npc != null && walkOut && !g.npc.IsDead) StartCoroutine(WalkOutAndDespawn(g));
            else
            {
                if (g.npc != null) Destroy(g.npc.gameObject);
                StartCoroutine(ReleaseLater(g, 0.5f));
            }
        }

        private IEnumerator WalkOutAndDespawn(GuestData g)
        {
            NPCController npc = g.npc;
            Vector3 exit = exitPoint != null ? exitPoint.position : (spawnPoint != null ? spawnPoint.position : transform.position);
            npc.DirectTo(exit, true);
            var wait = new WaitForSeconds(0.5f);
            float t = 0f;
            while (npc != null && t < 20f && (npc.transform.position - exit).sqrMagnitude > 6f)
            {
                yield return wait;
                t += 0.5f;
            }
            if (npc != null) Destroy(npc.gameObject);
            yield return new WaitForSeconds(0.5f);
            GuestGenerator.Release(g);
        }

        private IEnumerator ReleaseLater(GuestData g, float delay)
        {
            yield return new WaitForSeconds(delay);
            GuestGenerator.Release(g);
        }

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------
        private int CountAlive(GuestRole role)
        {
            int c = 0;
            for (int i = 0; i < guests.Count; i++)
                if (guests[i].isAlive && guests[i].role == role) c++;
            return c;
        }

        private void RefreshBook(int index)
        {
            if (bookUI == null) return;
            bookUI.LoadGuestRegistry(guests, Mathf.Clamp(index, 0, Mathf.Max(0, guests.Count - 1)));
        }

        private void SetPhase(HanPhase p)
        {
            Phase = p;
            RefreshHud();
            OnPhaseChanged?.Invoke(p);
        }

        private Transform GetPlayer()
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            return p != null ? p.transform : null;
        }

        private void EnsurePlayerControllers()
        {
            if (playerMovementSM != null && playerCameraCtrl != null) return;
            Transform player = GetPlayer();
            if (player != null)
            {
                if (playerMovementSM == null)
                    playerMovementSM = player.GetComponentInChildren<PlayerMovementStateMachine>(true);
                if (playerCameraCtrl == null)
                    playerCameraCtrl = player.GetComponentInChildren<PlayerCameraController>(true);
            }
            if (playerMovementSM == null)
                playerMovementSM = FindFirstObjectByType<PlayerMovementStateMachine>(FindObjectsInactive.Include);
            if (playerCameraCtrl == null)
                playerCameraCtrl = FindFirstObjectByType<PlayerCameraController>(FindObjectsInactive.Include);
        }

        private void CachePlayerControls()
        {
            playerControls.Clear();
            EnsurePlayerControllers();
            Transform player = GetPlayer();
            if (player == null) return;
            var all = player.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].GetType().Name;
                if (n == "PlayerMovementStateMachine" || n == "PlayerCameraController" ||
                    n == "CinemachineInputAxisController" || n == "DoorInteractionController")
                    playerControls.Add(all[i]);
            }
        }

        private void BuildRoomMap()
        {
            rooms.Clear();
            var smarts = FindObjectsByType<SmartObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var beds = new List<SmartObject>();
            for (int i = 0; i < smarts.Length; i++)
                if (smarts[i] != null && smarts[i].ObjectType == SmartObjectType.Bed)
                    beds.Add(smarts[i]);

            var doors = FindObjectsByType<EasyDoorSystem.EasyDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var roomDoors = new List<EasyDoorSystem.EasyDoor>();
            for (int i = 0; i < doors.Length; i++)
                if (doors[i] != null && doors[i].name == "_movable" && doors[i].transform.position.y > 4.5f)
                    roomDoors.Add(doors[i]);

            beds.Sort((a, b) =>
            {
                int cx = a.transform.position.x.CompareTo(b.transform.position.x);
                if (cx != 0) return cx;
                return a.transform.position.z.CompareTo(b.transform.position.z);
            });

            for (int i = 0; i < beds.Count; i++)
            {
                var bed = beds[i];
                EasyDoorSystem.EasyDoor closestDoor = null;
                float minDist = float.MaxValue;
                for (int j = 0; j < roomDoors.Count; j++)
                {
                    float dist = (roomDoors[j].transform.position - bed.transform.position).sqrMagnitude;
                    if (dist < minDist) { minDist = dist; closestDoor = roomDoors[j]; }
                }

                int roomNum = i + 1;
                Vector3 center = (bed.transform.position + (closestDoor != null ? closestDoor.transform.position : bed.transform.position)) * 0.5f;
                rooms[roomNum] = new HanRoom
                {
                    roomNumber = roomNum,
                    bed = bed,
                    door = closestDoor,
                    roomCenter = center
                };
            }

            Debug.Log($"[HanGameDirector] Initialized {rooms.Count} rooms with beds and doors.");
        }

        private void EnsureStableHorses()
        {
            if (stableHorses == null || stableHorses.Count == 0)
            {
                stableHorses = new List<GameObject>();
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                for (int i = 0; i < all.Length; i++)
                {
                    var go = all[i];
                    if (go != null && go.name.StartsWith("LPHorse_Saddled_") && go.scene.isLoaded && !stableHorses.Contains(go))
                    {
                        stableHorses.Add(go);
                    }
                }
                stableHorses.Sort((a, b) => a.name.CompareTo(b.name));
            }
        }

        /// <summary>Frees the cursor and freezes the player controller while a menu is open.</summary>
        private void SetUiMode(bool on)
        {
            if (on)
            {
                savedLock = Cursor.lockState;
                savedCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (!gameEnded)
            {
                Cursor.lockState = savedLock == CursorLockMode.None ? CursorLockMode.Locked : savedLock;
                Cursor.visible = savedLock == CursorLockMode.None ? false : savedCursorVisible;
            }
            for (int i = 0; i < playerControls.Count; i++)
                if (playerControls[i] != null) playerControls[i].enabled = !on;
        }

        private void Toast(string text, float seconds = 6f)
        {
            toastText = text;
            toastUntil = Time.unscaledTime + seconds;
        }

        private void RefreshHud()
        {
            if (!showHud) return;
            int remaining = Mathf.Max(0, Mathf.CeilToInt(nightDuration - nightElapsed));
            switch (Phase)
            {
                case HanPhase.CheckIn:
                    hudText = $"Night {Night}  |  Evening check-ins  |  Guests in ledger: {guests.Count}";
                    break;
                case HanPhase.Night:
                    hudText = $"Night {Night}  |  Time left: {remaining}s  |  Guests: {guests.Count}  |  [N] end the night";
                    break;
                case HanPhase.Accusation:
                    hudText = $"Night {Night}  |  Accusation";
                    break;
            }
        }

        // ------------------------------------------------------------------
        //  IMGUI (works on WebGL, replace with your own UI through the events above)
        // ------------------------------------------------------------------
        private void EnsureStyles()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
            labelStyle.normal.textColor = HanUITheme.WarmParchment;
            titleStyle = new GUIStyle(labelStyle) { fontSize = 34, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            titleStyle.normal.textColor = HanUITheme.BurnishedGold;
            boxStyle = new GUIStyle(GUI.skin.box);
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
            buttonStyle.normal.textColor = HanUITheme.WarmParchment;
            buttonStyle.hover.textColor = HanUITheme.BurnishedGold;
            toggleStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, alignment = TextAnchor.MiddleLeft };
            toggleStyle.normal.textColor = HanUITheme.WarmParchment;
            toggleStyle.hover.textColor = HanUITheme.BurnishedGold;
        }

        private void OnGUI()
        {
            // Camera shutter / screenshot flash effect overlay
            if (shutterFlashAlpha > 0.005f)
            {
                if (whiteFlashTex == null)
                {
                    whiteFlashTex = new Texture2D(1, 1);
                    whiteFlashTex.SetPixel(0, 0, Color.white);
                    whiteFlashTex.Apply();
                }
                Color prevColor = GUI.color;
                GUI.color = new Color(1f, 0.99f, 0.94f, shutterFlashAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), whiteFlashTex);
                GUI.color = prevColor;
            }

            if (!showHud || Phase == HanPhase.Idle) return;
            if (GameLoopManager.Instance != null && !GameLoopManager.Instance.IsGameplayActive) return;
            EnsureStyles();

            float s = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            float h = 1080f;

            if (Phase != HanPhase.Ended)
                GUI.Label(new Rect(20, 12, w - 40, 36), hudText, labelStyle);

            if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toastText))
                GUI.Label(new Rect(w * 0.5f - 480, h - 140, 960, 100), toastText, labelStyle);

            // Note: During HanPhase.CheckIn, no approval or denial options are shown.
            // The dialogue and "Go on" button are anchored directly above the character in 3D space.
            if (Phase == HanPhase.Accusation && kickTarget > 0)
                DrawAccusation(w, h);
            else if (Phase == HanPhase.Ended)
                DrawEnd(w, h);
        }

        private void DrawCheckInInterrogation(float w, float h)
        {
            if (activeDeskGuest == null) return;

            float pw = 600f;
            float ph = 350f;
            Rect r = new Rect(24f, h - ph - 24f, pw, ph);

            GUI.Box(r, GUIContent.none, boxStyle);

            GUI.Label(new Rect(r.x + 16, r.y + 10, pw - 32, 34), $"✦ INTEROGATORIU: {activeDeskGuest.guestName} ({activeDeskGuest.personality})", titleStyle);
            GUI.Label(new Rect(r.x + 16, r.y + 46, pw - 32, 26), $"Odaia #{activeDeskGuest.roomNumber} | Cal la grajd: {(activeDeskGuest.hasHorse ? "DA" : "NU")} | Sosire: {activeDeskGuest.arrivalTime}", labelStyle);

            bool isChar = bookViewController != null && bookViewController.FocusMode == DeskFocusMode.CharacterFocus;
            string focusPrompt = isChar 
                ? "[Click Drept / Q]  👁️ Focalizare: CĂLĂTOR  (Click pt Registru)" 
                : "[Click Drept / Q]  📖 Focalizare: REGISTRU  (Click pt Călător)";

            GUI.backgroundColor = isChar ? new Color(0.2f, 0.45f, 0.65f, 1f) : new Color(0.65f, 0.45f, 0.2f, 1f);
            if (GUI.Button(new Rect(r.x + 16, r.y + 78, pw - 32, 36), focusPrompt, buttonStyle))
            {
                bookViewController?.ToggleDeskFocus();
            }
            GUI.backgroundColor = Color.white;

            float qY = r.y + 122f;
            if (GUI.Button(new Rect(r.x + 16, qY, pw - 32, 34), "[1] \"Care este scopul sosirii tale la Han?\"", toggleStyle))
                AskQuestion(1);

            qY += 38f;
            if (GUI.Button(new Rect(r.x + 16, qY, pw - 32, 34), "[2] \"Ai sosit călare sau pe jos?\"", toggleStyle))
                AskQuestion(2);

            qY += 38f;
            if (GUI.Button(new Rect(r.x + 16, qY, pw - 32, 34), "[3] \"Ce ai zărit pe drumul muntelui?\"", toggleStyle))
                AskQuestion(3);

            qY += 38f;
            if (GUI.Button(new Rect(r.x + 16, qY, pw - 32, 34), "[4] \"Verifică numele și actele.\"", toggleStyle))
                AskQuestion(4);

            qY += 44f;
            GUI.backgroundColor = new Color(0.80f, 0.22f, 0.18f, 1f);
            if (GUI.Button(new Rect(r.x + 16, qY, pw - 32, 44), "[SPACE]  ✓ APROBĂ ȘI APLICĂ ȘTAMPILA", buttonStyle))
            {
                ApproveCheckIn();
            }
            GUI.backgroundColor = Color.white;
        }

        private void DrawAccusation(float w, float h)
        {
            float pw = 760f;
            float ph = 140f + accusationCandidates.Count * 52f + 80f;
            Rect r = new Rect((w - pw) * 0.5f, (h - ph) * 0.5f, pw, ph);
            GUI.Box(r, GUIContent.none, boxStyle);
            GUI.Label(new Rect(r.x + 20, r.y + 14, pw - 40, 44), "The night is over. Whom do you throw out?", titleStyle);
            GUI.Label(new Rect(r.x + 20, r.y + 62, pw - 40, 36), $"Select exactly {kickTarget}  ({selected.Count}/{kickTarget})", labelStyle);

            float y = r.y + 104f;
            for (int i = 0; i < accusationCandidates.Count; i++)
            {
                GuestData g = accusationCandidates[i];
                bool isSel = selected.Contains(g);
                string label = (isSel ? "[X]  " : "[  ]  ") + g.guestName + "   (room " + g.roomNumber + ")";
                if (GUI.Button(new Rect(r.x + 24, y, pw - 48, 46), label, toggleStyle))
                    ToggleKickSelection(g);
                y += 52f;
            }

            GUI.enabled = selected.Count == kickTarget;
            if (GUI.Button(new Rect(r.x + pw * 0.5f - 160, y + 12, 320, 52), "Throw them out", buttonStyle))
                ConfirmKickOut();
            GUI.enabled = true;
        }

        private void DrawEnd(float w, float h)
        {
            bool victory = hudText == "VICTORY";
            Rect r = new Rect((w - 900) * 0.5f, (h - 360) * 0.5f, 900, 360);
            GUI.Box(r, GUIContent.none, boxStyle);
            GUI.Label(new Rect(r.x + 20, r.y + 24, 860, 60), victory ? "VICTORY" : "DEFEAT", titleStyle);
            GUI.Label(new Rect(r.x + 40, r.y + 110, 820, 120), endText, labelStyle);
            if (GUI.Button(new Rect(r.x + 290, r.y + 270, 320, 56), "Play again", buttonStyle))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}
