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
            if (count == 0) return HanLocalization.Get("npc.someone");
            int pick = UnityEngine.Random.Range(0, count);
            for (int i = 0; i < NamesNearVictim.Count; i++)
            {
                if (NamesNearVictim[i] == exclude || NamesNearVictim[i] == VictimName) continue;
                if (pick-- == 0) return NamesNearVictim[i];
            }
            return HanLocalization.Get("npc.someone");
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

        [Header("Debug Controls")]
        [Tooltip("Key to instantly fast-forward all remaining check-ins and launch the Night phase.")]
        [SerializeField] private KeyCode debugFastForwardKey = KeyCode.F4;

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

        private bool fastForwardRequested = false;
        private int activeCheckInTarget = 0;

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
        private int activeDeskLineIdx = -1;
        private int activeDeskLineCount;
        private System.Action activeDeskAdvanceCallback;
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
        private string lastToastKey = "";
        private object[] lastToastArgs;
        private int lastAskedQuestionIdx = -1;
        private float toastUntil;
        private string endText = "";
        private bool endVictory;
        private string endReasonKey = "";
        private object[] endReasonArgs;
        private bool gameEnded;
        private CursorLockMode savedLock;
        private bool savedCursorVisible;
        private bool managedByGameLoop;
        private Coroutine activeNightRoutine;

        public bool RequiresCursorUnlocked => Phase == HanPhase.Accusation || gameEnded;
        public float NightTimeRemaining => Phase == HanPhase.Night ? Mathf.Max(0f, nightDuration - nightElapsed) : 0f;

        private GUIStyle labelStyle, boxStyle, buttonStyle, toggleStyle, titleStyle;
        private GUIStyle hudPillStyle, toastPillStyle, primaryButtonStyle, subtitleCenteredStyle, timerLabelStyle;
        private Texture2D dimBackdropTex, brassRuleTex, solidWhiteTex;

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
            HanLocalization.OnLanguageChanged += HandleLanguageChanged;
            NPCController.OnBodyInspected += HandleBodyInspected;
            if (bookUI != null) bookUI.OnShutterFlash += TriggerShutterFlash;
        }

        private void OnDisable()
        {
            HanLocalization.OnLanguageChanged -= HandleLanguageChanged;
            NPCController.OnBodyInspected -= HandleBodyInspected;
            if (bookUI != null) bookUI.OnShutterFlash -= TriggerShutterFlash;
        }

        private void HandleLanguageChanged(HanLanguage _)
        {
            for (int i = 0; i < guests.Count; i++)
            {
                if (guests[i] != null) GuestGenerator.ApplyLocalization(guests[i]);
            }
            if (activeDeskGuest != null) GuestGenerator.ApplyLocalization(activeDeskGuest);
            if (bookUI != null) bookUI.RefreshLocalization();

            if (gameEnded)
            {
                hudText = HanLocalization.Get(endVictory ? "director.end_victory_title" : "director.end_defeat_title");
                if (!string.IsNullOrEmpty(endReasonKey))
                {
                    endText = (endReasonArgs != null && endReasonArgs.Length > 0)
                        ? HanLocalization.Format(endReasonKey, endReasonArgs)
                        : HanLocalization.Get(endReasonKey);
                }
            }
            else
            {
                RefreshHud();
            }

            RefreshActiveToast();
            RefreshActiveDeskBubble();
        }

        private static void BuildCheckInLine(GuestData g, int lineIdx, int lineCount, out string lineText, out string buttonText, out string speakerHeader)
        {
            bool isLastLine = (lineIdx >= lineCount - 1);
            buttonText = isLastLine
                ? HanLocalization.Get("director.btn_approve")
                : HanLocalization.Get("director.btn_go_on");

            if (g.checkInDialogueSteps != null && lineIdx >= 0 && lineIdx < g.checkInDialogueSteps.Length)
            {
                lineText = g.checkInDialogueSteps[lineIdx].line;
                speakerHeader = g.checkInDialogueSteps[lineIdx].isEsmeralda ? "ANCUTZA" : null;
            }
            else if (g.checkInSmallTalk != null && lineIdx >= 0 && lineIdx < g.checkInSmallTalk.Length)
            {
                lineText = g.checkInSmallTalk[lineIdx];
                speakerHeader = null;
            }
            else
            {
                lineText = HanLocalization.Get("director.fallback_checkin_line");
                speakerHeader = null;
            }
        }

        private void RefreshActiveDeskBubble()
        {
            if (Phase != HanPhase.CheckIn || activeDeskGuest == null || activeDeskNpc == null || activeDeskNpc.Bubble == null)
                return;
            if (!activeDeskNpc.Bubble.IsInteractiveActive || activeDeskLineIdx < 0 || activeDeskAdvanceCallback == null)
                return;

            BuildCheckInLine(activeDeskGuest, activeDeskLineIdx, activeDeskLineCount, out string lineText, out string buttonText, out string speakerHeader);
            activeDeskNpc.Bubble.ShowInteractiveDialogue(lineText, activeDeskAdvanceCallback, buttonText, speakerHeader);
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
            if (dimBackdropTex != null) Destroy(dimBackdropTex);
            if (brassRuleTex != null) Destroy(brassRuleTex);
            if (solidWhiteTex != null) Destroy(solidWhiteTex);
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

            // End night hotkey [N] or [F6]
            if (Phase == HanPhase.Night && !endNightRequested && CheckEndNightKeyPressed())
            {
                RequestEndNight();
            }

            // Debug fast-forward check-ins hotkey [F4] or [K]
            if (Phase == HanPhase.CheckIn && !fastForwardRequested && CheckFastForwardKeyPressed())
            {
                FastForwardCheckIns();
            }
        }

        private bool CheckFastForwardKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.f4Key.wasPressedThisFrame || Keyboard.current.kKey.wasPressedThisFrame))
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(debugFastForwardKey) || Input.GetKeyDown(KeyCode.K))
                return true;
#endif
            return false;
        }

        private bool CheckEndNightKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.nKey.wasPressedThisFrame || Keyboard.current.f6Key.wasPressedThisFrame))
                return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(KeyCode.N) || Input.GetKeyDown(KeyCode.F6))
                return true;
#endif
            return false;
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

        /// <summary>
        /// Debug button / hotkey: Instantly fast-forwards all remaining check-ins for tonight,
        /// auto-registering travelers in the ledger and immediately launching the Night phase.
        /// </summary>
        [ContextMenu("Debug: Fast-Forward Check-Ins to Night (F4)")]
        public void FastForwardCheckIns()
        {
            if (Phase != HanPhase.CheckIn)
            {
                Debug.LogWarning($"<color=#E6C280>[HanGameDirector]</color> Cannot fast-forward check-ins: currently in phase {Phase}. (Fast-Forward is only available during CheckIn).");
                return;
            }

            fastForwardRequested = true;
            Debug.Log($"<color=#FFD700>[HanGameDirector]</color> ⏩ Fast-Forward triggered! Accelerating check-ins to launch Night {Night}...");
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
            ToastLocalized("director.toast_arrivals", 6f, Night);
            fastForwardRequested = false;
            activeCheckInTarget = target;
            RefreshHud();

            var wait = new WaitForSeconds(0.25f);
            for (int i = 0; i < roles.Count; i++)
            {
                if (fastForwardRequested)
                {
                    AutoCheckInRemainingRoles(roles, i);
                    break;
                }

                yield return CheckInOne(roles[i], wait);

                if (fastForwardRequested)
                {
                    AutoCheckInRemainingRoles(roles, i + 1);
                    break;
                }

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
                    g.horseNoteKey = "guest.note.horse_stall";
                    GuestGenerator.ApplyLocalization(g);
                }
                else
                {
                    g.horseNoteKey = "guest.note.horse_full";
                    GuestGenerator.ApplyLocalization(g);
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
            if (fastForwardRequested)
            {
                CompleteAndDismissActiveDeskGuest(g, npc);
                yield break;
            }
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
            if (fastForwardRequested)
            {
                CompleteAndDismissActiveDeskGuest(g, npc);
                yield break;
            }

            if (!HanLowEndOptimizer.IsLowEndModeEnabled)
            {
                Sprite capturedMugshot = CaptureMugshot(npc);
                if (capturedMugshot != null)
                {
                    if (g.portrait != null) Destroy(g.portrait);
                    g.portrait = capturedMugshot;
                }
            }

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

            if (fastForwardRequested)
            {
                CompleteAndDismissActiveDeskGuest(g, npc);
                yield break;
            }

            // Smoothly pan the camera cinematically from the book up to the character standing at the desk
            yield return PanCameraFromBookToCharacterCoroutine(bookInspectionPitch, characterFocusPitch, bookToCharacterPanDuration);

            if (fastForwardRequested)
            {
                CompleteAndDismissActiveDeskGuest(g, npc);
                yield break;
            }

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

            // Keep cursor locked and hidden during check-in
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            activeDeskGuest = g;
            activeDeskNpc = npc;
            checkInApproved = false;

            CheckInDialogueStep[] dialogueSteps = (g.checkInDialogueSteps != null && g.checkInDialogueSteps.Length > 0)
                ? g.checkInDialogueSteps
                : null;

            string[] fallbackLines = (g.checkInSmallTalk != null && g.checkInSmallTalk.Length > 0)
                ? g.checkInSmallTalk
                : new string[] { HanLocalization.Get("director.fallback_checkin_line") };

            int lineCount = dialogueSteps != null ? dialogueSteps.Length : fallbackLines.Length;

            ToastLocalized("director.toast_click_approve", 6f, g.guestName);

            for (int lineIdx = 0; lineIdx < lineCount && npc != null; lineIdx++)
            {
                if (fastForwardRequested) break;

                bool isLastLine = (lineIdx == lineCount - 1);
                string buttonText = isLastLine
                    ? HanLocalization.Get("director.btn_approve")
                    : HanLocalization.Get("director.btn_go_on");
                string lineText = dialogueSteps != null ? dialogueSteps[lineIdx].line : fallbackLines[lineIdx];
                string speakerHeader = (dialogueSteps != null && dialogueSteps[lineIdx].isEsmeralda) ? "ANCUTZA" : null;

                bool goOnClicked = false;
                System.Action advance = () => goOnClicked = true;
                activeDeskLineIdx = lineIdx;
                activeDeskLineCount = lineCount;
                activeDeskAdvanceCallback = advance;

                if (npc.Bubble != null)
                {
                    npc.Bubble.ShowInteractiveDialogue(lineText, advance, buttonText, speakerHeader);
                }
                else
                {
                    goOnClicked = true;
                }

                while (!goOnClicked && npc != null)
                {
                    if (fastForwardRequested) break;

                    // Ensure player movement stays strictly locked via StudiosAZE while free look remains unlocked
                    if (playerMovementSM != null && !playerMovementSM.IsMovementLocked)
                        playerMovementSM.SetMovementLocked(true, deskPlayerPosition);
                    yield return null;
                }
            }

            activeDeskLineIdx = -1;
            activeDeskLineCount = 0;
            activeDeskAdvanceCallback = null;

            if (fastForwardRequested)
            {
                CompleteAndDismissActiveDeskGuest(g, npc);
                yield break;
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

            ToastLocalized("__esmeralda_accept__", 6f);
            npc.Say(g.checkInApprovedLine, 2.8f);
            Debug.Log($"[Han] Check-in completed: {g.guestName} ({g.archetype}, {g.personality}), Room #{g.roomNumber}, Horse: {g.hasHorse} (stall {g.horseStallIndex}). (role: {g.role}, monsterType: {g.monsterType})");
            OnGuestCheckedIn?.Invoke(g);
            RefreshHud();

            // Make sure the approved NPC moves on away from the desk to make space for a new NPC
            yield return MoveGuestAwayFromDeskCoroutine(npc, g);
        }

        private void CompleteAndDismissActiveDeskGuest(GuestData g, NPCController npc)
        {
            if (npc != null && npc.Bubble != null)
                npc.Bubble.Hide();

            if (bookUI != null)
                bookUI.StampCheckedIn();

            g.isCheckedIn = true;
            if (!guests.Contains(g))
            {
                guests.Add(g);
                RefreshBook(guests.Count - 1);
                OnGuestCheckedIn?.Invoke(g);
            }

            if (npc != null)
            {
                if (rooms.TryGetValue(g.roomNumber, out var room) && room != null && room.bed != null)
                {
                    Vector3 bedPos = room.bed.transform.position;
                    npc.transform.position = bedPos;
                    if (npc.Agent != null)
                    {
                        if (!npc.Agent.enabled) npc.Agent.enabled = true;
                        if (NavMesh.SamplePosition(bedPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                            npc.Agent.Warp(hit.position);
                        npc.Agent.isStopped = true;
                    }
                    npc.SetHome(bedPos);
                }
                else
                {
                    Destroy(npc.gameObject);
                }
            }

            activeDeskGuest = null;
            activeDeskNpc = null;
            activeDeskLineIdx = -1;
            activeDeskLineCount = 0;
            activeDeskAdvanceCallback = null;

            if (bookViewController != null)
                bookViewController.SetDeskMode(false);
        }

        private void AutoCheckInRemainingRoles(List<GuestRole> roles, int startIndex)
        {
            for (int i = startIndex; i < roles.Count; i++)
            {
                GuestRole role = roles[i];
                GuestData g = GuestGenerator.Create(role, Night, usedNames, usedRooms, Mathf.Max(maxGuests, 10));

                if (g.hasHorse && allocatedStalls < stableHorses.Count)
                {
                    g.horseStallIndex = allocatedStalls++;
                    g.horseNoteKey = "guest.note.horse_stall";
                    GuestGenerator.ApplyLocalization(g);
                }

                g.isCheckedIn = true;
                guests.Add(g);

                // Spawn NPC directly in their bedroom so the inn is lively and populated for the night
                if (rooms.TryGetValue(g.roomNumber, out HanRoom room) && room != null && room.bed != null)
                {
                    Vector3 roomSpawn = room.bed.transform.position;
                    NPCController npc = SpawnNpcAt(g, roomSpawn, Quaternion.identity);
                    if (npc != null)
                    {
                        npc.SetHome(roomSpawn);
                    }
                }

                Debug.Log($"<color=#FFD700>[Han Fast-Forward]</color> Auto-checked in: {g.guestName} ({g.personality}), Room #{g.roomNumber}, Horse: {g.hasHorse}, Role: {g.role}");
                OnGuestCheckedIn?.Invoke(g);
            }

            if (guests.Count > 0)
            {
                RefreshBook(guests.Count - 1);
            }

            RefreshHud();
            ToastLocalized("director.toast_all_checked_in", 6f, guests.Count, Night);
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
                if (fastForwardRequested) break;
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
                if (fastForwardRequested) break;
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
                if (fastForwardRequested) break;
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

            const int mugshotSize = 128;

            if (snapshotRT == null)
            {
                snapshotRT = new RenderTexture(mugshotSize, mugshotSize, 16, RenderTextureFormat.ARGB32);
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

            Texture2D tex = new Texture2D(mugshotSize, mugshotSize, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, mugshotSize, mugshotSize), 0, 0);
            tex.Apply(false, !Application.isEditor);

            RenderTexture.active = prevActive;

            Sprite mugshot = Sprite.Create(tex, new Rect(0, 0, mugshotSize, mugshotSize), new Vector2(0.5f, 0.5f), mugshotSize);
            mugshot.name = "Mugshot_" + (npc.Guest != null ? npc.Guest.guestName : "Traveler");
            return mugshot;
        }

        public void AskQuestion(int q)
        {
            if (activeDeskGuest == null || activeDeskNpc == null) return;

            lastAskedQuestionIdx = q;
            string answer = GetQuestionAnswer(activeDeskGuest, q);
            activeDeskNpc.Say(answer, 5.0f);
            toastText = $"\"{answer}\"";
            toastUntil = Time.unscaledTime + 6f;
        }

        private string GetQuestionAnswer(GuestData guest, int q)
        {
            if (guest == null) return "";
            switch (q)
            {
                case 1:
                    return !string.IsNullOrEmpty(guest.responsePurpose)
                        ? guest.responsePurpose
                        : HanLocalization.Get("director.ask_fallback_purpose");
                case 2:
                    return !string.IsNullOrEmpty(guest.responseHorse)
                        ? guest.responseHorse
                        : HanLocalization.Get(guest.hasHorse ? "guest.response.horse_yes" : "guest.response.horse_no");
                case 3:
                    return !string.IsNullOrEmpty(guest.responseRoad)
                        ? guest.responseRoad
                        : HanLocalization.Get("director.ask_fallback_road");
                case 4:
                default:
                    return !string.IsNullOrEmpty(guest.responseIdentity)
                        ? guest.responseIdentity
                        : HanLocalization.Get("director.ask_fallback_identity");
            }
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
            nightElapsed = 0f;
            murdersTonight = 0;
            endNightRequested = false;
            SetPhase(HanPhase.Night);
            float nextAttempt = UnityEngine.Random.Range(murderDelayRange.x, murderDelayRange.y);

            // Spawn assigned horses in the stables
            int horseCount = 0;
            for (int i = 0; i < guests.Count; i++)
                if (guests[i].hasHorse && guests[i].isAlive) horseCount++;

            horseCount = Mathf.Min(horseCount, stableHorses.Count);
            for (int i = 0; i < stableHorses.Count; i++)
            {
                if (stableHorses[i] != null)
                {
                    bool shouldSpawn = i < horseCount;
                    stableHorses[i].SetActive(shouldSpawn);
                    if (shouldSpawn)
                    {
                        EnsureHorseBodyVisible(stableHorses[i]);
                    }
                }
            }
            Debug.Log($"[Han] Nightfall: {horseCount} of {stableHorses.Count} stable horse(s) spawned in the courtyard barn.");

            Debug.Log($"[Han] Night {Night} begins. Press N (or use the bell) to end the night early.");
            ToastLocalized("director.toast_night_falls", 6f, Night);
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

            killer.npc.Say(HanLocalization.Get("director.lure_killer"), 3.5f);
            yield return new WaitForSeconds(1.2f);
            victim.npc.Say(HanLocalization.Get("director.lure_victim"), 3f);

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
            ToastLocalized("director.toast_door_murder", 6f, room.roomNumber, victim.guestName);

            if (victim.isCheckedIn)
            {
                victim.isCheckedIn = false;
                victim.isDeceasedInBook = true;
                victim.deceasedRoomNumber = room.roomNumber;
                GuestGenerator.ApplyLocalization(victim);
                if (bookUI != null) bookUI.RefreshLocalization();
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
                ToastLocalized("director.toast_body_discovered", 6f, g.guestName);
                TriggerPanicNear(body.transform.position);
                OnBodyDiscovered?.Invoke(g);
                onMurderDiscovered?.Invoke();
            }

            if (g.isCheckedIn)
            {
                g.isCheckedIn = false;
                g.isDeceasedInBook = true;
                g.deceasedRoomNumber = 0;
                GuestGenerator.ApplyLocalization(g);
                if (bookUI != null) bookUI.RefreshLocalization();
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
                n.Say(HanLocalization.Get(n.Guest.role == GuestRole.Monster ? "director.panic_monster" : "director.panic_human"));
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
                EndGame(true, "director.end_victory_msg");
                return true;
            }
            if (monsters >= humans)
            {
                EndGame(false, "director.end_defeat_outnumbered");
                return true;
            }
            if (Night >= maxNights)
            {
                EndGame(false, "director.end_defeat_timeout", maxNights);
                return true;
            }
            return false;
        }

        private void EndGame(bool victory, string reasonKey, params object[] reasonArgs)
        {
            gameEnded = true;
            endVictory = victory;
            endReasonKey = reasonKey;
            endReasonArgs = reasonArgs;
            endText = (reasonArgs != null && reasonArgs.Length > 0)
                ? HanLocalization.Format(reasonKey, reasonArgs)
                : HanLocalization.Get(reasonKey);
            SetPhase(HanPhase.Ended);
            SetUiMode(true);
            if (victory)
            {
                Debug.Log("[Han] VICTORY: " + endText);
                OnVictory?.Invoke();
                onVictory?.Invoke();
            }
            else
            {
                Debug.Log("[Han] DEFEAT: " + endText);
                OnDefeat?.Invoke(endText);
                onDefeat?.Invoke();
            }
            hudText = HanLocalization.Get(victory ? "director.end_victory_title" : "director.end_defeat_title");
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
            ToastLocalized("director.toast_dawn");
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

            // Ensure Player does not have an errant camera-slicing Sprite quad child
            Transform errantSprite = player.Find("Sprite");
            if (errantSprite != null)
            {
                Debug.LogWarning("<color=#FF69B4>[HanGameDirector]</color> Removed camera-obstructing Sprite quad from Player.");
                Destroy(errantSprite.gameObject);
            }

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

            for (int i = 0; i < stableHorses.Count; i++)
            {
                if (stableHorses[i] != null)
                {
                    EnsureHorseBodyVisible(stableHorses[i]);
                }
            }
        }

        private static void EnsureHorseBodyVisible(GameObject horse)
        {
            if (horse == null) return;

            var skinnedRenderers = horse.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                var smr = skinnedRenderers[i];
                if (smr == null) continue;
                if (!smr.gameObject.activeSelf) smr.gameObject.SetActive(true);
                smr.enabled = true;
                smr.updateWhenOffscreen = true;
            }

            var meshRenderers = horse.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                var mr = meshRenderers[i];
                if (mr == null) continue;
                if (!mr.gameObject.activeSelf) mr.gameObject.SetActive(true);
                mr.enabled = true;
            }

            if (horse.activeInHierarchy)
            {
                var animators = horse.GetComponentsInChildren<Animator>(true);
                for (int i = 0; i < animators.Length; i++)
                {
                    if (animators[i] != null)
                    {
                        animators[i].cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        animators[i].Update(0f);
                    }
                }
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

        private void ToastLocalized(string key, float seconds = 6f, params object[] args)
        {
            lastAskedQuestionIdx = -1;
            lastToastKey = key;
            lastToastArgs = args;
            string text;
            if (key == "__esmeralda_accept__")
            {
                text = $"Ancutza: „{HanDialogueCatalog.EsmeraldaAcceptLine}”";
            }
            else
            {
                text = (args != null && args.Length > 0)
                    ? HanLocalization.Format(key, args)
                    : HanLocalization.Get(key);
            }
            toastText = text;
            toastUntil = Time.unscaledTime + seconds;
        }

        private void RefreshActiveToast()
        {
            if (Time.unscaledTime >= toastUntil || string.IsNullOrEmpty(toastText)) return;
            if (lastAskedQuestionIdx > 0 && activeDeskGuest != null)
            {
                string answer = GetQuestionAnswer(activeDeskGuest, lastAskedQuestionIdx);
                toastText = $"\"{answer}\"";
                return;
            }
            if (lastToastKey == "__esmeralda_accept__")
            {
                toastText = $"Ancutza: „{HanDialogueCatalog.EsmeraldaAcceptLine}”";
            }
            else if (!string.IsNullOrEmpty(lastToastKey))
            {
                toastText = (lastToastArgs != null && lastToastArgs.Length > 0)
                    ? HanLocalization.Format(lastToastKey, lastToastArgs)
                    : HanLocalization.Get(lastToastKey);
            }
        }

        private void Toast(string text, float seconds = 6f)
        {
            lastToastKey = "";
            lastToastArgs = null;
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
                    int totalTarget = activeCheckInTarget > 0 ? activeCheckInTarget : guests.Count;
                    hudText = HanLocalization.Format("director.hud_checkin", Night, guests.Count, totalTarget);
                    break;
                case HanPhase.Night:
                    hudText = HanLocalization.Format("director.hud_night", Night, remaining, guests.Count);
                    break;
                case HanPhase.Accusation:
                    hudText = HanLocalization.Format("director.hud_accusation", Night);
                    break;
            }
        }

        // ------------------------------------------------------------------
        //  IMGUI (works on WebGL, replace with your own UI through the events above)
        // ------------------------------------------------------------------
        private void EnsureStyles()
        {
            if (labelStyle != null) return;
            Font uiFont = HanUITheme.GetProjectImGuiFont();

            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, wordWrap = true };
            if (uiFont != null) labelStyle.font = uiFont;
            labelStyle.normal.textColor = HanUITheme.WarmParchment;

            subtitleCenteredStyle = new GUIStyle(labelStyle)
            {
                fontSize = 21,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            subtitleCenteredStyle.normal.textColor = HanUITheme.AgedPaper;

            titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = 32,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = HanUITheme.BurnishedGold;

            RectOffset sliceBorder = new RectOffset(8, 8, 8, 8);
            Texture2D cardTex = HanUITheme.GetImGuiCardTexture();
            Texture2D pillTex = HanUITheme.GetImGuiPillTexture();
            Texture2D btnNorm = HanUITheme.GetImGuiButtonTexture(false, false, false);
            Texture2D btnHover = HanUITheme.GetImGuiButtonTexture(true, false, false);
            Texture2D btnActive = HanUITheme.GetImGuiButtonTexture(false, true, false);
            Texture2D crimsonNorm = HanUITheme.GetImGuiButtonTexture(false, false, true);
            Texture2D crimsonHover = HanUITheme.GetImGuiButtonTexture(true, false, true);

            boxStyle = new GUIStyle(GUI.skin.box)
            {
                border = sliceBorder,
                padding = new RectOffset(20, 20, 16, 16)
            };
            boxStyle.normal.background = cardTex;

            hudPillStyle = new GUIStyle(GUI.skin.box)
            {
                font = uiFont,
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                border = sliceBorder,
                padding = new RectOffset(16, 16, 8, 8)
            };
            hudPillStyle.normal.background = pillTex;
            hudPillStyle.normal.textColor = HanUITheme.WarmParchment;

            toastPillStyle = new GUIStyle(hudPillStyle)
            {
                fontSize = 21,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(22, 22, 12, 12)
            };
            toastPillStyle.normal.textColor = HanUITheme.BurnishedGold;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                border = sliceBorder,
                padding = new RectOffset(18, 18, 8, 8)
            };
            if (uiFont != null) buttonStyle.font = uiFont;
            buttonStyle.normal.background = btnNorm;
            buttonStyle.hover.background = btnHover;
            buttonStyle.active.background = btnActive;
            buttonStyle.normal.textColor = HanUITheme.WarmParchment;
            buttonStyle.hover.textColor = HanUITheme.BurnishedGold;
            buttonStyle.active.textColor = HanUITheme.WarmParchment;

            primaryButtonStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 21
            };
            primaryButtonStyle.normal.background = crimsonNorm;
            primaryButtonStyle.hover.background = crimsonHover;
            primaryButtonStyle.active.background = crimsonHover;
            primaryButtonStyle.normal.textColor = HanUITheme.WarmParchment;
            primaryButtonStyle.hover.textColor = HanUITheme.BurnishedGold;

            toggleStyle = new GUIStyle(buttonStyle)
            {
                fontSize = 21,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(22, 18, 8, 8)
            };

            if (dimBackdropTex == null)
            {
                dimBackdropTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                dimBackdropTex.SetPixel(0, 0, HanUITheme.ModalBackdropDim);
                dimBackdropTex.Apply();
            }

            if (brassRuleTex == null)
            {
                brassRuleTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                brassRuleTex.SetPixel(0, 0, new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.45f));
                brassRuleTex.Apply();
            }

            if (solidWhiteTex == null)
            {
                solidWhiteTex = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                solidWhiteTex.SetPixel(0, 0, Color.white);
                solidWhiteTex.Apply();
            }

            timerLabelStyle = new GUIStyle(labelStyle)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            timerLabelStyle.normal.textColor = HanUITheme.BurnishedGold;
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

            float s = HanUITheme.GetMatchedReferenceScale();
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float w = Screen.width / s;
            float h = Screen.height / s;

            float topLeftBottomY = 22f;
            if (Phase != HanPhase.Ended && !string.IsNullOrEmpty(hudText))
            {
                float hudMaxW = Mathf.Clamp(w - 548f, 340f, 860f);
                GUIContent hudContent = new GUIContent(hudText);
                float measuredW = hudPillStyle.CalcSize(hudContent).x + 12f;
                float pillW = Mathf.Min(hudMaxW, Mathf.Max(280f, measuredW));
                float pillH = Mathf.Max(42f, hudPillStyle.CalcHeight(hudContent, pillW));
                Rect hudRect = new Rect(26f, 22f, pillW, pillH);
                GUI.Box(hudRect, hudContent, hudPillStyle);
                topLeftBottomY = hudRect.yMax;
            }

            // Fast-Forward button sits neatly under top-left HUD bar so Top-Right Dossier is never overlapped
            if (Phase == HanPhase.CheckIn && !fastForwardRequested)
            {
                float btnW = Mathf.Min(356f, w - 52f);
                float btnH = 42f;
                Rect btnRect = new Rect(26f, topLeftBottomY + 10f, btnW, btnH);

                if (GUI.Button(btnRect, HanLocalization.Get("director.fast_forward_btn"), primaryButtonStyle))
                {
                    FastForwardCheckIns();
                }
            }
            else if (Phase == HanPhase.Night)
            {
                DrawNightTimer(w, topLeftBottomY);
            }

            if (Time.unscaledTime < toastUntil && !string.IsNullOrEmpty(toastText))
            {
                float toastW = Mathf.Min(820f, w - 64f);
                GUIContent toastContent = new GUIContent(toastText);
                float toastH = Mathf.Max(54f, toastPillStyle.CalcHeight(toastContent, toastW));
                float toastY = h - 134f - toastH;
                GUI.Box(new Rect((w - toastW) * 0.5f, toastY, toastW, toastH), toastContent, toastPillStyle);
            }

            // Note: During HanPhase.CheckIn, no approval or denial options are shown.
            // The dialogue and "Go on" button are anchored directly above the character in 3D space.
            if (Phase == HanPhase.Accusation && kickTarget > 0)
                DrawAccusation(w, h);
            else if (Phase == HanPhase.Ended)
                DrawEnd(w, h);
        }

        private void DrawNightTimer(float w, float topLeftBottomY)
        {
            float remainingFloat = Mathf.Max(0f, nightDuration - nightElapsed);
            int remainingSeconds = Mathf.CeilToInt(remainingFloat);
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            string timerText = HanLocalization.Format("director.night_timer", $"{minutes:00}:{seconds:00}");

            GUIContent timerContent = new GUIContent(timerText);
            float measuredW = timerLabelStyle.CalcSize(timerContent).x + 32f;
            float timerW = Mathf.Min(Mathf.Max(320f, measuredW), Mathf.Max(280f, w - 52f));
            float timerH = 54f;
            Rect timerRect = new Rect(26f, topLeftBottomY + 10f, timerW, timerH);

            GUI.Box(timerRect, GUIContent.none, hudPillStyle);

            Color prevTextColor = timerLabelStyle.normal.textColor;
            timerLabelStyle.normal.textColor = remainingSeconds <= 30
                ? new Color(0.95f, 0.45f, 0.38f, 1f)
                : HanUITheme.BurnishedGold;
            GUI.Label(new Rect(timerRect.x + 14f, timerRect.y + 6f, timerRect.width - 28f, 26f), timerContent, timerLabelStyle);
            timerLabelStyle.normal.textColor = prevTextColor;

            if (solidWhiteTex != null)
            {
                float ratio = nightDuration > 0.01f ? Mathf.Clamp01(remainingFloat / nightDuration) : 0f;
                Rect trackRect = new Rect(timerRect.x + 14f, timerRect.yMax - 14f, timerRect.width - 28f, 6f);

                Color prevGuiColor = GUI.color;
                GUI.color = new Color(0.08f, 0.06f, 0.05f, 0.9f);
                GUI.DrawTexture(trackRect, solidWhiteTex);

                if (ratio > 0f)
                {
                    GUI.color = remainingSeconds <= 30 ? HanUITheme.StampCrimson : HanUITheme.BurnishedGold;
                    GUI.DrawTexture(new Rect(trackRect.x, trackRect.y, trackRect.width * ratio, trackRect.height), solidWhiteTex);
                }
                GUI.color = prevGuiColor;
            }
        }

        private void DrawCheckInInterrogation(float w, float h)
        {
            if (activeDeskGuest == null) return;

            float pw = Mathf.Min(600f, w - 48f);
            float ph = 350f;
            Rect r = new Rect(24f, h - ph - 24f, pw, ph);

            GUI.Box(r, GUIContent.none, boxStyle);

            string personalityLabel = HanLocalization.GetPersonalityLabel(activeDeskGuest.personality);
            GUI.Label(new Rect(r.x + 20f, r.y + 14f, pw - 40f, 34f), HanLocalization.Format("director.interrogate_header", activeDeskGuest.guestName, personalityLabel), titleStyle);
            string horseStr = HanLocalization.Get(activeDeskGuest.hasHorse ? "director.yes" : "director.no");
            GUI.Label(new Rect(r.x + 20f, r.y + 50f, pw - 40f, 26f), HanLocalization.Format("director.interrogate_meta", activeDeskGuest.roomNumber, horseStr, activeDeskGuest.arrivalTime), labelStyle);

            bool isChar = bookViewController != null && bookViewController.FocusMode == DeskFocusMode.CharacterFocus;
            string focusPrompt = HanLocalization.Get(isChar ? "director.focus_traveler" : "director.focus_ledger");

            if (GUI.Button(new Rect(r.x + 20f, r.y + 82f, pw - 40f, 36f), focusPrompt, buttonStyle))
            {
                bookViewController?.ToggleDeskFocus();
            }

            float qY = r.y + 126f;
            if (GUI.Button(new Rect(r.x + 20f, qY, pw - 40f, 34f), HanLocalization.Get("director.q1"), toggleStyle))
                AskQuestion(1);

            qY += 38f;
            if (GUI.Button(new Rect(r.x + 20f, qY, pw - 40f, 34f), HanLocalization.Get("director.q2"), toggleStyle))
                AskQuestion(2);

            qY += 38f;
            if (GUI.Button(new Rect(r.x + 20f, qY, pw - 40f, 34f), HanLocalization.Get("director.q3"), toggleStyle))
                AskQuestion(3);

            qY += 38f;
            if (GUI.Button(new Rect(r.x + 20f, qY, pw - 40f, 34f), HanLocalization.Get("director.q4"), toggleStyle))
                AskQuestion(4);

            qY += 44f;
            if (GUI.Button(new Rect(r.x + 20f, qY, pw - 40f, 44f), HanLocalization.Get("director.approve_stamp_btn"), primaryButtonStyle))
            {
                ApproveCheckIn();
            }
        }

        private void DrawAccusation(float w, float h)
        {
            if (dimBackdropTex != null)
                GUI.DrawTexture(new Rect(0f, 0f, w, h), dimBackdropTex);

            float pw = Mathf.Min(780f, w - 64f);
            float ph = 152f + accusationCandidates.Count * 54f + 92f;
            Rect r = new Rect((w - pw) * 0.5f, (h - ph) * 0.5f, pw, ph);
            GUI.Box(r, GUIContent.none, boxStyle);

            GUI.Label(new Rect(r.x + 28f, r.y + 20f, pw - 56f, 44f), HanLocalization.Get("director.accusation_title"), titleStyle);
            if (brassRuleTex != null)
                GUI.DrawTexture(new Rect(r.x + 56f, r.y + 68f, pw - 112f, 2f), brassRuleTex);

            GUI.Label(new Rect(r.x + 28f, r.y + 78f, pw - 56f, 36f), HanLocalization.Format("director.accusation_select", kickTarget, selected.Count), subtitleCenteredStyle);

            float y = r.y + 124f;
            for (int i = 0; i < accusationCandidates.Count; i++)
            {
                GuestData g = accusationCandidates[i];
                bool isSel = selected.Contains(g);
                string label = HanLocalization.Format("director.accusation_candidate", isSel ? "[X]  " : "[  ]  ", g.guestName, g.roomNumber);
                if (GUI.Button(new Rect(r.x + 32f, y, pw - 64f, 46f), label, isSel ? primaryButtonStyle : toggleStyle))
                    ToggleKickSelection(g);
                y += 54f;
            }

            GUI.enabled = selected.Count == kickTarget;
            float confirmW = Mathf.Min(360f, pw - 64f);
            if (GUI.Button(new Rect(r.x + (pw - confirmW) * 0.5f, y + 16f, confirmW, 54f), HanLocalization.Get("director.accusation_confirm"), primaryButtonStyle))
                ConfirmKickOut();
            GUI.enabled = true;
        }

        private void DrawEnd(float w, float h)
        {
            if (dimBackdropTex != null)
                GUI.DrawTexture(new Rect(0f, 0f, w, h), dimBackdropTex);

            bool victory = endVictory;
            float pw = Mathf.Min(880f, w - 64f);
            float ph = 390f;
            Rect r = new Rect((w - pw) * 0.5f, (h - ph) * 0.5f, pw, ph);
            GUI.Box(r, GUIContent.none, boxStyle);

            titleStyle.normal.textColor = victory ? HanUITheme.BurnishedGold : new Color(0.95f, 0.45f, 0.38f, 1f);
            GUI.Label(new Rect(r.x + 28f, r.y + 26f, pw - 56f, 54f), HanLocalization.Get(victory ? "director.end_victory_title" : "director.end_defeat_title"), titleStyle);
            titleStyle.normal.textColor = HanUITheme.BurnishedGold;

            if (brassRuleTex != null)
                GUI.DrawTexture(new Rect(r.x + 72f, r.y + 88f, pw - 144f, 2f), brassRuleTex);

            GUI.Label(new Rect(r.x + 48f, r.y + 110f, pw - 96f, 150f), endText, subtitleCenteredStyle);

            float btnW = Mathf.Min(340f, pw - 64f);
            if (GUI.Button(new Rect(r.x + (pw - btnW) * 0.5f, r.y + ph - 82f, btnW, 54f), HanLocalization.Get("director.end_play_again"), primaryButtonStyle))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}
