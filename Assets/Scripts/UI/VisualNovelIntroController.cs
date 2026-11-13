using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace Han.CheckIn
{
    /// <summary>
    /// Polished archival Gothic Visual Novel intro sequence replacing the legacy intro video.
    /// Features dynamic typewriter animation with punctuation cadence, subtle candlelight & ember glow,
    /// page progress pips, click/Space-to-complete-or-advance, and Skip support.
    /// </summary>
    [DisallowMultipleComponent]
    public class VisualNovelIntroController : MonoBehaviour
    {
        [Header("Intro Narrative Beats")]
        [Tooltip("When enabled, automatically pulls default narrative beats from HanDialogueCatalog / HanLocalization.")]
        [SerializeField] private bool useLocalizedDefaultBeats = true;
        [SerializeField] private IntroNarrativeBeat[] narrativeBeats;

        [Header("Typewriter Speed & Sound Cadence")]
        [Tooltip("Base reveal speed in characters per second.")]
        [Range(10f, 120f)]
        [SerializeField] private float charsPerSecond = 40f;

        [Tooltip("Pause multiplier for sentence-ending punctuation (., !, ?, …, —).")]
        [Range(1.0f, 6.0f)]
        [SerializeField] private float sentencePunctuationPause = 3.2f;

        [Tooltip("Pause multiplier for commas and colons (, ; :).")]
        [Range(1.0f, 4.5f)]
        [SerializeField] private float clausePunctuationPause = 2.1f;

        [Tooltip("When enabled, cadence scales alongside HanUIAudioManager's global TypewriterCadenceMultiplier.")]
        [SerializeField] private bool scaleWithGlobalAudioCadence = true;

        [Tooltip("Play a typewriter sound tick every N non-whitespace characters.")]
        [Range(1, 8)]
        [SerializeField] private int charsPerSoundTick = 2;

        [Tooltip("Minimum unscaled seconds between two typewriter sound ticks.")]
        [Range(0.008f, 0.25f)]
        [SerializeField] private float minSoundInterval = 0.032f;

        [Tooltip("Minimum pitch multiplier applied per sound tick.")]
        [Range(0.5f, 1.5f)]
        [SerializeField] private float soundPitchScale = 0.96f;

        [Tooltip("Relative volume multiplier for the intro typing sound.")]
        [Range(0f, 2f)]
        [SerializeField] private float soundVolumeScale = 1.05f;

        [Header("Visual Pacing & Transitions")]
        [SerializeField] private float panelFadeInDuration = 0.50f;
        [SerializeField] private float panelFadeOutDuration = 0.42f;
        [SerializeField] private float slideTransitionDuration = 0.18f;
        [SerializeField] private float allowAdvanceAfterSeconds = 0.18f;

        [Header("Generated / Bound UI References")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private CanvasGroup slideBodyCanvasGroup;
        [SerializeField] private TMP_Text globalHeaderText;
        [SerializeField] private TMP_Text chapterTagText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text slideCounterText;
        [SerializeField] private TMP_Text continueButtonText;
        [SerializeField] private TMP_Text continueHintText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button backdropAdvanceButton;
        [SerializeField] private Button cardAdvanceButton;
        [SerializeField] private RectTransform candleGlowRect;
        [SerializeField] private Image candleGlowImage;
        [SerializeField] private RectTransform cardTransform;
        [SerializeField] private Image[] progressPips;

        public Button SkipButton => skipButton;
        public Button ContinueButton => continueButton;
        public bool IsPlaying { get; private set; }

        public float CharsPerSecond
        {
            get => charsPerSecond;
            set => charsPerSecond = Mathf.Clamp(value, 10f, 120f);
        }

        public int CharsPerSoundTick
        {
            get => charsPerSoundTick;
            set => charsPerSoundTick = Mathf.Clamp(value, 1, 8);
        }

        public float MinSoundInterval
        {
            get => minSoundInterval;
            set => minSoundInterval = Mathf.Clamp(value, 0.008f, 0.25f);
        }

        private bool _uiBuilt;
        private bool _skipRequested;
        private bool _advanceRequested;
        private bool _slideIsTyping;
        private bool _completeCurrentSlideRequested;
        private float _slideShownTime;

        private int _activeSlideIndex = -1;
        private int _activeSlideTotal;

        private readonly List<RectTransform> _emberRects = new List<RectTransform>();
        private readonly List<Image> _emberImages = new List<Image>();
        private readonly List<Vector4> _emberParams = new List<Vector4>(); // x=speed, y=amp, z=phase, w=baseAlpha

        private static Sprite _radialGlowSprite;
        private static Sprite _diamondPipSprite;
        private static Sprite _cornerFiligreeSprite;

        private void Reset()
        {
            narrativeBeats = HanDialogueCatalog.DefaultIntroBeats;
        }

        private void Awake()
        {
            EnsureDefaultBeats();
            EnsureVisualNovelUI();
            ApplyLocalizedChromeLabels();
        }

        private void OnEnable()
        {
            HanLocalization.OnLanguageChanged += HandleLanguageChanged;
            ApplyLocalizedChromeLabels();
        }

        private void OnDisable()
        {
            HanLocalization.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged(HanLanguage _)
        {
            EnsureDefaultBeats();
            ApplyLocalizedChromeLabels();
            if (IsPlaying && _activeSlideIndex >= 0 && narrativeBeats != null && _activeSlideIndex < narrativeBeats.Length)
            {
                IntroNarrativeBeat beat = narrativeBeats[_activeSlideIndex];
                if (chapterTagText != null)
                {
                    chapterTagText.text = string.IsNullOrWhiteSpace(beat.chapterTag)
                        ? HanLocalization.Pick($"PROLOGUE · {_activeSlideIndex + 1}", $"PROLOG · {_activeSlideIndex + 1}", $"PROLÓGUS · {_activeSlideIndex + 1}")
                        : beat.chapterTag;
                }
                if (bodyText != null)
                {
                    bodyText.text = beat.bodyText ?? "";
                    if (!_slideIsTyping)
                    {
                        bodyText.maxVisibleCharacters = 99999;
                        UpdateContinuePromptReady(_activeSlideIndex >= _activeSlideTotal - 1);
                    }
                    else if (continueButtonText != null)
                    {
                        continueButtonText.text = HanLocalization.Pick("WAIT...", "AȘTEAPTĂ...", "VÁRJ...");
                    }
                }
            }
        }

        private void Update()
        {
            if (!IsPlaying) return;

            UpdateAmbientAtmosphere();
            PollIntroInput();
        }

        /// <summary>Configures typing cadence and speed cleanly from code or menus.</summary>
        public void ConfigureTypingCadence(float speedCps, int everyNChars, float minTickIntervalSec, float pitchMult = 0.96f, float volMult = 1.05f)
        {
            charsPerSecond = Mathf.Clamp(speedCps, 10f, 120f);
            charsPerSoundTick = Mathf.Clamp(everyNChars, 1, 8);
            minSoundInterval = Mathf.Clamp(minTickIntervalSec, 0.008f, 0.25f);
            soundPitchScale = Mathf.Clamp(pitchMult, 0.5f, 1.5f);
            soundVolumeScale = Mathf.Clamp(volMult, 0f, 2f);
        }

        /// <summary>Requests skipping the entire intro sequence immediately.</summary>
        public void RequestSkip()
        {
            if (!IsPlaying) return;
            _skipRequested = true;
            _completeCurrentSlideRequested = true;
            _advanceRequested = true;
        }

        /// <summary>Either finishes typing the current paragraph or advances to the next beat.</summary>
        public void RequestAdvanceOrComplete()
        {
            if (!IsPlaying) return;
            if (Time.unscaledTime - _slideShownTime < allowAdvanceAfterSeconds) return;

            if (_slideIsTyping)
            {
                _completeCurrentSlideRequested = true;
                HanUIAudioManager.PlayClick();
            }
            else
            {
                _advanceRequested = true;
            }
        }

        /// <summary>Plays the complete Visual Novel intro sequence and invokes callbacks on completion or skip.</summary>
        public IEnumerator PlayIntroSequenceRoutine(Action onSkipPressed = null)
        {
            EnsureDefaultBeats();
            EnsureVisualNovelUI();
            ApplyLocalizedChromeLabels();

            DisableLegacyVideoComponents();

            IsPlaying = true;
            _skipRequested = false;
            _advanceRequested = false;
            _slideIsTyping = false;
            _completeCurrentSlideRequested = false;

            if (skipButton != null)
            {
                skipButton.onClick.RemoveAllListeners();
                skipButton.onClick.AddListener(() =>
                {
                    HanUIAudioManager.PlayClick();
                    RequestSkip();
                    onSkipPressed?.Invoke();
                });
            }

            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(RequestAdvanceOrComplete);
            }

            if (backdropAdvanceButton != null)
            {
                backdropAdvanceButton.onClick.RemoveAllListeners();
                backdropAdvanceButton.onClick.AddListener(RequestAdvanceOrComplete);
            }

            if (cardAdvanceButton != null)
            {
                cardAdvanceButton.onClick.RemoveAllListeners();
                cardAdvanceButton.onClick.AddListener(RequestAdvanceOrComplete);
            }

            gameObject.SetActive(true);
            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 0f;
                rootCanvasGroup.blocksRaycasts = true;
                rootCanvasGroup.interactable = true;
            }

            // Initialize first slide text hidden before fade-in so card is already framed neatly
            IntroNarrativeBeat[] beats = narrativeBeats;
            int total = beats != null ? beats.Length : 0;
            if (total > 0)
            {
                PrepareSlideVisuals(beats[0], 0, total);
                if (bodyText != null) bodyText.maxVisibleCharacters = 0;
            }

            yield return FadeCanvasGroup(rootCanvasGroup, 0f, 1f, panelFadeInDuration);

            for (int idx = 0; idx < total && !_skipRequested; idx++)
            {
                _activeSlideIndex = idx;
                _activeSlideTotal = total;
                _advanceRequested = false;
                _completeCurrentSlideRequested = false;
                _slideShownTime = Time.unscaledTime;

                IntroNarrativeBeat currentBeat = (narrativeBeats != null && idx < narrativeBeats.Length) ? narrativeBeats[idx] : beats[idx];
                if (idx > 0)
                {
                    yield return FadeCanvasGroup(slideBodyCanvasGroup, 1f, 0f, slideTransitionDuration * 0.7f);
                    PrepareSlideVisuals(currentBeat, idx, total);
                    if (bodyText != null) bodyText.maxVisibleCharacters = 0;
                    yield return FadeCanvasGroup(slideBodyCanvasGroup, 0f, 1f, slideTransitionDuration);
                }
                else
                {
                    PrepareSlideVisuals(currentBeat, 0, total);
                }

                HanUIAudioManager.PlayPaperRustle();

                // Typewrite this slide
                yield return TypewriteSlideRoutine(currentBeat.bodyText);

                if (_skipRequested) break;

                // Show ready state on Continue prompt and wait for user to advance
                UpdateContinuePromptReady(idx >= total - 1);

                while (!_advanceRequested && !_skipRequested)
                {
                    PulseContinuePrompt();
                    yield return null;
                }

                if (!_skipRequested)
                {
                    HanUIAudioManager.PlayStampClick();
                }
            }

            if (rootCanvasGroup != null)
            {
                yield return FadeCanvasGroup(rootCanvasGroup, rootCanvasGroup.alpha, 0f, panelFadeOutDuration);
                rootCanvasGroup.blocksRaycasts = false;
                rootCanvasGroup.interactable = false;
            }

            _activeSlideIndex = -1;
            IsPlaying = false;
            gameObject.SetActive(false);
        }

        private void EnsureDefaultBeats()
        {
            if (useLocalizedDefaultBeats || narrativeBeats == null || narrativeBeats.Length == 0)
            {
                narrativeBeats = HanDialogueCatalog.DefaultIntroBeats;
            }
        }

        private void ApplyLocalizedChromeLabels()
        {
            if (globalHeaderText != null)
            {
                globalHeaderText.text = HanLocalization.Pick(
                    "ANCUTZA'S INN  ·  OLD WALLACHIA",
                    "HANUL ANCUTZEI  ·  VECHEA VALAHIE",
                    "ANCUTZA FOGADÓJA  ·  RÉGI HAVASALFÖLD");
            }

            if (continueHintText != null)
            {
                continueHintText.text = HanLocalization.Pick(
                    "[SPACE] or [CLICK] to finish / continue   ·   [ESC] to skip",
                    "[SPACE] sau [CLICK] pentru a grăbi / continua   ·   [ESC] pentru a sări",
                    "[SPACE] vagy [KATTINTÁS] a folytatáshoz   ·   [ESC] átugrás");
            }

            if (skipButton != null)
            {
                TMP_Text skipTmp = skipButton.GetComponentInChildren<TMP_Text>(true);
                if (skipTmp != null)
                {
                    skipTmp.text = HanLocalization.Pick(
                        "SKIP INTRO  [ESC]",
                        "OMITE INTRO  [ESC]",
                        "KIHAGYÁS  [ESC]");
                }
            }
        }

        private void DisableLegacyVideoComponents()
        {
            var vp = GetComponentInChildren<VideoPlayer>(true);
            if (vp != null)
            {
                if (vp.isPlaying) vp.Stop();
                vp.enabled = false;
            }

            var rawImg = transform.Find("VideoRawImage");
            if (rawImg != null) rawImg.gameObject.SetActive(false);

            var legacySkip = transform.Find("SkipVideoButton");
            if (legacySkip != null) legacySkip.gameObject.SetActive(false);
        }

        /// <summary>
        /// Ensures the complete Visual Novel UI hierarchy exists under this GameObject.
        /// Safe to call repeatedly in Edit mode or Play mode.
        /// </summary>
        public void EnsureVisualNovelUI()
        {
            Transform existingStage = transform.Find("VN_StageRoot");
            if (_uiBuilt && existingStage != null && bodyText != null && continueButton != null && skipButton != null)
            {
                return;
            }

            if (existingStage != null)
            {
                BindExistingChildren(existingStage);
                if (bodyText != null && continueButton != null && skipButton != null)
                {
                    _uiBuilt = true;
                    return;
                }
            }

            DisableLegacyVideoComponents();

            TMP_FontAsset font = HanUITheme.GetProjectFont();

            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (rootCanvasGroup == null) rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();

            // Root container stretching full screen
            GameObject stageObj = new GameObject("VN_StageRoot", typeof(RectTransform));
            stageObj.transform.SetParent(transform, false);
            RectTransform stageRt = stageObj.GetComponent<RectTransform>();
            StretchFull(stageRt);

            // 1. Rich Atmospheric Backdrop
            Image backdropImg = stageObj.AddComponent<Image>();
            backdropImg.color = new Color(0.065f, 0.052f, 0.048f, 1f);
            backdropAdvanceButton = stageObj.AddComponent<Button>();
            backdropAdvanceButton.transition = Selectable.Transition.None;

            // 2. Subtle Warm Candle Radial Glow behind Manuscript Card
            GameObject glowObj = new GameObject("VN_CandleGlow", typeof(RectTransform), typeof(Image));
            glowObj.transform.SetParent(stageObj.transform, false);
            candleGlowRect = glowObj.GetComponent<RectTransform>();
            candleGlowRect.anchorMin = new Vector2(0.5f, 0.5f);
            candleGlowRect.anchorMax = new Vector2(0.5f, 0.5f);
            candleGlowRect.pivot = new Vector2(0.5f, 0.5f);
            candleGlowRect.sizeDelta = new Vector2(1380f, 820f);
            candleGlowRect.anchoredPosition = new Vector2(0f, -10f);
            candleGlowImage = glowObj.GetComponent<Image>();
            candleGlowImage.sprite = GetRadialGlowSprite();
            candleGlowImage.color = new Color(0.68f, 0.34f, 0.12f, 0.24f);
            candleGlowImage.raycastTarget = false;

            // 3. Drifting Gold Embers / Dust Layer
            CreateEmberMotes(stageObj.transform);

            // 4. Subtle Vignette Grain Overlay
            GameObject vigObj = new GameObject("VN_VignetteGrain", typeof(RectTransform), typeof(Image));
            vigObj.transform.SetParent(stageObj.transform, false);
            StretchFull(vigObj.GetComponent<RectTransform>());
            Image vigImg = vigObj.GetComponent<Image>();
            vigImg.sprite = HanUITheme.GetVignetteGrainSprite();
            vigImg.color = new Color(1f, 1f, 1f, 0.62f);
            vigImg.raycastTarget = false;

            // 5. Top Chronicle Header Banner
            GameObject headerBanner = new GameObject("VN_TopBanner", typeof(RectTransform));
            headerBanner.transform.SetParent(stageObj.transform, false);
            RectTransform headerRt = headerBanner.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 0.5f);
            headerRt.anchorMax = new Vector2(0.5f, 0.5f);
            headerRt.pivot = new Vector2(0.5f, 0.5f);
            headerRt.sizeDelta = new Vector2(960f, 70f);
            headerRt.anchoredPosition = new Vector2(0f, 305f);

            GameObject globalHdrObj = new GameObject("Title", typeof(RectTransform));
            globalHdrObj.transform.SetParent(headerBanner.transform, false);
            RectTransform globalHdrRt = globalHdrObj.GetComponent<RectTransform>();
            StretchFull(globalHdrRt);
            globalHdrRt.offsetMin = new Vector2(0f, 20f);
            globalHeaderText = globalHdrObj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(globalHeaderText, font);
            globalHeaderText.text = "HANUL ESMERALDEI  ·  VECHEA VALAHIE";
            globalHeaderText.fontSize = 20f;
            globalHeaderText.characterSpacing = 6f;
            globalHeaderText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            globalHeaderText.alignment = TextAlignmentOptions.Center;
            globalHeaderText.color = HanUITheme.SubduedBrass;
            globalHeaderText.raycastTarget = false;

            GameObject topDivObj = new GameObject("TopDivider", typeof(RectTransform), typeof(Image));
            topDivObj.transform.SetParent(headerBanner.transform, false);
            RectTransform topDivRt = topDivObj.GetComponent<RectTransform>();
            topDivRt.anchorMin = new Vector2(0.5f, 0f);
            topDivRt.anchorMax = new Vector2(0.5f, 0f);
            topDivRt.pivot = new Vector2(0.5f, 0f);
            topDivRt.sizeDelta = new Vector2(540f, 8f);
            topDivRt.anchoredPosition = new Vector2(0f, 4f);
            Image topDivImg = topDivObj.GetComponent<Image>();
            topDivImg.sprite = HanUITheme.GetDividerOrnamentSprite();
            topDivImg.color = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.72f);
            topDivImg.raycastTarget = false;

            // 6. Central Archival Visual Novel Manuscript Card
            GameObject cardObj = new GameObject("VN_ManuscriptCard", typeof(RectTransform));
            cardObj.transform.SetParent(stageObj.transform, false);
            cardTransform = cardObj.GetComponent<RectTransform>();
            cardTransform.anchorMin = new Vector2(0.5f, 0.5f);
            cardTransform.anchorMax = new Vector2(0.5f, 0.5f);
            cardTransform.pivot = new Vector2(0.5f, 0.5f);
            cardTransform.sizeDelta = new Vector2(1060f, 460f);
            cardTransform.anchoredPosition = new Vector2(0f, -12f);

            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.sprite = HanUITheme.GetFramedCardSprite();
            cardBg.type = Image.Type.Sliced;
            cardBg.color = new Color(0.095f, 0.078f, 0.070f, 0.96f);

            cardAdvanceButton = cardObj.AddComponent<Button>();
            cardAdvanceButton.transition = Selectable.Transition.None;

            Shadow cardShadow = cardObj.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.03f, 0.02f, 0.015f, 0.85f);
            cardShadow.effectDistance = new Vector2(0f, -14f);

            // Double brass inner border frame
            GameObject borderObj = new GameObject("VN_CardDoubleBorder", typeof(RectTransform), typeof(Image));
            borderObj.transform.SetParent(cardObj.transform, false);
            RectTransform borderRt = borderObj.GetComponent<RectTransform>();
            StretchFull(borderRt);
            borderRt.offsetMin = new Vector2(10f, 10f);
            borderRt.offsetMax = new Vector2(-10f, -10f);
            Image borderImg = borderObj.GetComponent<Image>();
            borderImg.sprite = HanUITheme.GetDoubleBorderFrameSprite();
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.72f);
            borderImg.raycastTarget = false;

            CreateCornerFiligree(cardObj.transform);

            // Slide Content Container (for smooth fade transitions between narrative beats)
            GameObject bodyGroupObj = new GameObject("VN_SlideContent", typeof(RectTransform), typeof(CanvasGroup));
            bodyGroupObj.transform.SetParent(cardObj.transform, false);
            StretchFull(bodyGroupObj.GetComponent<RectTransform>());
            slideBodyCanvasGroup = bodyGroupObj.GetComponent<CanvasGroup>();

            // Chapter Tag Pill inside Top of Card
            GameObject tagPillObj = new GameObject("VN_ChapterTagPill", typeof(RectTransform), typeof(Image));
            tagPillObj.transform.SetParent(bodyGroupObj.transform, false);
            RectTransform tagPillRt = tagPillObj.GetComponent<RectTransform>();
            tagPillRt.anchorMin = new Vector2(0f, 1f);
            tagPillRt.anchorMax = new Vector2(0f, 1f);
            tagPillRt.pivot = new Vector2(0f, 1f);
            tagPillRt.anchoredPosition = new Vector2(54f, -34f);
            tagPillRt.sizeDelta = new Vector2(400f, 34f);
            Image tagPillImg = tagPillObj.GetComponent<Image>();
            tagPillImg.sprite = HanUITheme.GetButtonPlateSprite();
            tagPillImg.type = Image.Type.Sliced;
            tagPillImg.color = new Color(0.28f, 0.11f, 0.09f, 0.90f);
            tagPillImg.raycastTarget = false;

            GameObject chapterTextObj = new GameObject("VN_ChapterTagText", typeof(RectTransform));
            chapterTextObj.transform.SetParent(tagPillObj.transform, false);
            RectTransform chapterRt = chapterTextObj.GetComponent<RectTransform>();
            StretchFull(chapterRt);
            chapterRt.offsetMin = new Vector2(16f, 2f);
            chapterRt.offsetMax = new Vector2(-16f, -2f);
            chapterTagText = chapterTextObj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(chapterTagText, font);
            chapterTagText.fontSize = 14f;
            chapterTagText.characterSpacing = 3f;
            chapterTagText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            chapterTagText.alignment = TextAlignmentOptions.Left;
            chapterTagText.color = HanUITheme.BurnishedGold;
            chapterTagText.raycastTarget = false;

            // Thin horizontal brass line below chapter tag
            GameObject hdrRuleObj = new GameObject("VN_HeaderRule", typeof(RectTransform), typeof(Image));
            hdrRuleObj.transform.SetParent(cardObj.transform, false);
            RectTransform hdrRuleRt = hdrRuleObj.GetComponent<RectTransform>();
            hdrRuleRt.anchorMin = new Vector2(0f, 1f);
            hdrRuleRt.anchorMax = new Vector2(1f, 1f);
            hdrRuleRt.pivot = new Vector2(0.5f, 1f);
            hdrRuleRt.offsetMin = new Vector2(54f, -80f);
            hdrRuleRt.offsetMax = new Vector2(-54f, -78f);
            Image hdrRuleImg = hdrRuleObj.GetComponent<Image>();
            hdrRuleImg.color = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.35f);
            hdrRuleImg.raycastTarget = false;

            // Story Body TMP Text
            GameObject bodyTextObj = new GameObject("VN_StoryBodyText", typeof(RectTransform));
            bodyTextObj.transform.SetParent(bodyGroupObj.transform, false);
            RectTransform bodyRt = bodyTextObj.GetComponent<RectTransform>();
            StretchFull(bodyRt);
            bodyRt.offsetMin = new Vector2(56f, 92f);
            bodyRt.offsetMax = new Vector2(-56f, -96f);
            bodyText = bodyTextObj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(bodyText, font);
            bodyText.alignment = TextAlignmentOptions.TopLeft;
            bodyText.fontSize = 26f;
            bodyText.enableAutoSizing = true;
            bodyText.fontSizeMin = 20f;
            bodyText.fontSizeMax = 27f;
            bodyText.lineSpacing = 14f;
            bodyText.color = HanUITheme.WarmParchment;
            bodyText.raycastTarget = false;

            // Bottom Footer Bar inside Card
            GameObject footerRuleObj = new GameObject("VN_FooterRule", typeof(RectTransform), typeof(Image));
            footerRuleObj.transform.SetParent(cardObj.transform, false);
            RectTransform footerRuleRt = footerRuleObj.GetComponent<RectTransform>();
            footerRuleRt.anchorMin = new Vector2(0f, 0f);
            footerRuleRt.anchorMax = new Vector2(1f, 0f);
            footerRuleRt.pivot = new Vector2(0.5f, 0f);
            footerRuleRt.offsetMin = new Vector2(54f, 76f);
            footerRuleRt.offsetMax = new Vector2(-54f, 78f);
            Image footerRuleImg = footerRuleObj.GetComponent<Image>();
            footerRuleImg.color = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.28f);
            footerRuleImg.raycastTarget = false;

            // Progress Diamond Pips (Bottom-Left)
            int beatCount = narrativeBeats != null && narrativeBeats.Length > 0 ? narrativeBeats.Length : 4;
            progressPips = new Image[beatCount];
            for (int p = 0; p < beatCount; p++)
            {
                GameObject pipObj = new GameObject($"VN_Pip_{p}", typeof(RectTransform), typeof(Image));
                pipObj.transform.SetParent(cardObj.transform, false);
                RectTransform pipRt = pipObj.GetComponent<RectTransform>();
                pipRt.anchorMin = new Vector2(0f, 0f);
                pipRt.anchorMax = new Vector2(0f, 0f);
                pipRt.pivot = new Vector2(0.5f, 0.5f);
                pipRt.sizeDelta = new Vector2(14f, 14f);
                pipRt.anchoredPosition = new Vector2(62f + p * 24f, 44f);
                Image pipImg = pipObj.GetComponent<Image>();
                pipImg.sprite = GetDiamondPipSprite();
                pipImg.color = HanUITheme.MutedSlate;
                pipImg.raycastTarget = false;
                progressPips[p] = pipImg;
            }

            // Counter text next to pips
            GameObject counterObj = new GameObject("VN_SlideCounter", typeof(RectTransform));
            counterObj.transform.SetParent(cardObj.transform, false);
            RectTransform counterRt = counterObj.GetComponent<RectTransform>();
            counterRt.anchorMin = new Vector2(0f, 0f);
            counterRt.anchorMax = new Vector2(0f, 0f);
            counterRt.pivot = new Vector2(0f, 0.5f);
            counterRt.anchoredPosition = new Vector2(68f + beatCount * 24f, 44f);
            counterRt.sizeDelta = new Vector2(120f, 26f);
            slideCounterText = counterObj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(slideCounterText, font);
            slideCounterText.fontSize = 14f;
            slideCounterText.alignment = TextAlignmentOptions.Left;
            slideCounterText.color = HanUITheme.MutedSlate;
            slideCounterText.raycastTarget = false;

            // Continue Button (Bottom-Right)
            continueButton = CreateStyledButton(
                cardObj.transform,
                "VN_ContinueButton",
                HanLocalization.Pick("CONTINUE  >>", "CONTINUĂ  >>", "TOVÁBB  >>"),
                new Vector2(256f, 46f),
                new Vector2(1f, 0f),
                new Vector2(1f, 0f),
                new Vector2(-54f, 20f),
                font,
                HanUITheme.StampCrimson,
                HanUITheme.BurnishedGold,
                out continueButtonText);
            if (continueButtonText != null)
            {
                continueButtonText.enableAutoSizing = true;
                continueButtonText.fontSizeMin = 11f;
                continueButtonText.fontSizeMax = 16f;
            }

            // Subtle Control Hint centered under card
            GameObject hintObj = new GameObject("VN_ControlHint", typeof(RectTransform));
            hintObj.transform.SetParent(stageObj.transform, false);
            RectTransform hintRt = hintObj.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0.5f);
            hintRt.anchorMax = new Vector2(0.5f, 0.5f);
            hintRt.pivot = new Vector2(0.5f, 0.5f);
            hintRt.sizeDelta = new Vector2(860f, 34f);
            hintRt.anchoredPosition = new Vector2(0f, -278f);
            continueHintText = hintObj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(continueHintText, font);
            continueHintText.fontSize = 14.5f;
            continueHintText.enableAutoSizing = true;
            continueHintText.fontSizeMin = 10.5f;
            continueHintText.fontSizeMax = 14.5f;
            continueHintText.characterSpacing = 1.8f;
            continueHintText.alignment = TextAlignmentOptions.Center;
            continueHintText.color = new Color(HanUITheme.AgedPaper.r, HanUITheme.AgedPaper.g, HanUITheme.AgedPaper.b, 0.65f);
            continueHintText.text = HanLocalization.Pick(
                "[SPACE] or [CLICK] to finish / continue   ·   [ESC] to skip",
                "[SPACE] sau [CLICK] pentru a grăbi / continua   ·   [ESC] pentru a sări",
                "[SPACE] vagy [KATTINTÁS] a folytatáshoz   ·   [ESC] átugrás");
            continueHintText.raycastTarget = false;

            // Top-Right Skip Button
            skipButton = CreateStyledButton(
                stageObj.transform,
                "VN_SkipButton",
                HanLocalization.Pick("SKIP INTRO  [ESC]", "OMITE INTRO  [ESC]", "KIHAGYÁS  [ESC]"),
                new Vector2(240f, 44f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-42f, -36f),
                font,
                HanUITheme.CharcoalSurface,
                HanUITheme.AgedPaper,
                out TMP_Text skipBtnTxt);
            if (skipBtnTxt != null)
            {
                skipBtnTxt.enableAutoSizing = true;
                skipBtnTxt.fontSizeMin = 10.5f;
                skipBtnTxt.fontSizeMax = 15f;
            }

            _uiBuilt = true;
        }

        private void BindExistingChildren(Transform stage)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
            backdropAdvanceButton = stage.GetComponent<Button>();
            candleGlowRect = stage.Find("VN_CandleGlow") as RectTransform;
            if (candleGlowRect != null) candleGlowImage = candleGlowRect.GetComponent<Image>();
            globalHeaderText = stage.Find("VN_TopBanner/Title")?.GetComponent<TMP_Text>();
            continueHintText = stage.Find("VN_ControlHint")?.GetComponent<TMP_Text>();
            skipButton = stage.Find("VN_SkipButton")?.GetComponent<Button>();

            Transform card = stage.Find("VN_ManuscriptCard");
            if (card != null)
            {
                cardTransform = card as RectTransform;
                cardAdvanceButton = card.GetComponent<Button>();
                Transform content = card.Find("VN_SlideContent");
                if (content != null)
                {
                    slideBodyCanvasGroup = content.GetComponent<CanvasGroup>();
                    chapterTagText = content.Find("VN_ChapterTagPill/VN_ChapterTagText")?.GetComponent<TMP_Text>();
                    bodyText = content.Find("VN_StoryBodyText")?.GetComponent<TMP_Text>();
                }
                slideCounterText = card.Find("VN_SlideCounter")?.GetComponent<TMP_Text>();
                continueButton = card.Find("VN_ContinueButton")?.GetComponent<Button>();
                if (continueButton != null)
                {
                    continueButtonText = continueButton.GetComponentInChildren<TMP_Text>(true);
                }

                var pips = new List<Image>();
                for (int i = 0; i < card.childCount; i++)
                {
                    Transform ch = card.GetChild(i);
                    if (ch.name.StartsWith("VN_Pip_", StringComparison.Ordinal))
                    {
                        var img = ch.GetComponent<Image>();
                        if (img != null) pips.Add(img);
                    }
                }
                if (pips.Count > 0) progressPips = pips.ToArray();
            }
        }

        private void PrepareSlideVisuals(IntroNarrativeBeat beat, int index, int total)
        {
            if (chapterTagText != null)
            {
                chapterTagText.text = string.IsNullOrWhiteSpace(beat.chapterTag)
                    ? HanLocalization.Pick($"PROLOGUE · {index + 1}", $"PROLOG · {index + 1}", $"PROLÓGUS · {index + 1}")
                    : beat.chapterTag;
            }

            if (bodyText != null)
            {
                bodyText.text = beat.bodyText ?? "";
                bodyText.maxVisibleCharacters = 0;
                bodyText.ForceMeshUpdate();
            }

            if (slideCounterText != null)
            {
                slideCounterText.text = $"{index + 1} / {total}";
            }

            if (progressPips != null)
            {
                for (int i = 0; i < progressPips.Length; i++)
                {
                    if (progressPips[i] == null) continue;
                    if (i == index)
                    {
                        progressPips[i].color = HanUITheme.BurnishedGold;
                        progressPips[i].rectTransform.localScale = Vector3.one * 1.28f;
                    }
                    else if (i < index)
                    {
                        progressPips[i].color = HanUITheme.SubduedBrass;
                        progressPips[i].rectTransform.localScale = Vector3.one;
                    }
                    else
                    {
                        progressPips[i].color = HanUITheme.MutedSlate;
                        progressPips[i].rectTransform.localScale = Vector3.one * 0.85f;
                    }
                }
            }

            if (continueButtonText != null)
            {
                continueButtonText.text = HanLocalization.Pick("WAIT...", "AȘTEAPTĂ...", "VÁRJ...");
                continueButtonText.color = HanUITheme.MutedSlate;
            }

            if (HanUIFocusNavigator.Instance != null && continueButton != null)
            {
                HanUIFocusNavigator.Instance.FocusSelectable(continueButton);
            }
        }

        private IEnumerator TypewriteSlideRoutine(string fullText)
        {
            _slideIsTyping = true;

            if (bodyText == null || string.IsNullOrEmpty(fullText))
            {
                _slideIsTyping = false;
                yield break;
            }

            bodyText.text = fullText;
            bodyText.maxVisibleCharacters = 0;
            bodyText.ForceMeshUpdate();

            int totalChars = bodyText.textInfo.characterCount;
            if (totalChars <= 0)
            {
                bodyText.maxVisibleCharacters = 99999;
                _slideIsTyping = false;
                yield break;
            }

            int revealedCount = 0;
            int lastTickChar = -Mathf.Max(1, charsPerSoundTick);
            float timer = 0f;

            while (revealedCount < totalChars && !_skipRequested && !_completeCurrentSlideRequested)
            {
                timer -= Time.unscaledDeltaTime;

                float cadenceScale = scaleWithGlobalAudioCadence ? HanUIAudioManager.GetConfiguredCadenceMultiplier() : 1f;
                float effectiveCps = Mathf.Max(8f, charsPerSecond);
                float baseDelay = 1f / effectiveCps;

                while (revealedCount < totalChars && timer <= 0f && !_skipRequested && !_completeCurrentSlideRequested)
                {
                    char c = revealedCount < bodyText.textInfo.characterInfo.Length
                        ? bodyText.textInfo.characterInfo[revealedCount].character
                        : '\0';

                    revealedCount++;
                    bodyText.maxVisibleCharacters = revealedCount;

                    int tickStep = scaleWithGlobalAudioCadence
                        ? HanUIAudioManager.GetConfiguredCharsPerTick(charsPerSoundTick)
                        : Mathf.Clamp(charsPerSoundTick, 1, 8);

                    if (!char.IsWhiteSpace(c) && (revealedCount - lastTickChar >= tickStep))
                    {
                        lastTickChar = revealedCount;
                        float tickInterval = minSoundInterval / Mathf.Max(0.25f, cadenceScale);
                        HanUIAudioManager.PlayTypewriterTick(c, soundPitchScale, soundVolumeScale, tickInterval);
                    }

                    timer += baseDelay * GetPunctuationDelayMultiplier(c);
                }

                yield return null;
            }

            bodyText.maxVisibleCharacters = 99999;
            _slideIsTyping = false;
        }

        private float GetPunctuationDelayMultiplier(char c)
        {
            if (c == '.' || c == '!' || c == '?' || c == '…' || c == '—')
                return Mathf.Max(1f, sentencePunctuationPause);

            if (c == ',' || c == ';' || c == ':')
                return Mathf.Max(1f, clausePunctuationPause);

            return UnityEngine.Random.Range(0.88f, 1.12f);
        }

        private void UpdateContinuePromptReady(bool isLastSlide)
        {
            if (continueButtonText == null) return;
            continueButtonText.text = isLastSlide
                ? HanLocalization.Pick("BEGIN STORY  >>", "ÎNCEPE POVESTEA  >>", "TÖRTÉNET KEZDÉSE  >>")
                : HanLocalization.Pick("CONTINUE  >>", "CONTINUĂ  >>", "TOVÁBB  >>");
            continueButtonText.color = HanUITheme.BurnishedGold;
        }

        private void PulseContinuePrompt()
        {
            if (continueButtonText == null) return;
            float alpha = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 4.2f);
            Color c = HanUITheme.BurnishedGold;
            c.a = alpha;
            continueButtonText.color = c;
        }

        private void PollIntroInput()
        {
            bool escapePressed = false;
            bool advancePressed = false;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.escapeKey.wasPressedThisFrame)
                    escapePressed = true;
                else if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                         Keyboard.current.enterKey.wasPressedThisFrame ||
                         Keyboard.current.numpadEnterKey.wasPressedThisFrame)
                    advancePressed = true;
            }

            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonEast.wasPressedThisFrame)
                    escapePressed = true;
                else if (Gamepad.current.buttonSouth.wasPressedThisFrame)
                    advancePressed = true;
            }

            if (escapePressed)
            {
                if (skipButton != null)
                {
                    skipButton.onClick.Invoke();
                }
                else
                {
                    RequestSkip();
                }
                return;
            }

            if (advancePressed)
            {
                RequestAdvanceOrComplete();
            }
        }

        private void UpdateAmbientAtmosphere()
        {
            float t = Time.unscaledTime;

            if (candleGlowImage != null && candleGlowRect != null)
            {
                float flicker = 0.20f + 0.055f * Mathf.Sin(t * 2.1f) + 0.025f * Mathf.Sin(t * 6.7f);
                candleGlowImage.color = new Color(0.70f, 0.35f, 0.12f, flicker);
                float scale = 1f + 0.03f * Mathf.Sin(t * 1.4f);
                candleGlowRect.localScale = new Vector3(scale, scale, 1f);
            }

            for (int i = 0; i < _emberRects.Count; i++)
            {
                RectTransform rt = _emberRects[i];
                if (rt == null) continue;
                Vector4 p = _emberParams[i];
                Vector2 pos = rt.anchoredPosition;
                pos.y += p.x * Time.unscaledDeltaTime;
                pos.x += Mathf.Sin(t * 1.2f + p.z) * p.y * Time.unscaledDeltaTime;
                if (pos.y > 560f)
                {
                    pos.y = -560f;
                    pos.x = UnityEngine.Random.Range(-880f, 880f);
                }
                rt.anchoredPosition = pos;

                Image img = _emberImages[i];
                if (img != null)
                {
                    Color col = img.color;
                    col.a = p.w * (0.55f + 0.45f * Mathf.Sin(t * 2.4f + p.z));
                    img.color = col;
                }
            }
        }

        private void CreateEmberMotes(Transform parent)
        {
            _emberRects.Clear();
            _emberImages.Clear();
            _emberParams.Clear();

            const int moteCount = 14;
            for (int i = 0; i < moteCount; i++)
            {
                GameObject mote = new GameObject($"VN_Ember_{i}", typeof(RectTransform), typeof(Image));
                mote.transform.SetParent(parent, false);
                RectTransform rt = mote.GetComponent<RectTransform>();
                float size = UnityEngine.Random.Range(4f, 10f);
                rt.sizeDelta = new Vector2(size, size);
                rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-860f, 860f), UnityEngine.Random.Range(-520f, 520f));

                Image img = mote.GetComponent<Image>();
                img.sprite = GetRadialGlowSprite();
                float baseAlpha = UnityEngine.Random.Range(0.14f, 0.36f);
                img.color = new Color(0.92f, 0.68f, 0.28f, baseAlpha);
                img.raycastTarget = false;

                _emberRects.Add(rt);
                _emberImages.Add(img);
                _emberParams.Add(new Vector4(
                    UnityEngine.Random.Range(14f, 34f),
                    UnityEngine.Random.Range(8f, 22f),
                    UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                    baseAlpha));
            }
        }

        private void CreateCornerFiligree(Transform cardParent)
        {
            Sprite corner = GetCornerFiligreeSprite();
            Vector2[] anchors =
            {
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 0f)
            };
            Vector2[] offsets =
            {
                new Vector2(18f, -18f),
                new Vector2(-18f, -18f),
                new Vector2(18f, 18f),
                new Vector2(-18f, 18f)
            };
            Vector3[] scales =
            {
                new Vector3(1f, 1f, 1f),
                new Vector3(-1f, 1f, 1f),
                new Vector3(1f, -1f, 1f),
                new Vector3(-1f, -1f, 1f)
            };

            for (int i = 0; i < 4; i++)
            {
                GameObject cObj = new GameObject($"VN_Corner_{i}", typeof(RectTransform), typeof(Image));
                cObj.transform.SetParent(cardParent, false);
                RectTransform cRt = cObj.GetComponent<RectTransform>();
                cRt.anchorMin = anchors[i];
                cRt.anchorMax = anchors[i];
                cRt.pivot = new Vector2(0.5f, 0.5f);
                cRt.sizeDelta = new Vector2(28f, 28f);
                cRt.anchoredPosition = offsets[i];
                cRt.localScale = scales[i];

                Image cImg = cObj.GetComponent<Image>();
                cImg.sprite = corner;
                cImg.color = HanUITheme.BurnishedGold;
                cImg.raycastTarget = false;
            }
        }

        private static Button CreateStyledButton(
            Transform parent,
            string name,
            string label,
            Vector2 size,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPos,
            TMP_FontAsset font,
            Color plateColor,
            Color textColor,
            out TMP_Text labelTmp)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(HanTactileButton));
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            Image img = btnObj.GetComponent<Image>();
            img.sprite = HanUITheme.GetButtonPlateSprite();
            img.type = Image.Type.Sliced;
            img.color = plateColor;

            Shadow btnShadow = btnObj.AddComponent<Shadow>();
            btnShadow.effectColor = new Color(0.03f, 0.02f, 0.015f, 0.85f);
            btnShadow.effectDistance = new Vector2(2f, -3.5f);

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;

            GameObject lblObj = new GameObject("Label", typeof(RectTransform));
            lblObj.transform.SetParent(btnObj.transform, false);
            StretchFull(lblObj.GetComponent<RectTransform>());
            labelTmp = lblObj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(labelTmp, font);
            labelTmp.text = label;
            labelTmp.fontSize = 15.5f;
            labelTmp.fontStyle = FontStyles.Bold;
            labelTmp.alignment = TextAlignmentOptions.Center;
            labelTmp.color = textColor;
            labelTmp.raycastTarget = false;

            bool isPrimary = plateColor == HanUITheme.StampCrimson;
            HanTactileButton tactile = btnObj.GetComponent<HanTactileButton>();
            tactile.ConfigureStyle(
                isPrimary,
                img,
                null,
                null,
                null,
                labelTmp,
                null,
                btnShadow,
                size.x * 0.72f);

            return btn;
        }

        private static IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
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
                cg.alpha = Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            cg.alpha = to;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Sprite GetRadialGlowSprite()
        {
            if (_radialGlowSprite != null) return _radialGlowSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VN_RadialGlow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float falloff = Mathf.Clamp01(1f - dist);
                    falloff = falloff * falloff * (3f - 2f * falloff);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, falloff));
                }
            }
            tex.Apply(false, false);
            _radialGlowSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _radialGlowSprite;
        }

        private static Sprite GetDiamondPipSprite()
        {
            if (_diamondPipSprite != null) return _diamondPipSprite;

            const int size = 24;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VN_DiamondPip",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Abs(x - half) + Mathf.Abs(y - half);
                    float a = Mathf.Clamp01((half * 0.85f - d) * 1.4f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply(false, false);
            _diamondPipSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _diamondPipSprite;
        }

        private static Sprite GetCornerFiligreeSprite()
        {
            if (_cornerFiligreeSprite != null) return _cornerFiligreeSprite;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "VN_CornerFiligree",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Draws top-left ornamental corner bracket
                    bool topBar = y >= size - 3 && x <= size - 6;
                    bool leftBar = x <= 2 && y >= 6;
                    bool innerStep = (y == size - 6 && x >= 4 && x <= 14) || (x == 5 && y >= 16 && y <= size - 6);
                    bool cornerDot = (x >= 4 && x <= 6 && y >= size - 7 && y <= size - 5);
                    tex.SetPixel(x, y, (topBar || leftBar || innerStep || cornerDot) ? Color.white : Color.clear);
                }
            }
            tex.Apply(false, false);
            _cornerFiligreeSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _cornerFiligreeSprite;
        }
    }
}
