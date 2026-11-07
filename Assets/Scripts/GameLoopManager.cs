using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace Han.CheckIn
{
    public enum GameLoopState
    {
        MainMenu,
        IntroVideo,
        NightTeaser,
        TransitionToGameplay,
        GameplayDeskPOV,
        Paused,
        NightTransition
    }

    /// <summary>
    /// Centralized Game-Loop Flow & Tactile UI State Manager:
    ///   Main Menu -> Intro Video -> Night Teaser ("Night One, good luck")
    ///   -> Smooth Transition to Desk POV + InitializeGameUI() + Desk Check-In Trigger
    ///   -> In-Game Pause Menu (Esc) with smooth background dim/DOF blur & modal tweening
    ///   -> Night Completion Transition ("Night Two, keep going", etc. from NightConfigData)
    /// </summary>
    [DisallowMultipleComponent]
    public class GameLoopManager : MonoBehaviour
    {
        public static GameLoopManager Instance { get; private set; }

        // ------------------------------------------------------------------
        //  1. Main Menu References
        // ------------------------------------------------------------------
        [Header("1. Main Menu UI")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private CanvasGroup mainMenuCanvasGroup;
        [SerializeField] private RectTransform mainMenuCardRect;
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button exitButton;

        [Header("Sub-Panels (Settings & Credits)")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private CanvasGroup settingsCanvasGroup;
        [SerializeField] private RectTransform settingsCardRect;
        [SerializeField] private Button settingsBackButton;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider lookSensitivitySlider;
        [SerializeField] private Toggle doorHighlightToggle;

        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private CanvasGroup creditsCanvasGroup;
        [SerializeField] private RectTransform creditsCardRect;
        [SerializeField] private Button creditsBackButton;

        // ------------------------------------------------------------------
        //  2. Intro Video References
        // ------------------------------------------------------------------
        [Header("2. Intro Video Sequence")]
        [SerializeField] private GameObject introVideoPanel;
        [SerializeField] private VideoPlayer introVideoPlayer;
        [SerializeField] private RawImage introVideoRawImage;
        [SerializeField] private VideoClip introVideoClip;
        [SerializeField] private Button skipVideoButton;
        [Tooltip("Allow skipping the intro video with Space, Enter, or Escape.")]
        [SerializeField] private bool allowKeyboardSkipVideo = true;
        [Tooltip("Fallback duration if no VideoClip or URL is assigned to the VideoPlayer.")]
        [SerializeField] private float fallbackVideoDuration = 2.5f;

        // ------------------------------------------------------------------
        //  3. Night Teaser & Data-Driven Config
        // ------------------------------------------------------------------
        [Header("3. Night Teaser & Data Asset")]
        [Tooltip("External ScriptableObject driving all night teaser titles and subtitles.")]
        [SerializeField] private NightConfigData nightConfigData;
        [SerializeField] private GameObject nightTeaserPanel;
        [SerializeField] private CanvasGroup nightTeaserCanvasGroup;
        [Tooltip("Primary text displaying the full teaser line (e.g. 'Night One, good luck').")]
        [SerializeField] private TMP_Text teaserHeadlineText;
        [Tooltip("Optional separate title text (e.g. 'Night One').")]
        [SerializeField] private TMP_Text teaserTitleText;
        [Tooltip("Optional separate subtitle text (e.g. 'good luck').")]
        [SerializeField] private TMP_Text teaserSubtitleText;
        [SerializeField] private float teaserFadeInDuration = 0.8f;
        [SerializeField] private float teaserHoldDuration = 2.8f;
        [SerializeField] private float teaserFadeOutDuration = 1.1f;

        // ------------------------------------------------------------------
        //  4. Transition to Gameplay (Desk POV) & HUD
        // ------------------------------------------------------------------
        [Header("4. Desk POV Transition & In-Game HUD")]
        [Tooltip("Full-screen fade overlay CanvasGroup used for smooth transitions.")]
        [SerializeField] private CanvasGroup screenFadeCanvasGroup;
        [Tooltip("Optional Transform defining the player's exact Desk POV position and yaw. If null, uses initial Player desk pose.")]
        [SerializeField] private Transform deskPovAnchor;
        [SerializeField] private Vector3 defaultDeskPosition = new Vector3(-36.25f, 0.35f, 94.6f);
        [SerializeField] private Vector3 defaultDeskEuler = new Vector3(0f, 180f, 0f);
        [Tooltip("Smoothly blends the camera FOV and pitch when settling into the Desk POV.")]
        [SerializeField] private bool enableCameraBlendOnDeskEntry = true;
        [SerializeField] private float cameraBlendDuration = 1.1f;
        [SerializeField] private float transitionStartFovOffset = -8f;
        [SerializeField] private GameObject inGameHudPanel;
        [SerializeField] private TMP_Text hudNightTitleText;
        [SerializeField] private TMP_Text hudObjectiveText;

        // ------------------------------------------------------------------
        //  5. In-Game Pause Menu & Blur / Dim Overlay
        // ------------------------------------------------------------------
        [Header("5. In-Game Pause Menu (Esc) & Smooth Blur/Dim")]
        [SerializeField] private GameObject pauseMenuPanel;
        [SerializeField] private CanvasGroup pauseMenuCanvasGroup;
        [SerializeField] private RectTransform pauseModalCardRect;
        [Tooltip("URP Volume blended smoothly (0 -> 1) when paused or in modal menus for Depth-of-Field blur & vignette.")]
        [SerializeField] private Volume pauseBlurVolume;
        [SerializeField] private float pauseTransitionDuration = 0.22f;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button pauseSettingsButton;
        [SerializeField] private Button pauseExitButton;
        [Tooltip("Key bound to toggle the in-game pause menu.")]
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        // ------------------------------------------------------------------
        //  6. References & Events
        // ------------------------------------------------------------------
        [Header("6. Scene, Audio & Focus Bindings")]
        [SerializeField] private HanGameDirector gameDirector;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private AudioSource uiAudioSource;
        [SerializeField] private HanUIAudioManager uiAudioManager;
        [SerializeField] private HanUIFocusNavigator focusNavigator;

        [Header("Lifecycle Events")]
        public UnityEvent onMainMenuOpened;
        public UnityEvent onIntroVideoStarted;
        public UnityEvent onIntroVideoCompleted;
        public UnityEvent<int, string> onNightTeaserShown;
        public UnityEvent onInitializeGameUI;
        public UnityEvent onStartDeskCheckIn;
        public UnityEvent onGamePaused;
        public UnityEvent onGameResumed;
        public UnityEvent<int> onNightCompleted;

        public static event Action<GameLoopState> OnStateChanged;
        public static event Action OnGameUIInitialized;
        public static event Action<int> OnDeskCheckInTriggered;
        public static event Action<bool> OnPauseStateChanged;

        // ------------------------------------------------------------------
        //  Runtime State
        // ------------------------------------------------------------------
        public GameLoopState CurrentState { get; private set; } = GameLoopState.MainMenu;
        public int CurrentNightIndex { get; private set; } = 0; // 0 = Night 1, 1 = Night 2...
        public int CurrentNightNumber => CurrentNightIndex + 1;
        public bool IsPaused => CurrentState == GameLoopState.Paused;
        public bool IsGameplayActive => CurrentState == GameLoopState.GameplayDeskPOV;
        public NightConfigData NightData => nightConfigData;

        private readonly List<Behaviour> playerControlBehaviours = new List<Behaviour>();
        private readonly Dictionary<Behaviour, bool> prePauseBehaviourStates = new Dictionary<Behaviour, bool>();
        private CursorLockMode prePauseCursorLock = CursorLockMode.Locked;
        private bool prePauseCursorVisible = false;

        private Coroutine activeFlowCoroutine;
        private Coroutine pauseTransitionCoroutine;
        private Coroutine subPanelCoroutine;
        private RenderTexture videoRenderTexture;
        private bool introVideoFinishedOrSkipped;
        private float initialCameraFov = 60f;
        private Camera mainCamera;

        // ------------------------------------------------------------------
        //  Unity Lifecycle
        // ------------------------------------------------------------------
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureReferences();
            BindUIButtons();
            ConfigureExplicitKeyboardNavigation();
            CachePlayerControllers();

            // Prevent HanGameDirector from auto-starting in the background while on Main Menu
            if (gameDirector != null)
            {
                gameDirector.SetManagedByGameLoop(true);
            }
        }

        private void Start()
        {
            ShowMainMenu();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (Time.timeScale == 0f) Time.timeScale = 1f;

            if (introVideoPlayer != null)
            {
                introVideoPlayer.loopPointReached -= HandleVideoLoopPointReached;
                introVideoPlayer.errorReceived -= HandleVideoError;
            }

            if (videoRenderTexture != null)
            {
                videoRenderTexture.Release();
                Destroy(videoRenderTexture);
            }
        }

        private void Update()
        {
            if (CurrentState == GameLoopState.IntroVideo && allowKeyboardSkipVideo)
            {
                if (WasSkipKeyPressed())
                {
                    SkipIntroVideo();
                }
            }
            else if (CurrentState == GameLoopState.MainMenu)
            {
                if (WasPauseOrCancelPressed())
                {
                    if ((settingsPanel != null && settingsPanel.activeSelf) ||
                        (creditsPanel != null && creditsPanel.activeSelf))
                    {
                        CloseSubPanels();
                    }
                }
            }
            else if (CurrentState == GameLoopState.GameplayDeskPOV || CurrentState == GameLoopState.Paused)
            {
                if (WasPauseOrCancelPressed())
                {
                    if (CurrentState == GameLoopState.Paused && settingsPanel != null && settingsPanel.activeSelf)
                    {
                        CloseSubPanels();
                    }
                    else
                    {
                        TogglePause();
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        //  1. Main Menu System
        // ------------------------------------------------------------------
        public void ShowMainMenu()
        {
            if (activeFlowCoroutine != null)
            {
                StopCoroutine(activeFlowCoroutine);
                activeFlowCoroutine = null;
            }

            Time.timeScale = 1f;
            SetState(GameLoopState.MainMenu);

            SetActiveSafe(mainMenuPanel, true);
            SetActiveSafe(settingsPanel, false);
            SetActiveSafe(creditsPanel, false);
            SetActiveSafe(introVideoPanel, false);
            SetActiveSafe(nightTeaserPanel, false);
            SetActiveSafe(pauseMenuPanel, false);
            SetActiveSafe(inGameHudPanel, false);

            if (mainMenuCanvasGroup != null)
            {
                mainMenuCanvasGroup.alpha = 1f;
                mainMenuCanvasGroup.interactable = true;
                mainMenuCanvasGroup.blocksRaycasts = true;
            }

            if (pauseBlurVolume != null)
            {
                pauseBlurVolume.weight = 1f;
            }

            if (screenFadeCanvasGroup != null)
            {
                screenFadeCanvasGroup.alpha = 0f;
                screenFadeCanvasGroup.blocksRaycasts = false;
                screenFadeCanvasGroup.interactable = false;
            }

            if (gameDirector != null)
            {
                gameDirector.SetHudVisible(false);
            }

            SetPlayerControlsEnabled(false);
            UnlockCursor();

            if (focusNavigator != null && playButton != null)
            {
                focusNavigator.FocusSelectable(playButton);
            }

            onMainMenuOpened?.Invoke();
            Debug.Log("[GameLoopManager] Main Menu displayed.");
        }

        /// <summary>
        /// Bound to Main Menu 'Play' button. Starts the Intro Video -> Night One Teaser -> Desk POV flow.
        /// </summary>
        public void OnPlayButtonClicked()
        {
            CurrentNightIndex = 0;
            CloseSubPanelsImmediate();
            SetActiveSafe(mainMenuPanel, false);

            if (pauseBlurVolume != null)
            {
                pauseBlurVolume.weight = 0f;
            }

            if (activeFlowCoroutine != null) StopCoroutine(activeFlowCoroutine);
            activeFlowCoroutine = StartCoroutine(PlayIntroAndStartNightRoutine(CurrentNightIndex));
        }

        /// <summary>
        /// Bound to 'Settings' button on Main Menu or Pause Menu.
        /// </summary>
        public void OnSettingsButtonClicked()
        {
            SetActiveSafe(creditsPanel, false);
            bool open = settingsPanel != null && !settingsPanel.activeSelf;
            if (open)
            {
                OpenModalSubPanel(settingsPanel, settingsCanvasGroup, settingsCardRect, settingsBackButton);
            }
            else
            {
                CloseSubPanels();
            }
        }

        /// <summary>
        /// Bound to 'Credits' button on Main Menu.
        /// </summary>
        public void OnCreditsButtonClicked()
        {
            SetActiveSafe(settingsPanel, false);
            bool open = creditsPanel != null && !creditsPanel.activeSelf;
            if (open)
            {
                OpenModalSubPanel(creditsPanel, creditsCanvasGroup, creditsCardRect, creditsBackButton);
            }
            else
            {
                CloseSubPanels();
            }
        }

        public void CloseSubPanels()
        {
            bool anyWasOpen = (settingsPanel != null && settingsPanel.activeSelf) ||
                              (creditsPanel != null && creditsPanel.activeSelf);

            CloseSubPanelsImmediate();

            if (anyWasOpen)
            {
                HanUIAudioManager.PlayMenuClose();
                if (focusNavigator != null)
                {
                    if (CurrentState == GameLoopState.Paused && resumeButton != null)
                        focusNavigator.FocusSelectable(resumeButton);
                    else if (CurrentState == GameLoopState.MainMenu && playButton != null)
                        focusNavigator.FocusSelectable(playButton);
                }
            }
        }

        private void CloseSubPanelsImmediate()
        {
            if (subPanelCoroutine != null)
            {
                StopCoroutine(subPanelCoroutine);
                subPanelCoroutine = null;
            }
            SetActiveSafe(settingsPanel, false);
            SetActiveSafe(creditsPanel, false);
        }

        private void OpenModalSubPanel(GameObject panel, CanvasGroup cg, RectTransform cardRect, Button defaultBtn)
        {
            if (panel == null) return;
            panel.SetActive(true);
            HanUIAudioManager.PlayPaperRustle();

            if (focusNavigator != null && defaultBtn != null)
            {
                focusNavigator.FocusSelectable(defaultBtn);
            }

            if (subPanelCoroutine != null) StopCoroutine(subPanelCoroutine);
            subPanelCoroutine = StartCoroutine(AnimateSubPanelPopInRoutine(cg, cardRect));
        }

        private IEnumerator AnimateSubPanelPopInRoutine(CanvasGroup cg, RectTransform cardRect)
        {
            float dur = 0.18f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                if (cg != null) cg.alpha = smooth;
                if (cardRect != null)
                {
                    float scale = Mathf.Lerp(0.93f, 1f, smooth);
                    cardRect.localScale = new Vector3(scale, scale, 1f);
                }
                yield return null;
            }
            if (cg != null) cg.alpha = 1f;
            if (cardRect != null) cardRect.localScale = Vector3.one;
            subPanelCoroutine = null;
        }

        /// <summary>
        /// Bound to 'Exit' button on Main Menu and 'Exit Game' button on Pause Menu.
        /// </summary>
        public void OnExitButtonClicked()
        {
            Time.timeScale = 1f;
            Debug.Log("[GameLoopManager] Exiting game...");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------
        //  2. Intro Video & Night Teaser Flow
        // ------------------------------------------------------------------
        private IEnumerator PlayIntroAndStartNightRoutine(int nightIndex)
        {
            // Step A: Play Full-Screen Intro Video
            yield return PlayIntroVideoRoutine();

            // Step B: Show Data-Driven Night Teaser ("Night One, good luck")
            yield return ShowNightTeaserRoutine(nightIndex);

            // Step C: Smooth Transition to Gameplay (Desk POV) + InitializeGameUI() + Start Desk Check-In
            yield return TransitionToDeskGameplayRoutine(nightIndex);

            activeFlowCoroutine = null;
        }

        private IEnumerator PlayIntroVideoRoutine()
        {
            SetState(GameLoopState.IntroVideo);
            introVideoFinishedOrSkipped = false;

            SetActiveSafe(introVideoPanel, true);
            UnlockCursor();

            if (focusNavigator != null && skipVideoButton != null)
            {
                focusNavigator.FocusSelectable(skipVideoButton);
            }

            onIntroVideoStarted?.Invoke();
            Debug.Log("[GameLoopManager] Starting Intro Video sequence...");

            bool hasValidVideo = false;
            if (introVideoPlayer != null)
            {
                if (introVideoClip != null && introVideoPlayer.clip == null)
                {
                    introVideoPlayer.clip = introVideoClip;
                }

                SetupVideoRenderTarget();

                if (introVideoPlayer.clip != null || !string.IsNullOrWhiteSpace(introVideoPlayer.url))
                {
                    hasValidVideo = true;
                    introVideoPlayer.loopPointReached -= HandleVideoLoopPointReached;
                    introVideoPlayer.errorReceived -= HandleVideoError;
                    introVideoPlayer.loopPointReached += HandleVideoLoopPointReached;
                    introVideoPlayer.errorReceived += HandleVideoError;

                    introVideoPlayer.Prepare();
                    float prepareTimeout = 4f;
                    while (!introVideoPlayer.isPrepared && !introVideoFinishedOrSkipped && prepareTimeout > 0f)
                    {
                        prepareTimeout -= Time.unscaledDeltaTime;
                        yield return null;
                    }

                    if (!introVideoFinishedOrSkipped && introVideoPlayer.isPrepared)
                    {
                        introVideoPlayer.Play();
                    }
                    else if (!introVideoPlayer.isPrepared)
                    {
                        hasValidVideo = false;
                    }
                }
            }

            if (hasValidVideo)
            {
                while (!introVideoFinishedOrSkipped && introVideoPlayer != null)
                {
                    yield return null;
                }
            }
            else
            {
                float elapsed = 0f;
                while (!introVideoFinishedOrSkipped && elapsed < fallbackVideoDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (introVideoPlayer != null)
            {
                introVideoPlayer.loopPointReached -= HandleVideoLoopPointReached;
                introVideoPlayer.errorReceived -= HandleVideoError;
                if (introVideoPlayer.isPlaying) introVideoPlayer.Stop();
            }

            SetActiveSafe(introVideoPanel, false);
            onIntroVideoCompleted?.Invoke();
            Debug.Log("[GameLoopManager] Intro Video finished.");
        }

        /// <summary>
        /// Public method to skip the intro video immediately (bound to Skip UI button or keyboard).
        /// </summary>
        public void SkipIntroVideo()
        {
            if (CurrentState != GameLoopState.IntroVideo) return;
            introVideoFinishedOrSkipped = true;
            Debug.Log("[GameLoopManager] Intro Video skipped by player.");
        }

        private void HandleVideoLoopPointReached(VideoPlayer source)
        {
            introVideoFinishedOrSkipped = true;
        }

        private void HandleVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning($"[GameLoopManager] VideoPlayer error ({message}). Proceeding to Night Teaser.");
            introVideoFinishedOrSkipped = true;
        }

        private IEnumerator ShowNightTeaserRoutine(int nightIndex)
        {
            SetState(GameLoopState.NightTeaser);

            // Fetch data-driven night entry from ScriptableObject asset
            NightConfigEntry entry = GetNightEntrySafe(nightIndex);
            string fullLine = entry.GetFullTeaserLine();

            if (teaserHeadlineText != null)
                teaserHeadlineText.text = fullLine;

            if (teaserTitleText != null)
                teaserTitleText.text = !string.IsNullOrWhiteSpace(entry.title) ? $"--  {entry.title.ToUpperInvariant()}  --" : fullLine;

            if (teaserSubtitleText != null)
                teaserSubtitleText.text = entry.subtitle ?? "";

            if (entry.teaserStinger != null && uiAudioSource != null)
            {
                uiAudioSource.PlayOneShot(entry.teaserStinger);
            }
            else
            {
                HanUIAudioManager.PlayPaperRustle();
            }

            SetActiveSafe(nightTeaserPanel, true);

            if (nightTeaserCanvasGroup != null)
            {
                nightTeaserCanvasGroup.alpha = 0f;
                nightTeaserCanvasGroup.blocksRaycasts = true;
            }

            if (screenFadeCanvasGroup != null)
            {
                screenFadeCanvasGroup.alpha = 1f;
                screenFadeCanvasGroup.blocksRaycasts = true;
            }

            onNightTeaserShown?.Invoke(entry.nightNumber, fullLine);
            Debug.Log($"[GameLoopManager] Showing Night Teaser #{entry.nightNumber}: \"{fullLine}\"");

            if (nightTeaserCanvasGroup != null)
            {
                yield return FadeCanvasGroupUnscaled(nightTeaserCanvasGroup, 0f, 1f, teaserFadeInDuration);
            }

            float holdTime = entry.displayDuration > 0f ? entry.displayDuration : teaserHoldDuration;
            float elapsed = 0f;
            while (elapsed < holdTime)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (nightTeaserCanvasGroup != null)
            {
                yield return FadeCanvasGroupUnscaled(nightTeaserCanvasGroup, 1f, 0f, teaserFadeOutDuration * 0.6f);
                nightTeaserCanvasGroup.blocksRaycasts = false;
            }

            SetActiveSafe(nightTeaserPanel, false);
        }

        // ------------------------------------------------------------------
        //  3. Transition to Gameplay (Desk POV), InitializeGameUI(), & Check-In Trigger
        // ------------------------------------------------------------------
        private IEnumerator TransitionToDeskGameplayRoutine(int nightIndex)
        {
            SetState(GameLoopState.TransitionToGameplay);

            // 1. Place player directly at the Desk POV before lifting the fade overlay
            PositionPlayerAtDeskPOV();

            // 2. Initialize In-Game HUD / UI
            InitializeGameUI();

            // 3. Smooth fade & camera blend from teaser into Player's Desk POV
            float duration = Mathf.Max(0.2f, enableCameraBlendOnDeskEntry ? cameraBlendDuration : teaserFadeOutDuration);
            float camStartFov = initialCameraFov + transitionStartFovOffset;

            if (enableCameraBlendOnDeskEntry && mainCamera != null)
            {
                mainCamera.fieldOfView = camStartFov;
            }

            if (pauseBlurVolume != null)
            {
                pauseBlurVolume.weight = 1f;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (screenFadeCanvasGroup != null)
                {
                    screenFadeCanvasGroup.alpha = 1f - smoothT;
                }

                if (pauseBlurVolume != null)
                {
                    pauseBlurVolume.weight = 1f - smoothT;
                }

                if (enableCameraBlendOnDeskEntry && mainCamera != null)
                {
                    mainCamera.fieldOfView = Mathf.Lerp(camStartFov, initialCameraFov, smoothT);
                }

                yield return null;
            }

            if (screenFadeCanvasGroup != null)
            {
                screenFadeCanvasGroup.alpha = 0f;
                screenFadeCanvasGroup.blocksRaycasts = false;
                screenFadeCanvasGroup.interactable = false;
            }

            if (pauseBlurVolume != null)
            {
                pauseBlurVolume.weight = 0f;
            }

            if (mainCamera != null)
            {
                mainCamera.fieldOfView = initialCameraFov;
            }

            // 4. Enable player controller and enter active gameplay state
            SetState(GameLoopState.GameplayDeskPOV);
            SetPlayerControlsEnabled(true);
            LockCursorForGameplay();

            // 5. Fire the event/trigger to start the desk check-in sequence (bringing the first NPC to the desk)
            StartDeskCheckInSequence(nightIndex + 1);
        }

        /// <summary>
        /// Positions and orients the player controller directly into the Desk POV looking at the check-in counter.
        /// </summary>
        public void PositionPlayerAtDeskPOV()
        {
            if (playerTransform == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            if (playerTransform == null) return;

            Vector3 targetPos = deskPovAnchor != null ? deskPovAnchor.position : defaultDeskPosition;
            Quaternion targetRot = deskPovAnchor != null ? deskPovAnchor.rotation : Quaternion.Euler(defaultDeskEuler);

            var cc = playerTransform.GetComponent<CharacterController>();
            bool wasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            playerTransform.SetPositionAndRotation(targetPos, targetRot);

            var camCtrl = playerTransform.GetComponent<AZE.AdvancedFirstPerson.PlayerCameraController>();
            if (camCtrl != null && camCtrl.CameraTransform != null)
            {
                camCtrl.CameraTransform.localRotation = Quaternion.identity;
            }

            var legacyMove = playerTransform.GetComponent<movement>();
            if (legacyMove != null && legacyMove.cameraTransform != null)
            {
                legacyMove.cameraTransform.localRotation = Quaternion.identity;
            }

            if (cc != null) cc.enabled = wasEnabled;
        }

        /// <summary>
        /// Placeholder and extensible initialization hook for the in-game HUD/UI.
        /// Called automatically when transitioning from the Night Teaser into the Desk POV.
        /// </summary>
        public virtual void InitializeGameUI()
        {
            SetActiveSafe(inGameHudPanel, true);

            NightConfigEntry entry = GetNightEntrySafe(CurrentNightIndex);
            if (hudNightTitleText != null)
            {
                hudNightTitleText.text = $"[ {entry.title.ToUpperInvariant()} ]";
            }

            if (hudObjectiveText != null)
            {
                hudObjectiveText.text = "Inspect arriving travelers at the ledger.  [ESC] Pause  |  [F6] End Night";
            }

            if (gameDirector != null)
            {
                gameDirector.SetHudVisible(true);
            }

            onInitializeGameUI?.Invoke();
            OnGameUIInitialized?.Invoke();
            Debug.Log($"[GameLoopManager] InitializeGameUI() executed for Night #{CurrentNightNumber}.");
        }

        /// <summary>
        /// Fires the desk check-in sequence event and instructs HanGameDirector to bring the first NPC to the desk.
        /// </summary>
        public void StartDeskCheckInSequence(int nightNumber)
        {
            Debug.Log($"[GameLoopManager] Starting Desk Check-In Sequence for Night {nightNumber} (bringing first NPC to desk).");
            onStartDeskCheckIn?.Invoke();
            OnDeskCheckInTriggered?.Invoke(nightNumber);

            if (gameDirector != null)
            {
                gameDirector.StartNightCheckInFromGameLoop(nightNumber);
            }
        }

        // ------------------------------------------------------------------
        //  4. In-Game Pause Menu (Bound to Esc) with Smooth Dim & DOF Blur
        // ------------------------------------------------------------------
        public void TogglePause()
        {
            if (CurrentState == GameLoopState.GameplayDeskPOV)
            {
                PauseGame();
            }
            else if (CurrentState == GameLoopState.Paused)
            {
                ResumeGame();
            }
        }

        public void PauseGame()
        {
            if (CurrentState != GameLoopState.GameplayDeskPOV) return;

            prePauseCursorLock = Cursor.lockState;
            prePauseCursorVisible = Cursor.visible;

            SaveAndDisablePlayerControlsForPause();

            Time.timeScale = 0f;
            SetState(GameLoopState.Paused);

            SetActiveSafe(pauseMenuPanel, true);
            SetActiveSafe(settingsPanel, false);
            UnlockCursor();

            HanUIAudioManager.PlayMenuOpen();

            if (focusNavigator != null && resumeButton != null)
            {
                focusNavigator.FocusSelectable(resumeButton);
            }

            if (pauseTransitionCoroutine != null) StopCoroutine(pauseTransitionCoroutine);
            pauseTransitionCoroutine = StartCoroutine(AnimatePauseOverlayRoutine(opening: true));

            onGamePaused?.Invoke();
            OnPauseStateChanged?.Invoke(true);
            Debug.Log("[GameLoopManager] Game Paused (Time.timeScale = 0).");
        }

        public void ResumeGame()
        {
            if (CurrentState != GameLoopState.Paused) return;

            CloseSubPanelsImmediate();
            HanUIAudioManager.PlayMenuClose();

            Time.timeScale = 1f;
            SetState(GameLoopState.GameplayDeskPOV);

            RestorePlayerControlsAfterPause();

            bool directorNeedsCursor = gameDirector != null && gameDirector.RequiresCursorUnlocked;
            if (directorNeedsCursor)
            {
                UnlockCursor();
            }
            else
            {
                Cursor.lockState = prePauseCursorLock == CursorLockMode.None ? CursorLockMode.Locked : prePauseCursorLock;
                Cursor.visible = prePauseCursorLock == CursorLockMode.None ? false : prePauseCursorVisible;
            }

            if (pauseTransitionCoroutine != null) StopCoroutine(pauseTransitionCoroutine);
            pauseTransitionCoroutine = StartCoroutine(AnimatePauseOverlayRoutine(opening: false));

            onGameResumed?.Invoke();
            OnPauseStateChanged?.Invoke(false);
            Debug.Log("[GameLoopManager] Game Resumed (Time.timeScale = 1).");
        }

        private IEnumerator AnimatePauseOverlayRoutine(bool opening)
        {
            float dur = Mathf.Max(0.08f, pauseTransitionDuration);
            float elapsed = 0f;

            float startAlpha = pauseMenuCanvasGroup != null ? pauseMenuCanvasGroup.alpha : (opening ? 0f : 1f);
            float targetAlpha = opening ? 1f : 0f;

            float startBlur = pauseBlurVolume != null ? pauseBlurVolume.weight : (opening ? 0f : 1f);
            float targetBlur = opening ? 1f : 0f;

            if (pauseMenuCanvasGroup != null)
            {
                pauseMenuCanvasGroup.interactable = opening;
                pauseMenuCanvasGroup.blocksRaycasts = opening;
            }

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                if (pauseMenuCanvasGroup != null)
                {
                    pauseMenuCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, smooth);
                }

                if (pauseBlurVolume != null)
                {
                    pauseBlurVolume.weight = Mathf.Lerp(startBlur, targetBlur, smooth);
                }

                if (pauseModalCardRect != null)
                {
                    float scale = opening
                        ? Mathf.Lerp(0.92f, 1.0f, smooth)
                        : Mathf.Lerp(1.0f, 0.94f, smooth);
                    pauseModalCardRect.localScale = new Vector3(scale, scale, 1f);
                }

                yield return null;
            }

            if (pauseMenuCanvasGroup != null)
            {
                pauseMenuCanvasGroup.alpha = targetAlpha;
            }
            if (pauseBlurVolume != null)
            {
                pauseBlurVolume.weight = targetBlur;
            }
            if (pauseModalCardRect != null)
            {
                pauseModalCardRect.localScale = Vector3.one;
            }

            if (!opening)
            {
                SetActiveSafe(pauseMenuPanel, false);
            }

            pauseTransitionCoroutine = null;
        }

        // ------------------------------------------------------------------
        //  5. Night Completion Transition
        // ------------------------------------------------------------------
        /// <summary>
        /// End-of-night trigger that smoothly transitions out of gameplay, increments the night counter,
        /// and loads the next night teaser screen (e.g., "Night Two, keep going") from the NightConfigData asset.
        /// </summary>
        [ContextMenu("Trigger Night Completion Transition")]
        public void CompleteCurrentNight()
        {
            if (CurrentState == GameLoopState.NightTransition || CurrentState == GameLoopState.NightTeaser)
                return;

            if (CurrentState == GameLoopState.Paused)
            {
                Time.timeScale = 1f;
                SetActiveSafe(pauseMenuPanel, false);
                CloseSubPanelsImmediate();
                if (pauseBlurVolume != null) pauseBlurVolume.weight = 0f;
            }

            if (activeFlowCoroutine != null) StopCoroutine(activeFlowCoroutine);
            activeFlowCoroutine = StartCoroutine(NightCompletionTransitionRoutine());
        }

        private IEnumerator NightCompletionTransitionRoutine()
        {
            int completedNightNumber = CurrentNightNumber;
            SetState(GameLoopState.NightTransition);

            SetPlayerControlsEnabled(false);
            if (gameDirector != null)
            {
                gameDirector.SetHudVisible(false);
            }
            SetActiveSafe(inGameHudPanel, false);

            onNightCompleted?.Invoke(completedNightNumber);
            Debug.Log($"[GameLoopManager] Night {completedNightNumber} completed. Transitioning to Night {completedNightNumber + 1} Teaser...");

            if (screenFadeCanvasGroup != null)
            {
                screenFadeCanvasGroup.blocksRaycasts = true;
                yield return FadeCanvasGroupUnscaled(screenFadeCanvasGroup, screenFadeCanvasGroup.alpha, 1f, teaserFadeInDuration);
            }

            CurrentNightIndex++;

            yield return ShowNightTeaserRoutine(CurrentNightIndex);
            yield return TransitionToDeskGameplayRoutine(CurrentNightIndex);

            activeFlowCoroutine = null;
        }

        // ------------------------------------------------------------------
        //  Settings Callbacks
        // ------------------------------------------------------------------
        private void OnMasterVolumeChanged(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
        }

        private void OnSensitivityChanged(float value)
        {
            if (playerTransform == null) return;
            var camCtrl = playerTransform.GetComponent<AZE.AdvancedFirstPerson.PlayerCameraController>();
            if (camCtrl != null)
            {
                camCtrl.SensitivityX = value;
                camCtrl.SensitivityY = value * 0.75f;
            }
            var legacyMove = playerTransform.GetComponent<movement>();
            if (legacyMove != null)
            {
                legacyMove.sensitivity = Mathf.Max(0.2f, value * 0.1f);
            }
        }

        private void OnDoorHighlightToggled(bool enabled)
        {
            DoorInteractionController.SetGlobalHighlight(enabled);
            HanUIAudioManager.PlayClick();
        }

        // ------------------------------------------------------------------
        //  Internal Helpers
        // ------------------------------------------------------------------
        private NightConfigEntry GetNightEntrySafe(int nightIndex)
        {
            if (nightConfigData == null)
            {
                nightConfigData = Resources.Load<NightConfigData>("NightConfigData");
                if (nightConfigData == null)
                {
                    nightConfigData = ScriptableObject.CreateInstance<NightConfigData>();
                    nightConfigData.EnsureDefaultEntries();
                }
            }
            return nightConfigData.GetNightByIndex(nightIndex);
        }

        private void EnsureReferences()
        {
            if (gameDirector == null)
                gameDirector = FindFirstObjectByType<HanGameDirector>(FindObjectsInactive.Include);

            if (playerTransform == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null) playerTransform = playerObj.transform;
            }

            mainCamera = Camera.main;
            if (mainCamera != null)
            {
                initialCameraFov = mainCamera.fieldOfView;
            }

            if (nightConfigData == null)
            {
                nightConfigData = Resources.Load<NightConfigData>("NightConfigData");
            }

            if (uiAudioSource == null)
            {
                uiAudioSource = GetComponent<AudioSource>();
            }

            if (uiAudioManager == null)
            {
                uiAudioManager = GetComponent<HanUIAudioManager>();
                if (uiAudioManager == null)
                    uiAudioManager = HanUIAudioManager.EnsureInstance();
            }

            if (focusNavigator == null)
            {
                focusNavigator = GetComponent<HanUIFocusNavigator>();
                if (focusNavigator == null)
                    focusNavigator = gameObject.AddComponent<HanUIFocusNavigator>();
            }
        }

        private void BindUIButtons()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayButtonClicked);
                playButton.onClick.AddListener(OnPlayButtonClicked);
            }
            if (settingsButton != null)
            {
                settingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
                settingsButton.onClick.AddListener(OnSettingsButtonClicked);
            }
            if (creditsButton != null)
            {
                creditsButton.onClick.RemoveListener(OnCreditsButtonClicked);
                creditsButton.onClick.AddListener(OnCreditsButtonClicked);
            }
            if (exitButton != null)
            {
                exitButton.onClick.RemoveListener(OnExitButtonClicked);
                exitButton.onClick.AddListener(OnExitButtonClicked);
            }
            if (settingsBackButton != null)
            {
                settingsBackButton.onClick.RemoveListener(CloseSubPanels);
                settingsBackButton.onClick.AddListener(CloseSubPanels);
            }
            if (creditsBackButton != null)
            {
                creditsBackButton.onClick.RemoveListener(CloseSubPanels);
                creditsBackButton.onClick.AddListener(CloseSubPanels);
            }
            if (skipVideoButton != null)
            {
                skipVideoButton.onClick.RemoveListener(SkipIntroVideo);
                skipVideoButton.onClick.AddListener(SkipIntroVideo);
            }
            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(ResumeGame);
                resumeButton.onClick.AddListener(ResumeGame);
            }
            if (pauseSettingsButton != null)
            {
                pauseSettingsButton.onClick.RemoveListener(OnSettingsButtonClicked);
                pauseSettingsButton.onClick.AddListener(OnSettingsButtonClicked);
            }
            if (pauseExitButton != null)
            {
                pauseExitButton.onClick.RemoveListener(OnExitButtonClicked);
                pauseExitButton.onClick.AddListener(OnExitButtonClicked);
            }

            if (masterVolumeSlider != null)
            {
                masterVolumeSlider.SetValueWithoutNotify(AudioListener.volume);
                masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
                masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            }
            if (lookSensitivitySlider != null)
            {
                lookSensitivitySlider.onValueChanged.RemoveListener(OnSensitivityChanged);
                lookSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
            }
            if (doorHighlightToggle != null)
            {
                doorHighlightToggle.SetIsOnWithoutNotify(DoorInteractionController.GlobalHighlightEnabled);
                doorHighlightToggle.onValueChanged.RemoveListener(OnDoorHighlightToggled);
                doorHighlightToggle.onValueChanged.AddListener(OnDoorHighlightToggled);
            }
        }

        private void ConfigureExplicitKeyboardNavigation()
        {
            var mainMenuButtons = new List<Button>();
            if (playButton != null) mainMenuButtons.Add(playButton);
            if (settingsButton != null) mainMenuButtons.Add(settingsButton);
            if (creditsButton != null) mainMenuButtons.Add(creditsButton);
            if (exitButton != null) mainMenuButtons.Add(exitButton);
            HanUIFocusNavigator.ConfigureVerticalNavigationLoop(mainMenuButtons);

            var pauseButtons = new List<Button>();
            if (resumeButton != null) pauseButtons.Add(resumeButton);
            if (pauseSettingsButton != null) pauseButtons.Add(pauseSettingsButton);
            if (pauseExitButton != null) pauseButtons.Add(pauseExitButton);
            HanUIFocusNavigator.ConfigureVerticalNavigationLoop(pauseButtons);

            if (focusNavigator != null)
            {
                focusNavigator.ConfigureDefaults(
                    playButton,
                    resumeButton,
                    settingsBackButton,
                    creditsBackButton,
                    skipVideoButton);
            }
        }

        private void SetupVideoRenderTarget()
        {
            if (introVideoPlayer == null) return;

            if (introVideoRawImage != null)
            {
                if (videoRenderTexture == null)
                {
                    videoRenderTexture = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                    videoRenderTexture.name = "IntroVideo_RT";
                    videoRenderTexture.Create();
                }
                introVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
                introVideoPlayer.targetTexture = videoRenderTexture;
                introVideoRawImage.texture = videoRenderTexture;
                introVideoRawImage.color = Color.white;
            }
            else if (mainCamera != null && introVideoPlayer.renderMode == VideoRenderMode.CameraNearPlane)
            {
                introVideoPlayer.targetCamera = mainCamera;
            }
        }

        private void CachePlayerControllers()
        {
            playerControlBehaviours.Clear();
            if (playerTransform == null) return;

            var monoBehaviours = playerTransform.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < monoBehaviours.Length; i++)
            {
                var mb = monoBehaviours[i];
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName == "PlayerMovementStateMachine" ||
                    typeName == "PlayerCameraController" ||
                    typeName == "PlayerInputHandler" ||
                    typeName == "CinemachineInputAxisController" ||
                    typeName == "DoorInteractionController" ||
                    typeName == "movement")
                {
                    playerControlBehaviours.Add(mb);
                }
            }
        }

        private void SetPlayerControlsEnabled(bool enabled)
        {
            if (playerControlBehaviours.Count == 0) CachePlayerControllers();
            for (int i = 0; i < playerControlBehaviours.Count; i++)
            {
                if (playerControlBehaviours[i] != null)
                    playerControlBehaviours[i].enabled = enabled;
            }
        }

        private void SaveAndDisablePlayerControlsForPause()
        {
            if (playerControlBehaviours.Count == 0) CachePlayerControllers();
            prePauseBehaviourStates.Clear();
            for (int i = 0; i < playerControlBehaviours.Count; i++)
            {
                var b = playerControlBehaviours[i];
                if (b != null)
                {
                    prePauseBehaviourStates[b] = b.enabled;
                    b.enabled = false;
                }
            }
        }

        private void RestorePlayerControlsAfterPause()
        {
            foreach (var kvp in prePauseBehaviourStates)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.enabled = kvp.Value;
                }
            }
            prePauseBehaviourStates.Clear();
        }

        private static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private static void LockCursorForGameplay()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void SetState(GameLoopState nextState)
        {
            CurrentState = nextState;
            OnStateChanged?.Invoke(nextState);
        }

        private static void SetActiveSafe(GameObject obj, bool active)
        {
            if (obj != null && obj.activeSelf != active)
                obj.SetActive(active);
        }

        private static IEnumerator FadeCanvasGroupUnscaled(CanvasGroup cg, float from, float to, float duration)
        {
            if (cg == null) yield break;
            if (duration <= 0.01f)
            {
                cg.alpha = to;
                yield break;
            }

            float elapsed = 0f;
            cg.alpha = from;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                cg.alpha = Mathf.SmoothStep(from, to, t);
                yield return null;
            }
            cg.alpha = to;
        }

        private bool WasPauseOrCancelPressed()
        {
            if (Keyboard.current != null && pauseKey == KeyCode.Escape && Keyboard.current.escapeKey.wasPressedThisFrame)
                return true;

            if (Gamepad.current != null && (Gamepad.current.startButton.wasPressedThisFrame || Gamepad.current.buttonEast.wasPressedThisFrame))
                return true;

            try
            {
                if (Input.GetKeyDown(pauseKey)) return true;
            }
            catch { }

            return false;
        }

        private static bool WasSkipKeyPressed()
        {
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame ||
                    Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                return true;

            try
            {
                if (Input.GetKeyDown(KeyCode.Space) ||
                    Input.GetKeyDown(KeyCode.Return) ||
                    Input.GetKeyDown(KeyCode.Escape))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }
    }
}
