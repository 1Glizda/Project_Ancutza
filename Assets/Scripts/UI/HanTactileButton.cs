using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Han.CheckIn
{
    public enum HanButtonVisualState
    {
        Normal,
        Highlighted,
        Pressed,
        Selected,
        Disabled
    }

    /// <summary>
    /// Tactile vintage tab/plate button controller for the bureaucratic desk thriller UI.
    /// Provides fluid unscaled transitions across all 5 states (Normal, Highlighted/Hovered, Pressed, Selected, Disabled)
    /// with micro-interactions: horizontal nudge, animated brass underline draw-in, left index tab glow,
    /// mechanical press depression, and dedicated UI audio hooks.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class HanTactileButton : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler,
        ISubmitHandler
    {
        [Header("Button Role & Style")]
        [Tooltip("Primary buttons use the wax-stamp crimson palette and heavy stamp audio; secondary buttons use archival slate/sepia.")]
        [SerializeField] private bool isPrimaryAction = false;

        [Header("Visual Element Bindings")]
        [SerializeField] private Button targetButton;
        [SerializeField] private RectTransform plateContentRect;
        [SerializeField] private Image plateImage;
        [SerializeField] private Image leftTabImage;
        [SerializeField] private RectTransform underlineRect;
        [SerializeField] private Image underlineImage;
        [SerializeField] private TMP_Text labelText;
        [SerializeField] private TMP_Text rightGlyphText;
        [SerializeField] private Shadow dropShadow;

        [Header("State Colors (Archival Ledger Palette)")]
        [SerializeField] private Color normalPlateColor = HanUITheme.MutedSlate;
        [SerializeField] private Color hoveredPlateColor = HanUITheme.HoveredSlatePlate;
        [SerializeField] private Color pressedPlateColor = HanUITheme.PressedCrimson;
        [SerializeField] private Color selectedPlateColor = HanUITheme.HoveredSlatePlate;
        [SerializeField] private Color disabledPlateColor = HanUITheme.DisabledPlate;

        [SerializeField] private Color normalTextColor = HanUITheme.WarmParchment;
        [SerializeField] private Color hoveredTextColor = HanUITheme.BurnishedGold;
        [SerializeField] private Color pressedTextColor = HanUITheme.WarmParchment;
        [SerializeField] private Color disabledTextColor = HanUITheme.DisabledInk;

        [SerializeField] private Color normalTabColor = HanUITheme.SubduedBrass;
        [SerializeField] private Color activeTabColor = HanUITheme.BurnishedGold;

        [Header("Micro-Interaction Settings")]
        [Tooltip("Horizontal nudge in pixels when hovered or selected.")]
        [SerializeField] private float hoverNudgeX = 8f;
        [Tooltip("Mechanical downward depression offset when pressed.")]
        [SerializeField] private Vector2 pressDepressionOffset = new Vector2(2f, -3f);
        [SerializeField] private float hoverScale = 1.02f;
        [SerializeField] private float pressScale = 0.97f;
        [SerializeField] private float transitionSpeed = 14f;
        [SerializeField] private float maxUnderlineWidth = 240f;

        public HanButtonVisualState CurrentVisualState { get; private set; } = HanButtonVisualState.Normal;

        private RectTransform _selfRect;
        private Vector2 _initialAnchoredPos;
        private bool _hasCapturedInitialPos;
        private bool _isPointerInside;
        private bool _isPointerDown;
        private bool _isSelected;
        private bool _lastInteractable = true;

        private float _currentUnderlineRatio = 0f;
        private float _currentTabWidth = 6f;
        private Vector2 _currentPositionOffset = Vector2.zero;
        private float _currentScale = 1f;
        private Coroutine _submitPulseRoutine;

        private void Awake()
        {
            EnsureBindings();
            CaptureInitialPosition();
            ApplyThemeDefaults();
        }

        private void OnEnable()
        {
            EnsureBindings();
            CaptureInitialPosition();
            _isPointerInside = false;
            _isPointerDown = false;
            _isSelected = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            EvaluateState(immediate: true);
        }

        private void OnDisable()
        {
            _isPointerInside = false;
            _isPointerDown = false;
            _isSelected = false;
            if (_selfRect != null && _hasCapturedInitialPos)
            {
                _selfRect.anchoredPosition = _initialAnchoredPos;
                _selfRect.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            bool interactable = targetButton == null || targetButton.interactable;
            if (interactable != _lastInteractable)
            {
                _lastInteractable = interactable;
                EvaluateState(immediate: false);
            }

            AnimateMicroInteractions();
        }

        public void ConfigureStyle(
            bool primary,
            Image plate,
            Image leftTab,
            RectTransform underline,
            Image underlineImg,
            TMP_Text label,
            TMP_Text glyph,
            Shadow shadow,
            float underlineMaxWidth)
        {
            isPrimaryAction = primary;
            plateImage = plate;
            leftTabImage = leftTab;
            underlineRect = underline;
            underlineImage = underlineImg;
            labelText = label;
            rightGlyphText = glyph;
            dropShadow = shadow;
            maxUnderlineWidth = underlineMaxWidth;

            EnsureBindings();
            ApplyThemeDefaults();
            EvaluateState(immediate: true);
        }

        private void EnsureBindings()
        {
            if (_selfRect == null) _selfRect = GetComponent<RectTransform>();
            if (targetButton == null) targetButton = GetComponent<Button>();
            if (plateImage == null) plateImage = GetComponent<Image>();
            if (dropShadow == null) dropShadow = GetComponent<Shadow>();

            if (targetButton != null)
            {
                // Disable Unity's built-in ColorTint so HanTactileButton drives all 5 states cleanly
                targetButton.transition = Selectable.Transition.None;
            }

            if (labelText != null)
            {
                HanUITheme.ApplyCrispSDF(labelText);
            }
            if (rightGlyphText != null)
            {
                HanUITheme.ApplyCrispSDF(rightGlyphText);
            }
        }

        private void CaptureInitialPosition()
        {
            if (_selfRect != null && !_hasCapturedInitialPos)
            {
                _initialAnchoredPos = _selfRect.anchoredPosition;
                _hasCapturedInitialPos = true;
            }
        }

        private void ApplyThemeDefaults()
        {
            if (isPrimaryAction)
            {
                normalPlateColor = HanUITheme.StampCrimson;
                hoveredPlateColor = new Color(0.66f, 0.22f, 0.16f, 1f);
                pressedPlateColor = HanUITheme.PressedCrimson;
                selectedPlateColor = new Color(0.66f, 0.22f, 0.16f, 1f);
                normalTabColor = HanUITheme.BurnishedGold;
                activeTabColor = HanUITheme.WarmParchment;
            }
            else
            {
                normalPlateColor = HanUITheme.MutedSlate;
                hoveredPlateColor = HanUITheme.HoveredSlatePlate;
                pressedPlateColor = HanUITheme.PressedCrimson;
                selectedPlateColor = HanUITheme.HoveredSlatePlate;
                normalTabColor = HanUITheme.SubduedBrass;
                activeTabColor = HanUITheme.BurnishedGold;
            }

            normalTextColor = HanUITheme.WarmParchment;
            hoveredTextColor = HanUITheme.BurnishedGold;
            pressedTextColor = HanUITheme.WarmParchment;
            disabledPlateColor = HanUITheme.DisabledPlate;
            disabledTextColor = HanUITheme.DisabledInk;
        }

        // ------------------------------------------------------------------
        //  EventSystem Pointer & Navigation Handlers
        // ------------------------------------------------------------------
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (targetButton != null && !targetButton.interactable) return;
            _isPointerInside = true;

            // Sync EventSystem selection on pointer hover when mouse moves so keyboard & mouse share focus cleanly
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != gameObject)
            {
                EventSystem.current.SetSelectedGameObject(gameObject);
            }

            HanUIAudioManager.PlayHover();
            EvaluateState(immediate: false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            _isPointerDown = false;
            EvaluateState(immediate: false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (targetButton != null && !targetButton.interactable) return;

            _isPointerDown = true;
            if (isPrimaryAction)
                HanUIAudioManager.PlayStampClick();
            else
                HanUIAudioManager.PlayClick();

            EvaluateState(immediate: false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            _isPointerDown = false;
            EvaluateState(immediate: false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (targetButton != null && !targetButton.interactable) return;
            bool wasActive = _isSelected || _isPointerInside;
            _isSelected = true;

            if (!wasActive)
            {
                HanUIAudioManager.PlayHover();
            }

            EvaluateState(immediate: false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _isSelected = false;
            EvaluateState(immediate: false);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (targetButton != null && !targetButton.interactable) return;

            if (isPrimaryAction)
                HanUIAudioManager.PlayStampClick();
            else
                HanUIAudioManager.PlayClick();

            if (gameObject.activeInHierarchy)
            {
                if (_submitPulseRoutine != null) StopCoroutine(_submitPulseRoutine);
                _submitPulseRoutine = StartCoroutine(SubmitDepressionPulseRoutine());
            }
        }

        private IEnumerator SubmitDepressionPulseRoutine()
        {
            _isPointerDown = true;
            EvaluateState(immediate: false);
            yield return new WaitForSecondsRealtime(0.09f);
            _isPointerDown = false;
            EvaluateState(immediate: false);
            _submitPulseRoutine = null;
        }

        // ------------------------------------------------------------------
        //  State Evaluation & Unscaled Micro-Interaction Tweening
        // ------------------------------------------------------------------
        private void EvaluateState(bool immediate)
        {
            bool interactable = targetButton == null || targetButton.interactable;
            if (!interactable)
            {
                CurrentVisualState = HanButtonVisualState.Disabled;
            }
            else if (_isPointerDown)
            {
                CurrentVisualState = HanButtonVisualState.Pressed;
            }
            else if (_isPointerInside)
            {
                CurrentVisualState = HanButtonVisualState.Highlighted;
            }
            else if (_isSelected)
            {
                CurrentVisualState = HanButtonVisualState.Selected;
            }
            else
            {
                CurrentVisualState = HanButtonVisualState.Normal;
            }

            if (immediate)
            {
                ApplyStateVisualsImmediate();
            }
        }

        private void AnimateMicroInteractions()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f) * transitionSpeed;

            Color targetPlate = normalPlateColor;
            Color targetText = normalTextColor;
            Color targetTab = normalTabColor;
            Vector2 targetOffset = Vector2.zero;
            float targetScale = 1f;
            float targetUnderline = 0f;
            float targetTabW = 6f;
            float targetGlyphAlpha = 0f;
            Vector2 targetShadowDist = new Vector2(3f, -4f);

            switch (CurrentVisualState)
            {
                case HanButtonVisualState.Normal:
                    targetPlate = normalPlateColor;
                    targetText = normalTextColor;
                    targetTab = normalTabColor;
                    targetOffset = Vector2.zero;
                    targetScale = 1f;
                    targetUnderline = 0f;
                    targetTabW = 6f;
                    targetGlyphAlpha = 0.20f;
                    targetShadowDist = new Vector2(3f, -4f);
                    break;

                case HanButtonVisualState.Highlighted:
                case HanButtonVisualState.Selected:
                    targetPlate = CurrentVisualState == HanButtonVisualState.Highlighted ? hoveredPlateColor : selectedPlateColor;
                    targetText = hoveredTextColor;
                    targetTab = activeTabColor;
                    targetOffset = new Vector2(hoverNudgeX, 0f);
                    targetScale = hoverScale;
                    targetUnderline = 1f;
                    targetTabW = 11f;
                    targetGlyphAlpha = 1f;
                    targetShadowDist = new Vector2(5f, -5f);
                    break;

                case HanButtonVisualState.Pressed:
                    targetPlate = pressedPlateColor;
                    targetText = pressedTextColor;
                    targetTab = HanUITheme.WarmParchment;
                    targetOffset = new Vector2(hoverNudgeX * 0.5f, 0f) + pressDepressionOffset;
                    targetScale = pressScale;
                    targetUnderline = 1f;
                    targetTabW = 13f;
                    targetGlyphAlpha = 1f;
                    targetShadowDist = new Vector2(1f, -1f);
                    break;

                case HanButtonVisualState.Disabled:
                    targetPlate = disabledPlateColor;
                    targetText = disabledTextColor;
                    targetTab = HanUITheme.DisabledInk;
                    targetOffset = Vector2.zero;
                    targetScale = 1f;
                    targetUnderline = 0f;
                    targetTabW = 4f;
                    targetGlyphAlpha = 0f;
                    targetShadowDist = new Vector2(1f, -1f);
                    break;
            }

            _currentPositionOffset = Vector2.Lerp(_currentPositionOffset, targetOffset, dt);
            _currentScale = Mathf.Lerp(_currentScale, targetScale, dt);
            _currentUnderlineRatio = Mathf.Lerp(_currentUnderlineRatio, targetUnderline, dt);
            _currentTabWidth = Mathf.Lerp(_currentTabWidth, targetTabW, dt);

            if (_selfRect != null && _hasCapturedInitialPos)
            {
                _selfRect.anchoredPosition = _initialAnchoredPos + _currentPositionOffset;
                _selfRect.localScale = new Vector3(_currentScale, _currentScale, 1f);
            }

            if (plateImage != null)
            {
                plateImage.color = Color.Lerp(plateImage.color, targetPlate, dt);
            }

            if (labelText != null)
            {
                labelText.color = Color.Lerp(labelText.color, targetText, dt);
            }

            if (leftTabImage != null)
            {
                leftTabImage.color = Color.Lerp(leftTabImage.color, targetTab, dt);
                RectTransform tabRt = leftTabImage.rectTransform;
                tabRt.sizeDelta = new Vector2(_currentTabWidth, tabRt.sizeDelta.y);
            }

            if (underlineRect != null)
            {
                underlineRect.sizeDelta = new Vector2(maxUnderlineWidth * _currentUnderlineRatio, underlineRect.sizeDelta.y);
                if (underlineImage != null)
                {
                    Color ulCol = activeTabColor;
                    ulCol.a = Mathf.Clamp01(_currentUnderlineRatio * 1.15f);
                    underlineImage.color = ulCol;
                }
            }

            if (rightGlyphText != null)
            {
                Color gc = targetText;
                gc.a = Mathf.Lerp(rightGlyphText.color.a, targetGlyphAlpha, dt);
                rightGlyphText.color = gc;
            }

            if (dropShadow != null)
            {
                dropShadow.effectDistance = Vector2.Lerp(dropShadow.effectDistance, targetShadowDist, dt);
            }
        }

        private void ApplyStateVisualsImmediate()
        {
            _currentPositionOffset = Vector2.zero;
            _currentScale = 1f;
            _currentUnderlineRatio = (CurrentVisualState == HanButtonVisualState.Highlighted || CurrentVisualState == HanButtonVisualState.Selected) ? 1f : 0f;
            _currentTabWidth = (CurrentVisualState == HanButtonVisualState.Highlighted || CurrentVisualState == HanButtonVisualState.Selected) ? 11f : 6f;

            if (_selfRect != null && _hasCapturedInitialPos)
            {
                _selfRect.anchoredPosition = _initialAnchoredPos;
                _selfRect.localScale = Vector3.one;
            }

            if (plateImage != null)
            {
                plateImage.color = CurrentVisualState == HanButtonVisualState.Disabled ? disabledPlateColor : normalPlateColor;
            }

            if (labelText != null)
            {
                labelText.color = CurrentVisualState == HanButtonVisualState.Disabled ? disabledTextColor : normalTextColor;
            }

            if (leftTabImage != null)
            {
                leftTabImage.color = normalTabColor;
                RectTransform tabRt = leftTabImage.rectTransform;
                tabRt.sizeDelta = new Vector2(_currentTabWidth, tabRt.sizeDelta.y);
            }

            if (underlineRect != null)
            {
                underlineRect.sizeDelta = new Vector2(maxUnderlineWidth * _currentUnderlineRatio, underlineRect.sizeDelta.y);
            }

            if (rightGlyphText != null)
            {
                Color gc = normalTextColor;
                gc.a = 0.2f;
                rightGlyphText.color = gc;
            }
        }
    }
}
