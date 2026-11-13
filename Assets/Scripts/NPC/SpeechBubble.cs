using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Han.CheckIn;

/// <summary>
/// Tactile world-space archival transcript card floating above NPCs.
/// Features:
/// - Archival ledger palette (deep sepia card, warm parchment TMP SDF text, double-ruled brass border, ornamental divider).
/// - Smooth fade-in / scale-in (EaseOutBack) and fade-out tweens + paper rustle & typewriter tick audio hooks.
/// - Tactile "Go on" prompt button with Normal, Hovered, Pressed (mechanical depression), Selected, and Disabled states.
/// - Smooth camera billboarding and dynamic distance + free-look angle scale compensation.
/// </summary>
public class SpeechBubble : MonoBehaviour
{
    [Header("World-Space Positioning & Scaling")]
    [SerializeField] private float bubbleHeight = 2.08f;
    [SerializeField] private float baseWorldScale = 0.0052f;
    [SerializeField] private float minDistanceScaleMultiplier = 0.88f;
    [SerializeField] private float maxDistanceScaleMultiplier = 1.45f;
    [SerializeField] private float referenceDistance = 3.0f;
    [SerializeField] private float maxScaleDistance = 9.5f;
    [SerializeField] private float billboardSmoothSpeed = 18f;

    [Header("Tween & Typewriter Settings")]
    [SerializeField] private float popInDuration = 0.22f;
    [SerializeField] private float popOutDuration = 0.15f;
    [SerializeField] private float interactiveTypewriterSpeed = 55f;

    [Header("Typing Sound Cadence")]
    [Tooltip("When enabled, inherits chars-per-tick and cadence multiplier from HanUIAudioManager.")]
    [SerializeField] private bool useGlobalAudioCadence = true;
    [Tooltip("Play a typing tick every N characters (when not overridden by global audio settings).")]
    [Range(1, 8)] [SerializeField] private int charsPerSoundTick = 2;
    [Tooltip("Minimum seconds between sound ticks (0 = inherit from HanUIAudioManager).")]
    [Range(0f, 0.25f)] [SerializeField] private float minSoundInterval = 0f;
    [Tooltip("Pitch multiplier for speech bubble typing sounds.")]
    [Range(0.5f, 1.8f)] [SerializeField] private float soundPitchScale = 1.0f;
    [Tooltip("Volume multiplier for speech bubble typing sounds.")]
    [Range(0f, 2f)] [SerializeField] private float soundVolumeScale = 1.0f;
    [Tooltip("Delay multiplier when revealing punctuation during speech bubble typing.")]
    [Range(1f, 5f)] [SerializeField] private float punctuationPauseMultiplier = 2.0f;

    private Canvas _canvas;
    private CanvasGroup _canvasGroup;
    private RectTransform _canvasRect;
    private RectTransform _cardRootRect;

    private RectTransform _dialogueBoxRect;
    private Image _backgroundImage;
    private TMP_Text _headerText;
    private TMP_Text _textComponent;

    // Tactile "Go on" Prompt Elements
    private GameObject _goOnButtonObj;
    private RectTransform _goOnButtonRect;
    private Image _goOnButtonImage;
    private Image _goOnLeftTabImage;
    private RectTransform _goOnUnderlineRect;
    private Image _goOnUnderlineImage;
    private TMP_Text _goOnButtonText;
    private Shadow _goOnShadow;
    private Button _goOnButton;

    private Camera _mainCamera;
    private Coroutine _currentCoroutine;
    private Coroutine _tweenCoroutine;
    private Coroutine _pressRoutine;
    private bool _isInteractive;
    private bool _isPressingGoOn;
    private bool _wasHovered;
    private Action _onGoOnCallback;

    // Tactile "Go on" Prompt State Tracking
    private Vector2 _goOnBaseAnchoredPos = new Vector2(0f, 0f);
    private Vector2 _goOnCurrentOffset = Vector2.zero;
    private float _goOnCurrentScale = 1f;
    private float _goOnUnderlineRatio = 0f;

    private float _currentDistanceScale = 1f;

    private static readonly Color NormalPromptPlateColor = HanUITheme.StampCrimson;
    private static readonly Color HoverPromptPlateColor = new Color(0.68f, 0.23f, 0.17f, 1.0f);
    private static readonly Color PressedPromptPlateColor = HanUITheme.PressedCrimson;

    private bool _usesDefaultGoOnLabel = true;

    public bool IsInteractiveActive => _isInteractive && _canvas != null && _canvas.gameObject.activeInHierarchy;
    public RectTransform GoOnButtonRect => _goOnButtonRect;

    private void OnEnable()
    {
        HanLocalization.OnLanguageChanged += HandleLanguageChanged;
        HandleLanguageChanged(HanLocalization.CurrentLanguage);
    }

    private void OnDisable()
    {
        HanLocalization.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandleLanguageChanged(HanLanguage _)
    {
        UpdateSpeakerHeader();
        if (_goOnButtonText != null && _usesDefaultGoOnLabel)
        {
            _goOnButtonText.text = HanLocalization.Get("director.btn_go_on");
        }
    }

    private void Awake()
    {
        _mainCamera = Camera.main;
        BuildTranscriptCardUI();
    }

    private void BuildTranscriptCardUI()
    {
        TMP_FontAsset font = HanUITheme.GetProjectFont();

        // 1. World-Space Canvas anchored directly above the character
        GameObject canvasObj = new GameObject("SpeechBubbleCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0f, bubbleHeight, 0f);

        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 100;
        if (_mainCamera != null) _canvas.worldCamera = _mainCamera;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 24f;
        scaler.referencePixelsPerUnit = 100f;

        _canvasGroup = canvasObj.AddComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;

        canvasObj.AddComponent<GraphicRaycaster>();

        _canvasRect = canvasObj.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = new Vector2(368f, 198f);
        _canvasRect.localScale = Vector3.one * baseWorldScale;

        // 2. Animated Card Root (for pop-in scale & vertical slide tween)
        GameObject cardRootObj = new GameObject("CardRoot");
        cardRootObj.transform.SetParent(canvasObj.transform, false);
        _cardRootRect = cardRootObj.AddComponent<RectTransform>();
        _cardRootRect.anchorMin = Vector2.zero;
        _cardRootRect.anchorMax = Vector2.one;
        _cardRootRect.offsetMin = Vector2.zero;
        _cardRootRect.offsetMax = Vector2.zero;

        // 3. Dialogue Transcript Card Container (Top area)
        GameObject bgObj = new GameObject("DialogueBox");
        bgObj.transform.SetParent(cardRootObj.transform, false);
        _backgroundImage = bgObj.AddComponent<Image>();
        _backgroundImage.sprite = HanUITheme.GetFramedCardSprite();
        _backgroundImage.type = Image.Type.Sliced;
        _backgroundImage.color = HanUITheme.DeepSepiaInk;

        Shadow cardShadow = bgObj.AddComponent<Shadow>();
        cardShadow.effectColor = new Color(0.05f, 0.04f, 0.03f, 0.72f);
        cardShadow.effectDistance = new Vector2(3f, -4f);

        _dialogueBoxRect = bgObj.GetComponent<RectTransform>();
        _dialogueBoxRect.anchorMin = new Vector2(0f, 0.27f);
        _dialogueBoxRect.anchorMax = new Vector2(1f, 1f);
        _dialogueBoxRect.offsetMin = Vector2.zero;
        _dialogueBoxRect.offsetMax = Vector2.zero;

        // Subtle Vignette / Grain Overlay inside the transcript card
        GameObject grainObj = new GameObject("CardGrainOverlay");
        grainObj.transform.SetParent(bgObj.transform, false);
        RectTransform grainRt = grainObj.AddComponent<RectTransform>();
        grainRt.anchorMin = Vector2.zero;
        grainRt.anchorMax = Vector2.one;
        grainRt.offsetMin = new Vector2(4f, 4f);
        grainRt.offsetMax = new Vector2(-4f, -4f);
        Image grainImg = grainObj.AddComponent<Image>();
        grainImg.sprite = HanUITheme.GetVignetteGrainSprite();
        grainImg.color = new Color(1f, 1f, 1f, 0.25f);
        grainImg.raycastTarget = false;

        // Speaker Dossier Header Label
        GameObject headerObj = new GameObject("TranscriptHeader");
        headerObj.transform.SetParent(bgObj.transform, false);
        _headerText = headerObj.AddComponent<TextMeshProUGUI>();
        HanUITheme.ApplyCrispSDF(_headerText, font);
        _headerText.text = HanLocalization.Get("bubble.default_header");
        _headerText.enableAutoSizing = true;
        _headerText.fontSizeMin = 9f;
        _headerText.fontSizeMax = 11.5f;
        _headerText.alignment = TextAlignmentOptions.Center;
        _headerText.fontSize = 11.5f;
        _headerText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        _headerText.color = HanUITheme.SubduedBrass;
        _headerText.raycastTarget = false;

        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -7f);
        headerRect.sizeDelta = new Vector2(-20f, 18f);

        // Ornamental Divider Line under Header
        GameObject divObj = new GameObject("HeaderDivider");
        divObj.transform.SetParent(bgObj.transform, false);
        RectTransform divRect = divObj.AddComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.5f, 1f);
        divRect.anchorMax = new Vector2(0.5f, 1f);
        divRect.pivot = new Vector2(0.5f, 1f);
        divRect.anchoredPosition = new Vector2(0f, -24f);
        divRect.sizeDelta = new Vector2(240f, 6f);
        Image divImg = divObj.AddComponent<Image>();
        divImg.sprite = HanUITheme.GetDividerOrnamentSprite();
        divImg.color = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.65f);
        divImg.raycastTarget = false;

        // 4. Crisp TMP Dialogue Transcript Text
        GameObject textObj = new GameObject("TranscriptText");
        textObj.transform.SetParent(bgObj.transform, false);
        _textComponent = textObj.AddComponent<TextMeshProUGUI>();
        HanUITheme.ApplyCrispSDF(_textComponent, font);
        _textComponent.alignment = TextAlignmentOptions.Center;
        _textComponent.fontSize = 16.5f;
        _textComponent.enableAutoSizing = true;
        _textComponent.fontSizeMin = 10.5f;
        _textComponent.fontSizeMax = 16.5f;
        _textComponent.color = HanUITheme.WarmParchment;
        _textComponent.raycastTarget = false;

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 10f);
        textRect.offsetMax = new Vector2(-16f, -28f);

        // 5. Tactile "Go on" Stamp-Plate Prompt Button Positioned Directly Below Dialogue Card
        _goOnButtonObj = new GameObject("GoOnButton");
        _goOnButtonObj.transform.SetParent(cardRootObj.transform, false);
        _goOnButtonImage = _goOnButtonObj.AddComponent<Image>();
        _goOnButtonImage.sprite = HanUITheme.GetButtonPlateSprite();
        _goOnButtonImage.type = Image.Type.Sliced;
        _goOnButtonImage.color = NormalPromptPlateColor;

        _goOnShadow = _goOnButtonObj.AddComponent<Shadow>();
        _goOnShadow.effectColor = new Color(0.05f, 0.03f, 0.02f, 0.85f);
        _goOnShadow.effectDistance = new Vector2(2f, -3.5f);

        Outline btnOutline = _goOnButtonObj.AddComponent<Outline>();
        btnOutline.effectColor = HanUITheme.SubduedBrass;
        btnOutline.effectDistance = new Vector2(1.2f, -1.2f);

        _goOnButton = _goOnButtonObj.AddComponent<Button>();
        _goOnButton.transition = Selectable.Transition.None;
        _goOnButton.targetGraphic = _goOnButtonImage;
        _goOnButton.onClick.AddListener(TriggerGoOn);

        _goOnButtonRect = _goOnButtonObj.GetComponent<RectTransform>();
        _goOnButtonRect.anchorMin = new Vector2(0.5f, 0f);
        _goOnButtonRect.anchorMax = new Vector2(0.5f, 0f);
        _goOnButtonRect.pivot = new Vector2(0.5f, 0f);
        _goOnBaseAnchoredPos = new Vector2(0f, 2f);
        _goOnButtonRect.anchoredPosition = _goOnBaseAnchoredPos;
        _goOnButtonRect.sizeDelta = new Vector2(176f, 36f);

        // Left Brass Index Tab on "Go on" Button
        GameObject leftTabObj = new GameObject("LeftBrassTab");
        leftTabObj.transform.SetParent(_goOnButtonObj.transform, false);
        RectTransform leftTabRt = leftTabObj.AddComponent<RectTransform>();
        leftTabRt.anchorMin = new Vector2(0f, 0f);
        leftTabRt.anchorMax = new Vector2(0f, 1f);
        leftTabRt.pivot = new Vector2(0f, 0.5f);
        leftTabRt.anchoredPosition = Vector2.zero;
        leftTabRt.sizeDelta = new Vector2(5f, 0f);
        _goOnLeftTabImage = leftTabObj.AddComponent<Image>();
        _goOnLeftTabImage.color = HanUITheme.BurnishedGold;
        _goOnLeftTabImage.raycastTarget = false;

        // Animated Underline Draw-In on "Go on" Button
        GameObject ulObj = new GameObject("UnderlineBar");
        ulObj.transform.SetParent(_goOnButtonObj.transform, false);
        _goOnUnderlineRect = ulObj.AddComponent<RectTransform>();
        _goOnUnderlineRect.anchorMin = new Vector2(0.5f, 0f);
        _goOnUnderlineRect.anchorMax = new Vector2(0.5f, 0f);
        _goOnUnderlineRect.pivot = new Vector2(0.5f, 0f);
        _goOnUnderlineRect.anchoredPosition = new Vector2(0f, 3f);
        _goOnUnderlineRect.sizeDelta = new Vector2(0f, 2f);
        _goOnUnderlineImage = ulObj.AddComponent<Image>();
        _goOnUnderlineImage.color = HanUITheme.BurnishedGold;
        _goOnUnderlineImage.raycastTarget = false;

        // Crisp TMP Label inside "Go on" Button
        GameObject btnTextObj = new GameObject("GoOnLabel");
        btnTextObj.transform.SetParent(_goOnButtonObj.transform, false);
        _goOnButtonText = btnTextObj.AddComponent<TextMeshProUGUI>();
        HanUITheme.ApplyCrispSDF(_goOnButtonText, font);
        _goOnButtonText.text = HanLocalization.Get("director.btn_go_on");
        _goOnButtonText.alignment = TextAlignmentOptions.Center;
        _goOnButtonText.fontStyle = FontStyles.Bold;
        _goOnButtonText.fontSize = 15.5f;
        _goOnButtonText.enableAutoSizing = true;
        _goOnButtonText.fontSizeMin = 10.5f;
        _goOnButtonText.fontSizeMax = 15.5f;
        _goOnButtonText.color = HanUITheme.WarmParchment;
        _goOnButtonText.raycastTarget = false;

        RectTransform btnTextRect = btnTextObj.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = new Vector2(6f, 0f);
        btnTextRect.offsetMax = Vector2.zero;

        _goOnButtonObj.SetActive(false);
        canvasObj.SetActive(false);
    }

    private void Update()
    {
        if (!_isInteractive || _canvas == null || !_canvas.gameObject.activeInHierarchy)
            return;

        if (GameLoopManager.Instance != null && GameLoopManager.Instance.IsPaused)
            return;

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null || _goOnButtonRect == null) return;

        Vector2 mousePos = Vector2.zero;
        bool leftClick = false;
        bool leftHeld = false;

        if (Mouse.current != null)
        {
            mousePos = Mouse.current.position.ReadValue();
            if (Mouse.current.leftButton.wasPressedThisFrame) leftClick = true;
            if (Mouse.current.leftButton.isPressed) leftHeld = true;
        }
        try
        {
            if (mousePos == Vector2.zero) mousePos = Input.mousePosition;
            if (Input.GetMouseButtonDown(0)) leftClick = true;
            if (Input.GetMouseButton(0)) leftHeld = true;
        }
        catch { }

        bool hovered = RectTransformUtility.RectangleContainsScreenPoint(_goOnButtonRect, mousePos, _mainCamera);

        // Generous screen proximity tolerance so free-look rotation never makes clicking frustrating
        if (!hovered)
        {
            Vector3 screenPt = _mainCamera.WorldToScreenPoint(_goOnButtonRect.position);
            if (screenPt.z > 0f)
            {
                float dist = Vector2.Distance(mousePos, new Vector2(screenPt.x, screenPt.y));
                if (dist <= 95f) hovered = true;
            }
        }

        bool isSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _goOnButtonObj;
        bool isHighlighted = hovered || isSelected;

        if (hovered && !_wasHovered)
        {
            HanUIAudioManager.PlayHover();
        }
        _wasHovered = hovered;

        // Animate Tactile "Go on" Prompt States (Normal, Highlighted/Hovered, Pressed, Selected)
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 16f;
        bool isPressedState = _isPressingGoOn || (hovered && leftHeld);

        Color targetPlate = isPressedState
            ? PressedPromptPlateColor
            : (isHighlighted ? HoverPromptPlateColor : NormalPromptPlateColor);

        Color targetText = isHighlighted ? HanUITheme.BurnishedGold : HanUITheme.WarmParchment;
        Vector2 targetOffset = isPressedState
            ? new Vector2(1f, -2.8f)
            : (isHighlighted ? new Vector2(0f, 1.5f) : Vector2.zero);

        float targetScale = isPressedState ? 0.94f : (isHighlighted ? 1.04f : 1.0f);
        float targetUnderline = isHighlighted || isPressedState ? 1f : 0f;
        Vector2 targetShadow = isPressedState ? new Vector2(0.5f, -0.5f) : (isHighlighted ? new Vector2(3f, -4.5f) : new Vector2(2f, -3.5f));

        _goOnCurrentOffset = Vector2.Lerp(_goOnCurrentOffset, targetOffset, dt);
        _goOnCurrentScale = Mathf.Lerp(_goOnCurrentScale, targetScale, dt);
        _goOnUnderlineRatio = Mathf.Lerp(_goOnUnderlineRatio, targetUnderline, dt);

        _goOnButtonRect.anchoredPosition = _goOnBaseAnchoredPos + _goOnCurrentOffset;
        _goOnButtonRect.localScale = new Vector3(_goOnCurrentScale, _goOnCurrentScale, 1f);

        if (_goOnButtonImage != null)
            _goOnButtonImage.color = Color.Lerp(_goOnButtonImage.color, targetPlate, dt);

        if (_goOnButtonText != null)
            _goOnButtonText.color = Color.Lerp(_goOnButtonText.color, targetText, dt);

        if (_goOnUnderlineRect != null)
            _goOnUnderlineRect.sizeDelta = new Vector2(118f * _goOnUnderlineRatio, 2f);

        if (_goOnLeftTabImage != null)
            _goOnLeftTabImage.color = isHighlighted ? HanUITheme.BurnishedGold : HanUITheme.SubduedBrass;

        if (_goOnShadow != null)
            _goOnShadow.effectDistance = Vector2.Lerp(_goOnShadow.effectDistance, targetShadow, dt);

        // Check Keyboard & Gamepad advance inputs
        bool keyOrGamepadAdvance = false;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                keyOrGamepadAdvance = true;
            }
        }
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            keyOrGamepadAdvance = true;
        }
        try
        {
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                keyOrGamepadAdvance = true;
        }
        catch { }

        if (!_isPressingGoOn && ((leftClick && hovered) || keyOrGamepadAdvance))
        {
            TriggerGoOn();
        }
    }

    private void LateUpdate()
    {
        if (_canvas == null || !_canvas.gameObject.activeInHierarchy)
            return;

        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_mainCamera == null) return;

        // 1. Clean Camera Billboarding (smooth yaw + subtle pitch alignment, strictly level roll)
        Vector3 camPos = _mainCamera.transform.position;
        Vector3 bubblePos = _canvas.transform.position;
        Vector3 toBubble = bubblePos - camPos;
        float dist = toBubble.magnitude;

        if (dist > 0.001f)
        {
            Vector3 flatLook = new Vector3(toBubble.x, toBubble.y * 0.35f, toBubble.z);
            if (flatLook.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(flatLook.normalized, Vector3.up);
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f) * billboardSmoothSpeed;
                _canvas.transform.rotation = Quaternion.Slerp(_canvas.transform.rotation, targetRot, dt);
            }
        }

        // 2. Smooth Distance & Free-Look Rotation Scale Compensation
        float normalizedDist = Mathf.InverseLerp(referenceDistance, maxScaleDistance, dist);
        float targetDistMult = Mathf.Lerp(minDistanceScaleMultiplier, maxDistanceScaleMultiplier, normalizedDist);

        // Subtle focus scale boost when centered in free-look view
        Vector3 dirNormalized = dist > 0.001f ? (toBubble / dist) : Vector3.forward;
        float lookAlignment = Mathf.Clamp01(Vector3.Dot(_mainCamera.transform.forward, dirNormalized));
        float focusMultiplier = Mathf.Lerp(0.92f, 1.04f, Mathf.SmoothStep(0f, 1f, lookAlignment));

        float desiredScale = baseWorldScale * targetDistMult * focusMultiplier;
        _currentDistanceScale = Mathf.Lerp(_currentDistanceScale, desiredScale, Mathf.Min(Time.unscaledDeltaTime, 0.05f) * 12f);
        _canvasRect.localScale = Vector3.one * _currentDistanceScale;
    }

    /// <summary>
    /// Shows an interactive archival transcript card anchored directly above the character
    /// with a tactile "Go on" or "Approve" stamp button below it.
    /// </summary>
    public void ShowInteractiveDialogue(string text, Action onGoOnClicked, string buttonLabel = null, string speakerHeaderOverride = null)
    {
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
            _currentCoroutine = null;
        }
        if (_pressRoutine != null)
        {
            StopCoroutine(_pressRoutine);
            _pressRoutine = null;
        }

        UpdateSpeakerHeader(speakerHeaderOverride);

        _isInteractive = true;
        _isPressingGoOn = false;
        _wasHovered = false;
        _onGoOnCallback = onGoOnClicked;

        _usesDefaultGoOnLabel = string.IsNullOrEmpty(buttonLabel) || buttonLabel == "Go on >>";
        if (_goOnButtonText != null)
        {
            _goOnButtonText.text = _usesDefaultGoOnLabel
                ? HanLocalization.Get("director.btn_go_on")
                : buttonLabel;
        }

        if (_goOnButtonObj != null) _goOnButtonObj.SetActive(true);
        if (_canvas != null) _canvas.gameObject.SetActive(true);

        HanUIFocusNavigator.RegisterActivePrompt(_goOnButton);
        HanUIAudioManager.PlayPaperRustle();

        AnimateCardPopIn();
        _currentCoroutine = StartCoroutine(TypewriteInteractiveTranscriptRoutine(text));
    }

    public void TriggerGoOn()
    {
        if (!_isInteractive || _isPressingGoOn) return;

        // Play tactile mechanical press & stamp sound before invoking the callback
        HanUIAudioManager.PlayStampClick();

        if (gameObject.activeInHierarchy)
        {
            _pressRoutine = StartCoroutine(PressAndInvokeGoOnRoutine());
        }
        else
        {
            CompleteGoOnImmediate();
        }
    }

    private IEnumerator PressAndInvokeGoOnRoutine()
    {
        _isPressingGoOn = true;
        yield return new WaitForSecondsRealtime(0.085f);
        _isPressingGoOn = false;
        _pressRoutine = null;
        CompleteGoOnImmediate();
    }

    private void CompleteGoOnImmediate()
    {
        Action cb = _onGoOnCallback;
        _onGoOnCallback = null;
        _isInteractive = false;

        HanUIFocusNavigator.ClearActivePrompt(_goOnButton);
        if (_goOnButtonObj != null) _goOnButtonObj.SetActive(false);
        cb?.Invoke();
    }

    public void ShowMessage(string text, float duration = 4f)
    {
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
        }
        _isInteractive = false;
        _isPressingGoOn = false;
        _onGoOnCallback = null;
        HanUIFocusNavigator.ClearActivePrompt(_goOnButton);
        if (_goOnButtonObj != null) _goOnButtonObj.SetActive(false);

        UpdateSpeakerHeader();
        _currentCoroutine = StartCoroutine(ShowMessageCoroutine(text, duration));
    }

    public void ShowTypewriter(string text, float charsPerSecond = 30f, float displayDuration = 3f)
    {
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
        }
        _isInteractive = false;
        _isPressingGoOn = false;
        _onGoOnCallback = null;
        HanUIFocusNavigator.ClearActivePrompt(_goOnButton);
        if (_goOnButtonObj != null) _goOnButtonObj.SetActive(false);

        UpdateSpeakerHeader();
        _currentCoroutine = StartCoroutine(ShowTypewriterCoroutine(text, charsPerSecond, displayDuration));
    }

    public void Hide()
    {
        if (_currentCoroutine != null)
        {
            StopCoroutine(_currentCoroutine);
            _currentCoroutine = null;
        }
        if (_pressRoutine != null)
        {
            StopCoroutine(_pressRoutine);
            _pressRoutine = null;
        }

        _isInteractive = false;
        _isPressingGoOn = false;
        _onGoOnCallback = null;
        HanUIFocusNavigator.ClearActivePrompt(_goOnButton);

        if (_goOnButtonObj != null) _goOnButtonObj.SetActive(false);

        if (_canvas != null && _canvas.gameObject.activeInHierarchy && gameObject.activeInHierarchy)
        {
            if (_tweenCoroutine != null) StopCoroutine(_tweenCoroutine);
            _tweenCoroutine = StartCoroutine(PopOutAndDisableRoutine());
        }
        else if (_canvas != null)
        {
            _canvas.gameObject.SetActive(false);
        }
    }

    private void UpdateSpeakerHeader(string speakerOverride = null)
    {
        if (_headerText == null) return;
        if (!string.IsNullOrWhiteSpace(speakerOverride))
        {
            _headerText.text = $"[ {speakerOverride.Trim().ToUpperInvariant()} ]";
            _headerText.color = string.Equals(speakerOverride.Trim(), "ESMERALDA", StringComparison.OrdinalIgnoreCase)
                ? HanUITheme.BurnishedGold
                : HanUITheme.SubduedBrass;
            return;
        }

        NPCController npc = GetComponentInParent<NPCController>();
        _headerText.color = HanUITheme.SubduedBrass;
        if (npc != null && npc.Guest != null && !string.IsNullOrWhiteSpace(npc.Guest.guestName))
        {
            _headerText.text = $"[ {npc.Guest.guestName.ToUpperInvariant()} ]";
        }
        else
        {
            _headerText.text = HanLocalization.Get("bubble.default_header");
        }
    }

    // ------------------------------------------------------------------
    //  Smooth Card Tweens & Typewriter Coroutines
    // ------------------------------------------------------------------
    private void AnimateCardPopIn()
    {
        if (!gameObject.activeInHierarchy) return;
        if (_tweenCoroutine != null) StopCoroutine(_tweenCoroutine);
        _tweenCoroutine = StartCoroutine(PopInRoutine());
    }

    private IEnumerator PopInRoutine()
    {
        if (_canvasGroup == null || _cardRootRect == null) yield break;

        float elapsed = 0f;
        float dur = Mathf.Max(0.05f, popInDuration);

        while (elapsed < dur)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / dur);
            // EaseOutBack overshoot for crisp tactile card appearance
            float c1 = 1.70158f;
            float c3 = c1 + 1f;
            float tm1 = t - 1f;
            float easeOutBack = 1f + c3 * tm1 * tm1 * tm1 + c1 * tm1 * tm1;

            _canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 1.35f));
            float scale = Mathf.LerpUnclamped(0.82f, 1f, easeOutBack);
            _cardRootRect.localScale = new Vector3(scale, scale, 1f);
            _cardRootRect.anchoredPosition = new Vector2(0f, Mathf.Lerp(10f, 0f, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }

        _canvasGroup.alpha = 1f;
        _cardRootRect.localScale = Vector3.one;
        _cardRootRect.anchoredPosition = Vector2.zero;
        _tweenCoroutine = null;
    }

    private IEnumerator PopOutAndDisableRoutine()
    {
        if (_canvasGroup != null && _cardRootRect != null)
        {
            float elapsed = 0f;
            float dur = Mathf.Max(0.04f, popOutDuration);
            float startAlpha = _canvasGroup.alpha;

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dur);
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
                float scale = Mathf.Lerp(1f, 0.88f, t);
                _cardRootRect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
        }

        if (_canvas != null)
        {
            _canvas.gameObject.SetActive(false);
        }
        _tweenCoroutine = null;
    }

    private int ResolveCharsPerSoundTick()
    {
        return useGlobalAudioCadence
            ? HanUIAudioManager.GetConfiguredCharsPerTick(charsPerSoundTick)
            : Mathf.Clamp(charsPerSoundTick, 1, 8);
    }

    private float ResolvePunctuationMultiplier()
    {
        return useGlobalAudioCadence
            ? HanUIAudioManager.GetConfiguredPunctuationMultiplier(punctuationPauseMultiplier)
            : Mathf.Max(1f, punctuationPauseMultiplier);
    }

    private IEnumerator TypewriteInteractiveTranscriptRoutine(string text)
    {
        if (_textComponent == null) yield break;

        _textComponent.text = text ?? "";
        _textComponent.maxVisibleCharacters = 0;
        _textComponent.ForceMeshUpdate();

        int totalChars = _textComponent.textInfo.characterCount;
        if (totalChars <= 0)
        {
            _textComponent.maxVisibleCharacters = 99999;
            yield break;
        }

        float cps = Mathf.Max(12f, interactiveTypewriterSpeed);
        float baseDelay = 1f / cps;
        float timer = 0f;
        int currentCount = 0;
        int lastTickChar = -ResolveCharsPerSoundTick();

        while (currentCount < totalChars)
        {
            timer -= Time.unscaledDeltaTime;
            while (timer <= 0f && currentCount < totalChars)
            {
                char c = _textComponent.textInfo.characterInfo[currentCount].character;
                currentCount++;
                _textComponent.maxVisibleCharacters = currentCount;

                int tickStep = ResolveCharsPerSoundTick();
                if (!char.IsWhiteSpace(c) && (currentCount - lastTickChar >= tickStep))
                {
                    lastTickChar = currentCount;
                    float intervalOverride = minSoundInterval > 0f ? minSoundInterval : -1f;
                    HanUIAudioManager.PlayTypewriterTick(c, soundPitchScale, soundVolumeScale, intervalOverride);
                }

                float pauseMult = HanUIAudioManager.IsPunctuationChar(c) ? ResolvePunctuationMultiplier() : 1f;
                timer += baseDelay * pauseMult;
            }

            yield return null;
        }

        _textComponent.maxVisibleCharacters = 99999;
    }

    private IEnumerator ShowMessageCoroutine(string text, float duration)
    {
        _canvas.gameObject.SetActive(true);
        _textComponent.maxVisibleCharacters = 99999;
        _textComponent.text = text;
        AnimateCardPopIn();

        yield return new WaitForSeconds(duration);
        Hide();
    }

    private IEnumerator ShowTypewriterCoroutine(string text, float charsPerSecond, float displayDuration)
    {
        _canvas.gameObject.SetActive(true);
        _textComponent.text = text ?? "";
        _textComponent.maxVisibleCharacters = 0;
        AnimateCardPopIn();

        _textComponent.ForceMeshUpdate();
        int totalChars = _textComponent.textInfo.characterCount;

        if (charsPerSecond <= 0) charsPerSecond = 30f;
        float delay = 1f / charsPerSecond;
        int lastTickChar = -ResolveCharsPerSoundTick();

        for (int i = 1; i <= totalChars; i++)
        {
            _textComponent.maxVisibleCharacters = i;
            char c = _textComponent.textInfo.characterInfo[i - 1].character;
            int tickStep = ResolveCharsPerSoundTick();
            if (!char.IsWhiteSpace(c) && (i - lastTickChar >= tickStep))
            {
                lastTickChar = i;
                float intervalOverride = minSoundInterval > 0f ? minSoundInterval : -1f;
                HanUIAudioManager.PlayTypewriterTick(c, soundPitchScale, soundVolumeScale, intervalOverride);
            }

            float pauseMult = HanUIAudioManager.IsPunctuationChar(c) ? ResolvePunctuationMultiplier() : 1f;
            yield return new WaitForSeconds(delay * pauseMult);
        }

        _textComponent.maxVisibleCharacters = 99999;
        yield return new WaitForSeconds(displayDuration);
        Hide();
    }
}
