using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using AZE.AdvancedFirstPerson;

namespace Han.CheckIn
{
    public enum DeskFocusMode
    {
        CharacterFocus, // Focus strictly on traveler (book resting on counter, traveler fully visible)
        BookFocus       // Focus strictly on ledger (ledger raised into center reading view)
    }

    /// <summary>
    /// CheckInBookViewController
    /// Controls raising and lowering the 3D check-in register book viewmodel when the player looks down.
    /// Supports:
    /// - Pitch detection with angle hysteresis (avoids boundary chatter).
    /// - Manual toggle key (Tab / B) for quick testing and accessibility.
    /// - Smooth 3D Viewmodel Transform interpolation AND/OR UI RectTransform.
    /// - Procedural 3D micro-sway / breathing bob (Doom 64 / Ultrakill feel).
    /// - Diegetic inspection light fading (so pages remain legible in deep Transylvanian midnight).
    /// </summary>
    public class CheckInBookViewController : MonoBehaviour
    {
        [Header("Camera & Tracking")]
        [Tooltip("The camera transform to read pitch from. If null, auto-finds Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Hysteresis Angle Thresholds")]
        [Tooltip("Downward pitch angle (degrees) to raise book. Unity pitch: > 0 is looking down.")]
        [Range(25f, 75f)]
        [SerializeField] private float enterPitchAngle = 45f;

        [Tooltip("Downward pitch angle (degrees) below which book is lowered. Must be lower than enter angle.")]
        [Range(10f, 50f)]
        [SerializeField] private float exitPitchAngle = 30f;

        [Header("Manual Key Toggle (Convenience / Testing)")]
        [Tooltip("Optional keyboard key to toggle the book regardless of pitch angle.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        [Header("3D Viewmodel Transform Targets")]
        [Tooltip("Root Transform of the 3D Book Viewmodel.")]
        [SerializeField] private Transform bookViewmodelTransform;

        [Tooltip("Local position relative to camera when fully raised in reading view (framed so entire book is visible).")]
        [SerializeField] private Vector3 raisedLocalPosition = new Vector3(0f, -0.015f, 0.43f);

        [Tooltip("Local position relative to camera when lowered out of view.")]
        [SerializeField] private Vector3 loweredLocalPosition = new Vector3(0f, -0.90f, 0.38f);

        [Tooltip("Local rotation (Euler angles) relative to camera when raised.")]
        [SerializeField] private Vector3 raisedLocalEuler = new Vector3(8f, 0f, 0f);

        [Tooltip("Local rotation (Euler angles) relative to camera when lowered.")]
        [SerializeField] private Vector3 loweredLocalEuler = new Vector3(60f, 0f, 0f);

        [Header("Desk Mode Poses (Strict Character vs Book Focus)")]
        [Tooltip("Local position relative to camera when lowered (looking forward at the traveler). Keep low so the whole NPC is visible.")]
        [SerializeField] private Vector3 deskRestPosition = new Vector3(0f, -0.55f, 0.45f);
        [SerializeField] private Vector3 deskRestEuler = new Vector3(35f, 0f, 0f);

        [Tooltip("Camera pitch (degrees down) at which the book starts rising.")]
        [SerializeField] private float deskBookStartPitch = 5f;
        [Tooltip("Camera pitch (degrees down) at which the whole book is fully in view.")]
        [SerializeField] private float deskBookFullPitch = 20f;
        [Tooltip("Seconds for the book to follow the camera pitch.")]
        [SerializeField] private float deskBookFollowTime = 0.25f;

        [Tooltip("Key to toggle focus between traveler and book during check-in.")]
        [SerializeField] private KeyCode toggleFocusKey = KeyCode.Q;

        [Header("StudiosAZE Movement & Camera Integration")]
        [Tooltip("StudiosAZE PlayerMovementStateMachine to block all movement (WASD, sprint, jump, crouch) when reading the book.")]
        [SerializeField] private PlayerMovementStateMachine azeMovement;
        [Tooltip("StudiosAZE PlayerCameraController to constrain or lock free look while reading.")]
        [SerializeField] private PlayerCameraController azeCamera;

        [Header("Look Down Pop-up Prompt (Tab to see book)")]
        [Tooltip("Shows a subtle pop-up to press Tab when looking down towards the register/counter.")]
        [SerializeField] private bool showPromptOnLookDown = true;
        [Tooltip("Pitch angle (degrees down) required to trigger the subtle Tab prompt.")]
        [Range(15f, 60f)]
        [SerializeField] private float lookDownPromptPitch = 22f;
        [Tooltip("Pitch angle (degrees down) below which the subtle prompt fades away.")]
        [Range(5f, 40f)]
        [SerializeField] private float lookDownPromptExitPitch = 14f;
        [Tooltip("If true, looking down automatically raises the book without pressing Tab. Set to false so Tab is required.")]
        [SerializeField] private bool autoRaiseOnLookDown = false;
        [Tooltip("If true, looking back up towards the horizon gently lowers the book.")]
        [SerializeField] private bool closeOnLookUp = false;

        [Header("Pop-up UI Elements")]
        [SerializeField] private GameObject lookDownPromptUI;
        [SerializeField] private CanvasGroup lookDownPromptCanvasGroup;
        [SerializeField] private RectTransform lookDownPromptRect;
        [SerializeField] private TMP_Text lookDownPromptLabelText;
        [SerializeField] private string promptMessage = "Check-in Book";

        [Header("Player Movement & Look Containment")]
        [Tooltip("Disables player walking, sprinting, and jumping when reading the book.")]
        [SerializeField] private bool disableMovementWhileReading = true;
        [SerializeField] private movement playerMovement;

        [Header("Contained Screen Inspection Clamps")]
        [Tooltip("Allows subtle mouse look while reading but keeps the screen strictly contained on the book.")]
        [SerializeField] private bool containScreenLook = true;
        [SerializeField] private float maxInspectionYawDelta = 14f;
        [SerializeField] private float minInspectionPitch = 38f;
        [SerializeField] private float maxInspectionPitch = 65f;

        [Header("2D UI Fallback (Optional)")]
        [Tooltip("Optional 2D RectTransform if using screen-space UI mode.")]
        [SerializeField] private RectTransform bookRectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Vector2 uiRaisedAnchoredPos = new Vector2(0f, -40f);
        [SerializeField] private Vector2 uiLoweredAnchoredPos = new Vector2(0f, -800f);

        [Header("Inspection Light (Diegetic Illumination)")]
        [Tooltip("Subtle warm point light attached to the viewmodel to illuminate pages in pitch darkness.")]
        [SerializeField] private Light inspectionLight;
        [SerializeField] private float maxInspectionLightIntensity = 0.22f;

        [Header("Animation & Transitions")]
        [Tooltip("Duration in seconds for the raising/lowering transition.")]
        [Range(0.1f, 1.2f)]
        [SerializeField] private float transitionDuration = 0.30f;

        [Tooltip("Animation curve for raising/lowering interpolation.")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Procedural 3D Micro-Sway (Doom / Retro Juiciness)")]
        [Tooltip("Enable subtle breathing / idle bobbing when holding the book.")]
        [SerializeField] private bool enableProceduralSway = true;
        [Tooltip("Adds organic Perlin noise to the book sway for natural, non-repeating hand tremors.")]
        [SerializeField] private bool useNoiseForSway = true;
        [Range(0f, 1f)] [SerializeField] private float swayNoiseInfluence = 0.35f;
        [SerializeField] private float swayFrequency = 1.7f;
        [SerializeField] private float swayPosAmplitude = 0.005f;
        [SerializeField] private float swayRotAmplitude = 1.2f;


        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip bookRaiseSfx;
        [SerializeField] private AudioClip bookLowerSfx;

        [Header("Data & Page Navigation Link")]
        [SerializeField] private GuestBookUI guestBookUI;
        [SerializeField] private FirstPersonHandsController handsController;

        [Header("Events")]
        public UnityEvent onBookOpened;
        public UnityEvent onBookClosed;
        public Action<bool> OnBookStateChanged;

        // Runtime State
        private bool isBookOpen = false;
        private bool isKeyToggled = false;
        private bool isForceOpen = false;
        private bool isInDeskMode = false;
        private bool isCinematicDriven = false;
        private DeskFocusMode currentFocusMode = DeskFocusMode.CharacterFocus;
        private float deskFocusProgress = 0f; // 0 = Character, 1 = Book
        private float currentTransitionProgress = 0f; // 0 = lowered, 1 = raised
        private float currentPitch = 0f;
        private float swayTimer = 0f;
        private bool wasIdleAtZero = false;
        private float bookImpulseOffset = 0f;
        private float bookImpulseVelocity = 0f;

        public bool IsBookOpen => isBookOpen;
        public float CurrentPitch => currentPitch;
        public float TransitionProgress => currentTransitionProgress;
        public DeskFocusMode FocusMode => currentFocusMode;
        public bool IsInDeskMode => isInDeskMode;

        /// <summary>
        /// Applies a tactile physical recoil impulse to the 3D book when a photo or stamp hits the page.
        /// </summary>
        public void TriggerBookImpulse(float strength = 0.5f)
        {
            bookImpulseVelocity += Mathf.Clamp(strength, 0.1f, 2f) * 0.12f;
        }

        /// <summary>
        /// Immediately cuts/transitions the 3D check-in book into close-up reading focus.
        /// </summary>
        public void CutToBookFocus()
        {
            isInDeskMode = true;
            isCinematicDriven = true;
            currentFocusMode = DeskFocusMode.BookFocus;
            deskFocusProgress = 1f;
            currentTransitionProgress = 1f;
            SetBookState(true);
            ApplyPose(1f);
        }

        /// <summary>
        /// Updates the blend between BookFocus (1) and CharacterFocus (0) during a cinematic camera pan.
        /// </summary>
        public void SetCinematicDeskBlend(float bookFocusFactor01)
        {
            isInDeskMode = true;
            isCinematicDriven = true;
            deskFocusProgress = Mathf.Clamp01(bookFocusFactor01);
            currentTransitionProgress = 1f;
            ApplyPose(1f);
        }

        /// <summary>
        /// Releases cinematic override while keeping the book resting on the desk during check-in dialogue.
        /// </summary>
        public void ReleaseCinematicToDeskRest()
        {
            isCinematicDriven = false;
            currentFocusMode = DeskFocusMode.CharacterFocus;
            deskFocusProgress = 0f;
            ApplyPose(1f);
        }

        /// <summary>
        /// Activates or deactivates the desk check-in inspection mode.
        /// </summary>
        public void SetDeskMode(bool active, DeskFocusMode initialFocus = DeskFocusMode.CharacterFocus)
        {
            isInDeskMode = active;
            if (!active) isCinematicDriven = false;
            currentFocusMode = initialFocus;
            deskFocusProgress = initialFocus == DeskFocusMode.BookFocus ? 1f : 0f;

            if (active)
            {
                SetBookState(true);
            }
            else
            {
                SetBookState(false);
            }

            ApplyFocusToPlayerMovement();
        }

        public void ToggleDeskFocus()
        {
            if (!isInDeskMode) return;
            SetDeskFocus(currentFocusMode == DeskFocusMode.CharacterFocus ? DeskFocusMode.BookFocus : DeskFocusMode.CharacterFocus);
        }

        public void SetDeskFocus(DeskFocusMode mode)
        {
            currentFocusMode = mode;
            ApplyFocusToPlayerMovement();
        }

        private Transform firstPersonCamTransform;

        private void EnsurePlayerControllers()
        {
            if (azeMovement == null)
            {
                if (cameraTransform != null) azeMovement = cameraTransform.GetComponentInParent<PlayerMovementStateMachine>();
                if (azeMovement == null) azeMovement = UnityEngine.Object.FindFirstObjectByType<PlayerMovementStateMachine>();
            }

            if (azeCamera == null)
            {
                if (cameraTransform != null) azeCamera = cameraTransform.GetComponentInParent<PlayerCameraController>();
                if (azeCamera == null) azeCamera = UnityEngine.Object.FindFirstObjectByType<PlayerCameraController>();
            }

            if (playerMovement == null)
            {
                if (cameraTransform != null) playerMovement = cameraTransform.GetComponentInParent<movement>();
                if (playerMovement == null) playerMovement = UnityEngine.Object.FindFirstObjectByType<movement>();
            }
        }

        private void ApplyFocusToPlayerMovement()
        {
            EnsurePlayerControllers();

            if (azeMovement != null && isInDeskMode)
            {
                azeMovement.SetMovementLocked(true);
            }

            if (playerMovement != null)
            {
                playerMovement.isDeskMode = isInDeskMode;
                if (isInDeskMode)
                {
                    playerMovement.deskTargetYaw = 180f;
                    // Glide toward the chosen focus; the player can then freely pitch the view.
                    playerMovement.deskSnapPitch = currentFocusMode == DeskFocusMode.CharacterFocus ? 2f : deskBookFullPitch;
                    playerMovement.deskSnapActive = true;
                }
            }

            if (firstPersonCamTransform == null)
            {
                var fp = GameObject.Find("Player/FirstPersonCam");
                if (fp != null) firstPersonCamTransform = fp.transform;
            }
        }

        private void Awake()
        {
            FindCameraReference();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (bookViewmodelTransform == null && bookRectTransform == null)
                bookViewmodelTransform = transform;

            if (guestBookUI == null)
                guestBookUI = GetComponentInChildren<GuestBookUI>();

            if (handsController == null)
            {
                handsController = GetComponent<FirstPersonHandsController>();
                if (handsController == null)
                    handsController = gameObject.AddComponent<FirstPersonHandsController>();
            }

            // Ensure safe near-clip plane on both Main Camera and BookOverlayCamera so close-up framing never clips
            Camera overlayCam = transform.parent != null ? transform.parent.Find("BookOverlayCamera")?.GetComponent<Camera>() : null;
            if (overlayCam == null && Camera.main != null)
                overlayCam = Camera.main.transform.Find("BookOverlayCamera")?.GetComponent<Camera>();
            if (overlayCam != null)
                overlayCam.nearClipPlane = 0.01f;
            if (Camera.main != null && Camera.main.nearClipPlane > 0.02f)
                Camera.main.nearClipPlane = 0.01f;

            Canvas canvas = GetComponentInChildren<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                if (overlayCam != null)
                    canvas.worldCamera = overlayCam;
            }

            if (inspectionLight == null)
                inspectionLight = GetComponentInChildren<Light>();

            // Clamp inspection light intensity so legacy serialized values never blow out parchment
            maxInspectionLightIntensity = Mathf.Clamp(maxInspectionLightIntensity, 0.05f, 0.24f);

            if (exitPitchAngle >= enterPitchAngle)
                exitPitchAngle = enterPitchAngle - 10f;
        }

        private void FindCameraReference()
        {
            if (cameraTransform == null)
            {
                if (transform.parent != null && transform.parent.GetComponent<Camera>() != null)
                    cameraTransform = transform.parent;
                else if (Camera.main != null)
                    cameraTransform = Camera.main.transform;
            }
        }

        private void Start()
        {
            FindCameraReference();
            currentTransitionProgress = 0f;
            ApplyPose(0f);
        }

        private float promptAlpha = 0f;
        private bool isLookingDownForPrompt = false;

        private void EnsureLookDownPromptUI()
        {
            if (lookDownPromptUI != null && lookDownPromptCanvasGroup != null) return;

            var existing = GameObject.Find("CheckInBookPromptPill");
            if (existing != null)
            {
                lookDownPromptUI = existing;
                lookDownPromptCanvasGroup = existing.GetComponent<CanvasGroup>();
                lookDownPromptRect = existing.GetComponent<RectTransform>();
                var promptTransform = existing.transform.Find("PromptLabel");
                lookDownPromptLabelText = promptTransform != null
                    ? promptTransform.GetComponent<TMP_Text>()
                    : existing.GetComponentInChildren<TMP_Text>();
                ApplyPromptLabelSettings();
                return;
            }

            Canvas parentCanvas = null;
            var inGameHud = GameObject.Find("InGameHudPanel");
            if (inGameHud != null) parentCanvas = inGameHud.GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < canvases.Length; i++)
                {
                    if (canvases[i].renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        parentCanvas = canvases[i];
                        break;
                    }
                }
            }

            if (parentCanvas == null)
            {
                GameObject canvasObj = new GameObject("CheckInBookPromptCanvas");
                parentCanvas = canvasObj.AddComponent<Canvas>();
                parentCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                parentCanvas.sortingOrder = 90;
                var scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            TMP_FontAsset font = HanUITheme.GetProjectFont();

            GameObject pillObj = new GameObject("CheckInBookPromptPill");
            pillObj.transform.SetParent(parentCanvas.transform, false);
            lookDownPromptUI = pillObj;

            lookDownPromptRect = pillObj.AddComponent<RectTransform>();
            lookDownPromptRect.anchorMin = new Vector2(0.5f, 0f);
            lookDownPromptRect.anchorMax = new Vector2(0.5f, 0f);
            lookDownPromptRect.pivot = new Vector2(0.5f, 0f);
            lookDownPromptRect.anchoredPosition = new Vector2(0f, 54f);
            lookDownPromptRect.sizeDelta = new Vector2(276f, 42f);

            lookDownPromptCanvasGroup = pillObj.AddComponent<CanvasGroup>();
            lookDownPromptCanvasGroup.alpha = 0f;
            lookDownPromptCanvasGroup.interactable = false;
            lookDownPromptCanvasGroup.blocksRaycasts = false;

            Image bgImg = pillObj.AddComponent<Image>();
            bgImg.sprite = HanUITheme.GetButtonPlateSprite();
            bgImg.type = Image.Type.Sliced;
            bgImg.color = new Color(HanUITheme.DeepSepiaInk.r, HanUITheme.DeepSepiaInk.g, HanUITheme.DeepSepiaInk.b, 0.90f);
            bgImg.raycastTarget = false;

            Outline outline = pillObj.AddComponent<Outline>();
            outline.effectColor = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.65f);
            outline.effectDistance = new Vector2(1f, -1f);

            Shadow shadow = pillObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.04f, 0.03f, 0.02f, 0.85f);
            shadow.effectDistance = new Vector2(2f, -3f);

            GameObject badgeObj = new GameObject("TabBadge");
            badgeObj.transform.SetParent(pillObj.transform, false);
            RectTransform badgeRect = badgeObj.AddComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(10f, 0f);
            badgeRect.sizeDelta = new Vector2(50f, 26f);

            Image badgeImg = badgeObj.AddComponent<Image>();
            badgeImg.sprite = HanUITheme.GetButtonPlateSprite();
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = HanUITheme.MutedSlate;
            badgeImg.raycastTarget = false;

            Outline badgeOutline = badgeObj.AddComponent<Outline>();
            badgeOutline.effectColor = HanUITheme.BurnishedGold;
            badgeOutline.effectDistance = new Vector2(1f, -1f);

            GameObject badgeTextObj = new GameObject("TabText");
            badgeTextObj.transform.SetParent(badgeObj.transform, false);
            RectTransform badgeTextRect = badgeTextObj.AddComponent<RectTransform>();
            badgeTextRect.anchorMin = Vector2.zero;
            badgeTextRect.anchorMax = Vector2.one;
            badgeTextRect.offsetMin = Vector2.zero;
            badgeTextRect.offsetMax = Vector2.zero;

            TMP_Text badgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(badgeText, font);
            badgeText.text = "TAB";
            badgeText.fontSize = 13.5f;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.color = HanUITheme.BurnishedGold;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.raycastTarget = false;

            GameObject labelObj = new GameObject("PromptLabel");
            labelObj.transform.SetParent(pillObj.transform, false);
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.offsetMin = new Vector2(68f, 2f);
            labelRect.offsetMax = new Vector2(-12f, -2f);

            lookDownPromptLabelText = labelObj.AddComponent<TextMeshProUGUI>();
            ApplyPromptLabelSettings();
        }

        private void ApplyPromptLabelSettings()
        {
            if (lookDownPromptLabelText == null) return;
            TMP_FontAsset font = HanUITheme.GetProjectFont();
            HanUITheme.ApplyCrispSDF(lookDownPromptLabelText, font);
            lookDownPromptLabelText.text = HanLocalization.Get("book.prompt_open");
            lookDownPromptLabelText.fontSize = 15f;
            lookDownPromptLabelText.enableAutoSizing = true;
            lookDownPromptLabelText.fontSizeMin = 10f;
            lookDownPromptLabelText.fontSizeMax = 15f;
            lookDownPromptLabelText.color = HanUITheme.WarmParchment;
            lookDownPromptLabelText.alignment = TextAlignmentOptions.Left;
            lookDownPromptLabelText.raycastTarget = false;
        }

        private void HandleLanguageChanged(HanLanguage _)
        {
            if (lookDownPromptLabelText != null)
            {
                lookDownPromptLabelText.text = HanLocalization.Get("book.prompt_open");
            }
        }

        private void UpdateLookDownPrompt()
        {
            if (!showPromptOnLookDown)
            {
                if (lookDownPromptCanvasGroup != null) lookDownPromptCanvasGroup.alpha = 0f;
                return;
            }

            EnsureLookDownPromptUI();
            if (lookDownPromptCanvasGroup == null) return;

            bool gameplayActive = (GameLoopManager.Instance == null || GameLoopManager.Instance.IsGameplayActive) && Time.timeScale > 0f;

            if (!isLookingDownForPrompt)
            {
                if (currentPitch >= lookDownPromptPitch)
                    isLookingDownForPrompt = true;
            }
            else
            {
                if (currentPitch <= lookDownPromptExitPitch)
                    isLookingDownForPrompt = false;
            }

            bool shouldShow = gameplayActive && !isBookOpen && isLookingDownForPrompt;
            float targetAlpha = shouldShow ? 1f : 0f;

            promptAlpha = Mathf.MoveTowards(promptAlpha, targetAlpha, Time.unscaledDeltaTime * 6f);
            lookDownPromptCanvasGroup.alpha = promptAlpha;

            if (lookDownPromptRect != null)
            {
                float targetY = Mathf.Lerp(52f, 68f, promptAlpha);
                lookDownPromptRect.anchoredPosition = new Vector2(0f, targetY);
            }

            if (lookDownPromptUI != null)
            {
                lookDownPromptUI.SetActive(promptAlpha > 0.005f);
            }
        }

        private void Update()
        {
            if (Time.timeScale == 0f || (GameLoopManager.Instance != null && !GameLoopManager.Instance.IsGameplayActive))
                return;

            UpdateCameraPitch();
            UpdateLookDownPrompt();

            if (isInDeskMode && !isCinematicDriven)
            {
                // Right click or Q toggles focus between traveler and ledger
                bool rightClick = (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
                try { if (Input.GetMouseButtonDown(1)) rightClick = true; } catch { }
                bool qKey = (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame);
                try { if (Input.GetKeyDown(toggleFocusKey)) qKey = true; } catch { }

                if (rightClick || qKey)
                {
                    ToggleDeskFocus();
                }

                // Newspaper behaviour: book is lowered when looking forward at the NPC and rises
                // smoothly into view as the player looks down.
                float targetFocus = Mathf.InverseLerp(deskBookStartPitch, deskBookFullPitch, currentPitch);
                deskFocusProgress = Mathf.MoveTowards(deskFocusProgress, targetFocus, Time.deltaTime / Mathf.Max(0.01f, deskBookFollowTime));
                currentFocusMode = deskFocusProgress > 0.5f ? DeskFocusMode.BookFocus : DeskFocusMode.CharacterFocus;
            }

            HandleManualToggle();
            EvaluatePitchActivation();
            UpdateTransition();
            HandlePageInputs();
        }

        /// <summary>
        /// Reads camera pitch and converts Unity's [0, 360) Euler representation to signed [-180, +180].
        /// > 0 = Looking Down (towards ground)
        /// < 0 = Looking Up (towards sky)
        /// </summary>
        private void UpdateCameraPitch()
        {
            if (cameraTransform == null)
            {
                FindCameraReference();
                if (cameraTransform == null) return;
            }

            float rawPitch = cameraTransform.localEulerAngles.x;

            if (rawPitch > 180f)
                rawPitch -= 360f;

            currentPitch = rawPitch;
        }

        private void HandleManualToggle()
        {
            bool toggled = false;
            if (Keyboard.current != null)
            {
                if (toggleKey == KeyCode.Tab && Keyboard.current.tabKey.wasPressedThisFrame)
                    toggled = true;
                else if (toggleKey == KeyCode.B && Keyboard.current.bKey.wasPressedThisFrame)
                    toggled = true;
                else if (isBookOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
                    toggled = true;
            }
            if (!toggled && toggleKey != KeyCode.None)
            {
                try
                {
                    if (Input.GetKeyDown(toggleKey) || (isBookOpen && Input.GetKeyDown(KeyCode.Escape))) toggled = true;
                }
                catch { }
            }

            if (toggled)
            {
                if (isInDeskMode)
                {
                    ToggleDeskFocus();
                }
                else
                {
                    isKeyToggled = !isBookOpen;
                    SetBookState(isKeyToggled);
                }
            }
        }

        /// <summary>
        /// Evaluates hysteresis thresholds to switch open/closed states based on look-down angle.
        /// When autoRaiseOnLookDown is false, looking down only displays the subtle prompt to press Tab.
        /// </summary>
        private void EvaluatePitchActivation()
        {
            if (isKeyToggled || isForceOpen || isInDeskMode) return;

            if (autoRaiseOnLookDown)
            {
                if (!isBookOpen)
                {
                    if (currentPitch >= enterPitchAngle)
                    {
                        SetBookState(true);
                    }
                }
                else
                {
                    if (currentPitch <= exitPitchAngle)
                    {
                        SetBookState(false);
                    }
                }
            }
            else if (closeOnLookUp && isBookOpen)
            {
                if (currentPitch <= exitPitchAngle)
                {
                    SetBookState(false);
                }
            }
        }

        /// <summary>
        /// Programmatically forces the book open (e.g. during check-in desk inspection) ignoring camera pitch.
        /// Call with false to release lock and allow normal look-down/key controls.
        /// </summary>
        public void SetForceOpen(bool force)
        {
            isForceOpen = force;
            SetBookState(force);
        }

        public void SetBookState(bool open)
        {
            if (!open) isForceOpen = false;
            if (isBookOpen == open) return;

            isBookOpen = open;

            EnsurePlayerControllers();

            // 1. Block/Restore StudiosAZE movement
            if (azeMovement != null)
            {
                if (disableMovementWhileReading)
                {
                    azeMovement.SetMovementLocked(open);
                }
            }

            // 2. Lock/Unlock free look camera while reading if containScreenLook is true
            if (azeCamera != null && containScreenLook)
            {
                azeCamera.SetFreeLookLocked(open);
            }

            // 3. Fallback support for legacy movement.cs
            if (playerMovement != null)
            {
                if (disableMovementWhileReading)
                {
                    playerMovement.canMove = !open;
                }

                if (containScreenLook)
                {
                    playerMovement.isInspectionMode = open;
                    if (open)
                    {
                        playerMovement.inspectionCenterYaw = playerMovement.transform.eulerAngles.y;
                        playerMovement.minInspectionPitch = minInspectionPitch;
                        playerMovement.maxInspectionPitch = maxInspectionPitch;
                        playerMovement.maxInspectionYawDelta = maxInspectionYawDelta;
                    }
                }
            }
            movement.MovementLocked = disableMovementWhileReading && open;

            if (isBookOpen)
            {
                Debug.Log($"[CheckInBook] 📖 Book Raised into view! StudiosAZE movement blocked. (Pitch: {currentPitch:F1}°)");
                PlaySound(bookRaiseSfx);
                onBookOpened?.Invoke();
            }
            else
            {
                Debug.Log($"[CheckInBook] 📕 Book Lowered out of view! StudiosAZE movement restored. (Pitch: {currentPitch:F1}°)");
                isKeyToggled = false;
                PlaySound(bookLowerSfx);
                onBookClosed?.Invoke();
            }

            OnBookStateChanged?.Invoke(isBookOpen);
        }

        private void OnEnable()
        {
            HanLocalization.OnLanguageChanged += HandleLanguageChanged;
            HandleLanguageChanged(HanLocalization.CurrentLanguage);
        }

        private void OnDisable()
        {
            HanLocalization.OnLanguageChanged -= HandleLanguageChanged;
            if (isBookOpen)
            {
                SetBookState(false);
            }
            if (lookDownPromptUI != null)
            {
                lookDownPromptUI.SetActive(false);
            }
        }

        private void UpdateTransition()
        {
            float targetProgress = isBookOpen ? 1f : 0f;
            if (!isBookOpen && currentTransitionProgress <= 0f)
            {
                if (!wasIdleAtZero)
                {
                    ApplyPose(0f);
                    wasIdleAtZero = true;
                }
                return;
            }

            wasIdleAtZero = false;
            float step = Time.deltaTime / Mathf.Max(0.01f, transitionDuration);

            currentTransitionProgress = Mathf.MoveTowards(currentTransitionProgress, targetProgress, step);

            float curveT = transitionCurve.Evaluate(currentTransitionProgress);
            ApplyPose(curveT);
        }

        /// <summary>
        /// Applies interpolated position, rotation, sway, and lighting based on progress t.
        /// </summary>
        private void ApplyPose(float t)
        {
            // Update physical impact spring recoil
            if (Application.isPlaying)
            {
                float dt = Time.deltaTime;
                float springForce = -bookImpulseOffset * 130f - bookImpulseVelocity * 16f;
                bookImpulseVelocity += springForce * dt;
                bookImpulseOffset += bookImpulseVelocity * dt;
                if (Mathf.Abs(bookImpulseOffset) < 0.00002f && Mathf.Abs(bookImpulseVelocity) < 0.0002f)
                {
                    bookImpulseOffset = 0f;
                    bookImpulseVelocity = 0f;
                }
            }

            // Calculate procedural micro-sway
            Vector3 swayPosOffset = Vector3.zero;
            Vector3 swayRotOffset = Vector3.zero;

            if (enableProceduralSway && t > 0.05f)
            {
                swayTimer += Time.deltaTime * swayFrequency;
                float sinVal = Mathf.Sin(swayTimer);
                float cosVal = Mathf.Cos(swayTimer * 0.8f);

                if (useNoiseForSway)
                {
                    float nX = (Mathf.PerlinNoise(swayTimer * 0.85f, 23.4f) * 2f - 1f);
                    float nY = (Mathf.PerlinNoise(swayTimer * 0.85f, 67.8f) * 2f - 1f);
                    float nZ = (Mathf.PerlinNoise(swayTimer * 0.85f, 91.2f) * 2f - 1f);

                    sinVal = Mathf.Lerp(sinVal, nY, swayNoiseInfluence);
                    cosVal = Mathf.Lerp(cosVal, nX, swayNoiseInfluence);
                }

                swayPosOffset = new Vector3(cosVal * swayPosAmplitude * 0.4f, sinVal * swayPosAmplitude, 0f) * t;
                swayRotOffset = new Vector3(sinVal * swayRotAmplitude * 0.6f, 0f, cosVal * swayRotAmplitude) * t;
            }

            Vector3 impulsePos = new Vector3(0f, -bookImpulseOffset * 0.35f, bookImpulseOffset);
            Vector3 impulseRot = new Vector3(bookImpulseOffset * 140f, 0f, 0f);

            float focusT = isInDeskMode ? transitionCurve.Evaluate(deskFocusProgress) : t;

            // 1. Apply to 3D Viewmodel Transform
            if (bookViewmodelTransform != null)
            {
                Vector3 targetPos;
                Vector3 targetRotEuler;

                if (isInDeskMode)
                {
                    // Interpolate between desk resting pose (Character Focus) and raised close-up pose (Book Focus)
                    targetPos = Vector3.Lerp(deskRestPosition, raisedLocalPosition, focusT) + swayPosOffset + impulsePos;
                    targetRotEuler = Vector3.Lerp(deskRestEuler, raisedLocalEuler, focusT) + swayRotOffset + impulseRot;
                }
                else
                {
                    targetPos = Vector3.Lerp(loweredLocalPosition, raisedLocalPosition, t) + swayPosOffset + impulsePos;
                    targetRotEuler = Vector3.Lerp(loweredLocalEuler, raisedLocalEuler, t) + swayRotOffset + impulseRot;
                }

                bookViewmodelTransform.localPosition = targetPos;
                bookViewmodelTransform.localRotation = Quaternion.Euler(targetRotEuler);
            }

            // 1B. Drive First-Person Hands Pose & Grip Blend
            if (handsController != null)
            {
                handsController.SetInspectionBlend(focusT, t);
            }

            // 2. Apply to 2D UI RectTransform (if assigned)
            if (bookRectTransform != null)
            {
                Vector2 baseUiPos = Vector2.Lerp(uiLoweredAnchoredPos, uiRaisedAnchoredPos, t);
                baseUiPos.y += swayPosOffset.y * 100f;
                bookRectTransform.anchoredPosition = baseUiPos;
                bookRectTransform.localRotation = Quaternion.Euler(0f, 0f, swayRotOffset.z);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Clamp01(t * 1.5f);
                    canvasGroup.interactable = isBookOpen && t > 0.85f;
                    canvasGroup.blocksRaycasts = isBookOpen && t > 0.85f;
                }
            }

            // 3. Fade inspection light softly (toned down warm candlelight)
            if (inspectionLight != null)
            {
                float lightFactor = isInDeskMode ? Mathf.Lerp(0.65f, 1f, deskFocusProgress) : t;
                inspectionLight.intensity = maxInspectionLightIntensity * lightFactor;
                inspectionLight.enabled = lightFactor > 0.01f;
            }
        }

        private void HandlePageInputs()
        {
            if (!isBookOpen || guestBookUI == null || currentTransitionProgress < 0.8f)
                return;

            bool prev = false;
            bool next = false;

            if (Mouse.current != null)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (scrollY > 0.05f) prev = true;
                else if (scrollY < -0.05f) next = true;
            }
            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                    prev = true;
                else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                    next = true;
            }

            if (!prev && !next)
            {
                try
                {
                    float scroll = Input.GetAxis("Mouse ScrollWheel");
                    if (scroll > 0.05f || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                        prev = true;
                    else if (scroll < -0.05f || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                        next = true;
                }
                catch { }
            }

            if (prev)
            {
                guestBookUI.PreviousPage();
            }
            else if (next)
            {
                guestBookUI.NextPage();
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                audioSource.PlayOneShot(clip);
            }
        }

        public float ApplyInspectionPitchClamp(float currentCameraPitch)
        {
            if (!isBookOpen || !containScreenLook) return currentCameraPitch;
            return Mathf.Clamp(currentCameraPitch, minInspectionPitch, maxInspectionPitch);
        }
    }
}
