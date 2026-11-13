using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Han.CheckIn
{
    /// <summary>
    /// Centralized Visual Style & Theme configuration for the vintage atmospheric / bureaucratic desk thriller aesthetic.
    /// Provides archival ledger colors (no #000000 pure black), crisp TextMeshPro SDF standardization,
    /// and procedural 9-sliced borders, ornamental dividers, and paper grain / vignette sprites.
    /// </summary>
    [CreateAssetMenu(fileName = "HanUITheme", menuName = "Han/UI Theme Config", order = 2)]
    public class HanUITheme : ScriptableObject
    {
        // ------------------------------------------------------------------
        //  Static Archival Ledger Palette (Never uses #000000 pure black)
        // ------------------------------------------------------------------
        /// <summary>Warm Parchment (#F2E7D1) - Primary readable text & light ledger surface.</summary>
        public static readonly Color WarmParchment = new Color(0.949f, 0.906f, 0.820f, 1.00f);

        /// <summary>Aged Paper (#DFCFAF) - Secondary labels, subtitles, and worn paper cards.</summary>
        public static readonly Color AgedPaper = new Color(0.875f, 0.812f, 0.686f, 0.95f);

        /// <summary>Muted Slate (#272B30) - Secondary button plates & bureaucratic backing.</summary>
        public static readonly Color MutedSlate = new Color(0.153f, 0.169f, 0.188f, 0.96f);

        /// <summary>Deep Sepia Ink (#1E1813) - Primary dark card surface & dark ink (replaces #000000).</summary>
        public static readonly Color DeepSepiaInk = new Color(0.118f, 0.094f, 0.075f, 0.97f);

        /// <summary>Charcoal Espresso (#251E19) - Elevated modal panel surface.</summary>
        public static readonly Color CharcoalSurface = new Color(0.145f, 0.118f, 0.098f, 0.96f);

        /// <summary>Modal Backdrop Dim (#14100D) - Deep warm sepia dim overlay.</summary>
        public static readonly Color ModalBackdropDim = new Color(0.078f, 0.063f, 0.051f, 0.86f);

        /// <summary>Subdued Brass (#C89D54) - Borders, dividers, and active accents.</summary>
        public static readonly Color SubduedBrass = new Color(0.784f, 0.616f, 0.329f, 1.00f);

        /// <summary>Burnished Gold (#E6C17A) - Highlighted / Hovered / Selected state accent.</summary>
        public static readonly Color BurnishedGold = new Color(0.902f, 0.757f, 0.478f, 1.00f);

        /// <summary>Wax Stamp Crimson (#8A2B21) - Primary CTA plates & stamp highlights.</summary>
        public static readonly Color StampCrimson = new Color(0.541f, 0.169f, 0.129f, 0.98f);

        /// <summary>Pressed Crimson/Brass (#6B2018) - Depressed tactile button state.</summary>
        public static readonly Color PressedCrimson = new Color(0.420f, 0.125f, 0.094f, 1.00f);

        /// <summary>Hovered Slate/Sepia (#342A22) - Hovered secondary button plate.</summary>
        public static readonly Color HoveredSlatePlate = new Color(0.204f, 0.165f, 0.133f, 0.98f);

        /// <summary>Disabled Ink (#6E655B) - Muted low-contrast state for disabled controls.</summary>
        public static readonly Color DisabledInk = new Color(0.431f, 0.396f, 0.357f, 0.50f);

        /// <summary>Disabled Plate (#1B1715) - Muted backing for disabled buttons.</summary>
        public static readonly Color DisabledPlate = new Color(0.106f, 0.090f, 0.082f, 0.65f);

        // ------------------------------------------------------------------
        //  Inspector Customizable Theme Fields
        // ------------------------------------------------------------------
        [Header("Archival Ledger Palette")]
        public Color primaryTextParchment = WarmParchment;
        public Color secondaryTextPaper = AgedPaper;
        public Color deepSepiaBackground = DeepSepiaInk;
        public Color charcoalModalSurface = CharcoalSurface;
        public Color mutedSlatePlate = MutedSlate;
        public Color brassAccent = SubduedBrass;
        public Color goldHighlight = BurnishedGold;
        public Color crimsonStamp = StampCrimson;
        public Color disabledColor = DisabledInk;

        [Header("Typography & SDF Crispness")]
        public TMP_FontAsset primaryFont;
        [Range(0f, 0.15f)] public float sdfFaceDilate = 0.03f;
        [Range(0f, 0.2f)] public float sdfOutlineWidth = 0.0f;

        // ------------------------------------------------------------------
        //  Cached Procedural Sprites
        // ------------------------------------------------------------------
        private static Sprite _framedCardSprite;
        private static Sprite _doubleBorderFrameSprite;
        private static Sprite _buttonPlateSprite;
        private static Sprite _vignetteGrainSprite;
        private static Sprite _dividerOrnamentSprite;
        private static TMP_FontAsset _cachedFont;
        private static Texture2D _imguiCardTex;
        private static Texture2D _imguiPillTex;
        private static Texture2D _imguiButtonNormTex;
        private static Texture2D _imguiButtonHoverTex;
        private static Texture2D _imguiButtonActiveTex;
        private static Texture2D _imguiCrimsonNormTex;
        private static Texture2D _imguiCrimsonHoverTex;

        /// <summary>
        /// Computes the exact scale factor used by 1920x1080 ScreenSpaceOverlay CanvasScalers
        /// with matchWidthOrHeight = 0.5f so IMGUI overlays scale identically with uGUI in WebGL & fullscreen.
        /// </summary>
        public static float GetMatchedReferenceScale()
        {
            float wRatio = Mathf.Max(0.25f, Screen.width / 1920f);
            float hRatio = Mathf.Max(0.25f, Screen.height / 1080f);
            return Mathf.Pow(wRatio, 0.5f) * Mathf.Pow(hRatio, 0.5f);
        }

        /// <summary>
        /// Resolves the project's primary TextMeshPro SDF font asset.
        /// </summary>
        public static TMP_FontAsset GetProjectFont()
        {
            if (_cachedFont != null) return _cachedFont;

            HanUITheme themeAsset = Resources.Load<HanUITheme>("HanUITheme");
            if (themeAsset != null && themeAsset.primaryFont != null)
            {
                _cachedFont = themeAsset.primaryFont;
            }

#if UNITY_EDITOR
            if (_cachedFont == null)
                _cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Minecraft-Regular SDF.asset");
            if (_cachedFont == null)
                _cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
#endif
            if (_cachedFont == null)
                _cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (_cachedFont == null)
                _cachedFont = TMP_Settings.defaultFontAsset;

            if (_cachedFont != null && (_cachedFont.fallbackFontAssetTable == null || _cachedFont.fallbackFontAssetTable.Count == 0))
            {
                TMP_FontAsset fallback = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
                if (fallback != null && fallback != _cachedFont)
                {
                    if (_cachedFont.fallbackFontAssetTable == null)
                        _cachedFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                    _cachedFont.fallbackFontAssetTable.Add(fallback);
                }
            }

            return _cachedFont;
        }

        /// <summary>
        /// Resolves a dynamic Font usable by Unity IMGUI styles that supports Romanian and Hungarian diacritics.
        /// </summary>
        public static Font GetProjectImGuiFont()
        {
            TMP_FontAsset tmpFont = GetProjectFont();
            if (tmpFont != null && tmpFont.sourceFontFile != null)
                return tmpFont.sourceFontFile;
            return null;
        }

        /// <summary>
        /// Applies crisp SDF rendering settings to any TMP_Text component to eliminate blurriness across all resolutions.
        /// </summary>
        public static void ApplyCrispSDF(TMP_Text tmp, TMP_FontAsset customFont = null)
        {
            if (tmp == null) return;

            TMP_FontAsset font = customFont != null ? customFont : GetProjectFont();
            if (font != null)
            {
                tmp.font = font;
            }

            tmp.extraPadding = true;
            tmp.textWrappingMode = TextWrappingModes.Normal;

            if (tmp.fontSharedMaterial != null)
            {
                // Ensure zero outline softness so SDF edges remain razor-sharp at any scale
                if (tmp.fontSharedMaterial.HasProperty(ShaderUtilities.ID_OutlineSoftness))
                    tmp.fontSharedMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
            }
        }

        /// <summary>
        /// Generates a 9-sliced archival card sprite with a double-ruled border and subtle paper grain.
        /// </summary>
        public static Sprite GetFramedCardSprite()
        {
            if (_framedCardSprite != null) return _framedCardSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Han_FramedCard_9Slice",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int distEdge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));

                    // Subtle organic paper grain (deterministic hash so no allocations)
                    float grain = (((x * 73856093 ^ y * 19349663) & 0xFF) / 255f - 0.5f) * 0.035f;

                    Color c;
                    if (distEdge == 0 || distEdge == 1)
                    {
                        // Outer brass rule
                        c = new Color(0.82f + grain, 0.66f + grain, 0.38f, 1f);
                    }
                    else if (distEdge == 4)
                    {
                        // Inner hairline rule
                        c = new Color(0.68f + grain, 0.54f + grain, 0.32f, 0.65f);
                    }
                    else
                    {
                        // Interior base (white-neutral with subtle paper grain so Image.color tints cleanly)
                        float v = Mathf.Clamp01(0.97f + grain);
                        c = new Color(v, v, v, 1f);
                    }

                    // Corner ornament ticks
                    bool isCornerTick = (x <= 7 && y <= 7) && (x == 7 || y == 7) ||
                                        (x >= size - 8 && y <= 7) && (x == size - 8 || y == 7) ||
                                        (x <= 7 && y >= size - 8) && (x == 7 || y == size - 8) ||
                                        (x >= size - 8 && y >= size - 8) && (x == size - 8 || y == size - 8);
                    if (isCornerTick && distEdge > 1 && distEdge < 7)
                    {
                        c = new Color(0.86f, 0.70f, 0.42f, 0.85f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(false, !Application.isEditor);
            _framedCardSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10));
            return _framedCardSprite;
        }

        /// <summary>
        /// Generates a 9-sliced transparent frame with only the double brass rules (for inner panel borders).
        /// </summary>
        public static Sprite GetDoubleBorderFrameSprite()
        {
            if (_doubleBorderFrameSprite != null) return _doubleBorderFrameSprite;

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Han_DoubleBorder_9Slice",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int distEdge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    Color c = Color.clear;

                    if (distEdge == 0 || distEdge == 1)
                    {
                        c = new Color(1f, 1f, 1f, 0.95f);
                    }
                    else if (distEdge == 5)
                    {
                        c = new Color(1f, 1f, 1f, 0.42f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(false, !Application.isEditor);
            _doubleBorderFrameSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(8, 8, 8, 8));
            return _doubleBorderFrameSprite;
        }

        /// <summary>
        /// Generates a 9-sliced vintage button plate sprite with beveled top/bottom edges and subtle paper texture.
        /// </summary>
        public static Sprite GetButtonPlateSprite()
        {
            if (_buttonPlateSprite != null) return _buttonPlateSprite;

            const int w = 64;
            const int h = 32;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Han_ButtonPlate_9Slice",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool isBorder = x == 0 || x == w - 1 || y == 0 || y == h - 1;
                    float grain = (((x * 19349663 ^ y * 83492791) & 0xFF) / 255f - 0.5f) * 0.04f;
                    float verticalGrad = Mathf.Lerp(0.90f, 1.04f, (float)y / (h - 1));

                    Color c;
                    if (isBorder)
                    {
                        c = new Color(1f, 0.94f, 0.82f, 1f);
                    }
                    else if (y == h - 2)
                    {
                        // Subtle top specular highlight
                        c = new Color(1f, 0.98f, 0.92f, 1f);
                    }
                    else if (y == 1)
                    {
                        // Subtle bottom bevel shadow
                        c = new Color(0.75f, 0.72f, 0.68f, 1f);
                    }
                    else
                    {
                        float val = Mathf.Clamp01(verticalGrad + grain);
                        c = new Color(val, val, val, 1f);
                    }

                    tex.SetPixel(x, y, c);
                }
            }

            tex.Apply(false, !Application.isEditor);
            _buttonPlateSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 6, 6, 6));
            return _buttonPlateSprite;
        }

        /// <summary>
        /// Generates a subtle radial vignette + archival grain overlay sprite for modal windows and menus.
        /// </summary>
        public static Sprite GetVignetteGrainSprite()
        {
            if (_vignetteGrainSprite != null) return _vignetteGrainSprite;

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Han_VignetteGrain",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = center.magnitude;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float vignette = Mathf.SmoothStep(0.10f, 0.92f, dist);
                    float grain = (((x * 73856093 ^ y * 19349663) & 0xFF) / 255f) * 0.08f;

                    // Warm deep sepia vignette (never #000000)
                    float alpha = Mathf.Clamp01(vignette * 0.72f + grain);
                    tex.SetPixel(x, y, new Color(0.08f, 0.06f, 0.05f, alpha));
                }
            }

            tex.Apply(false, !Application.isEditor);
            _vignetteGrainSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _vignetteGrainSprite;
        }

        /// <summary>
        /// Generates a horizontal panel divider sprite with tapered ends and a centered diamond ornament.
        /// </summary>
        public static Sprite GetDividerOrnamentSprite()
        {
            if (_dividerOrnamentSprite != null) return _dividerOrnamentSprite;

            const int w = 256;
            const int h = 12;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Han_DividerOrnament",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float midX = (w - 1) * 0.5f;
            float midY = (h - 1) * 0.5f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x - midX);
                    float dy = Mathf.Abs(y - midY);

                    // Center diamond ornament
                    float diamond = dx * 0.55f + dy;
                    float alpha = 0f;

                    if (diamond <= 4.8f)
                    {
                        alpha = Mathf.Clamp01(1.3f - diamond / 4.8f);
                    }
                    else if (dy <= 1.1f)
                    {
                        // Tapered horizontal line
                        float horizontalFade = 1f - Mathf.Clamp01(dx / (midX * 0.96f));
                        alpha = Mathf.Pow(horizontalFade, 0.65f) * (1f - dy * 0.45f);
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
                }
            }

            tex.Apply(false, !Application.isEditor);
            _dividerOrnamentSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _dividerOrnamentSprite;
        }

        // ------------------------------------------------------------------
        //  Cached Archival IMGUI Textures (9-sliced via GUIStyle.border = 8)
        // ------------------------------------------------------------------
        public static Texture2D GetImGuiCardTexture()
        {
            if (_imguiCardTex == null)
            {
                _imguiCardTex = BuildImGuiPlateTexture(
                    DeepSepiaInk,
                    new Color(SubduedBrass.r, SubduedBrass.g, SubduedBrass.b, 0.90f),
                    new Color(SubduedBrass.r, SubduedBrass.g, SubduedBrass.b, 0.25f),
                    0,
                    Color.clear);
            }
            return _imguiCardTex;
        }

        public static Texture2D GetImGuiPillTexture()
        {
            if (_imguiPillTex == null)
            {
                _imguiPillTex = BuildImGuiPlateTexture(
                    new Color(0.086f, 0.068f, 0.055f, 0.88f),
                    new Color(SubduedBrass.r, SubduedBrass.g, SubduedBrass.b, 0.62f),
                    Color.clear,
                    3,
                    new Color(SubduedBrass.r, SubduedBrass.g, SubduedBrass.b, 0.95f));
            }
            return _imguiPillTex;
        }

        public static Texture2D GetImGuiButtonTexture(bool hovered, bool pressed, bool primaryCrimson)
        {
            if (primaryCrimson)
            {
                if (hovered || pressed)
                {
                    if (_imguiCrimsonHoverTex == null)
                    {
                        _imguiCrimsonHoverTex = BuildImGuiPlateTexture(
                            pressed ? PressedCrimson : new Color(0.62f, 0.20f, 0.15f, 0.98f),
                            BurnishedGold,
                            Color.clear,
                            5,
                            BurnishedGold);
                    }
                    return _imguiCrimsonHoverTex;
                }
                if (_imguiCrimsonNormTex == null)
                {
                    _imguiCrimsonNormTex = BuildImGuiPlateTexture(
                        StampCrimson,
                        new Color(SubduedBrass.r, SubduedBrass.g, SubduedBrass.b, 0.85f),
                        Color.clear,
                        5,
                        BurnishedGold);
                }
                return _imguiCrimsonNormTex;
            }

            if (pressed)
            {
                if (_imguiButtonActiveTex == null)
                {
                    _imguiButtonActiveTex = BuildImGuiPlateTexture(
                        PressedCrimson,
                        BurnishedGold,
                        Color.clear,
                        5,
                        BurnishedGold);
                }
                return _imguiButtonActiveTex;
            }

            if (hovered)
            {
                if (_imguiButtonHoverTex == null)
                {
                    _imguiButtonHoverTex = BuildImGuiPlateTexture(
                        HoveredSlatePlate,
                        BurnishedGold,
                        Color.clear,
                        5,
                        BurnishedGold);
                }
                return _imguiButtonHoverTex;
            }

            if (_imguiButtonNormTex == null)
            {
                _imguiButtonNormTex = BuildImGuiPlateTexture(
                    CharcoalSurface,
                    new Color(SubduedBrass.r, SubduedBrass.g, SubduedBrass.b, 0.72f),
                    Color.clear,
                    5,
                    SubduedBrass);
            }
            return _imguiButtonNormTex;
        }

        private static Texture2D BuildImGuiPlateTexture(
            Color fill,
            Color outerBorder,
            Color innerRule,
            int leftTabWidth,
            Color leftTabColor)
        {
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isOuter = x == 0 || x == size - 1 || y == 0 || y == size - 1;
                    bool isInner = innerRule.a > 0.01f &&
                                   (x == 4 || x == size - 5 || y == 4 || y == size - 5) &&
                                   x >= 4 && x <= size - 5 && y >= 4 && y <= size - 5;
                    bool isLeftTab = leftTabWidth > 0 && x >= 1 && x <= leftTabWidth && y >= 1 && y <= size - 2;

                    Color px = fill;
                    if (isLeftTab)
                        px = leftTabColor;
                    else if (isOuter)
                        px = outerBorder;
                    else if (isInner)
                        px = innerRule;

                    tex.SetPixel(x, y, px);
                }
            }

            tex.Apply(false, false);
            return tex;
        }
    }

    /// <summary>
    /// Archival 3-language segmented row (EN | RO | HU) integrated inside SettingsModalCard
    /// matching the geometry and visual grammar of MasterVolumeRow / DoorHighlightRow.
    /// </summary>
    public class HanSettingsLanguageRow : MonoBehaviour
    {
        public const string RowObjectName = "LanguageRow";

        [SerializeField] private TMP_Text rowLabel;
        [SerializeField] private Button englishButton;
        [SerializeField] private Button romanianButton;
        [SerializeField] private Button hungarianButton;

        public Action OnLanguagePicked;

        private bool _listenersBound;

        private void Awake()
        {
            BindSegmentButtons();
        }

        private void OnEnable()
        {
            BindSegmentButtons();
            HanLocalization.OnLanguageChanged -= HandleLanguageChanged;
            HanLocalization.OnLanguageChanged += HandleLanguageChanged;
            RefreshVisuals();
        }

        private void OnDisable()
        {
            HanLocalization.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged(HanLanguage _)
        {
            RefreshVisuals();
        }

        public void BindSegmentButtons()
        {
            ResolveReferences();
            if (_listenersBound) return;
            _listenersBound = true;

            if (englishButton != null)
                englishButton.onClick.AddListener(() => SelectLanguage(HanLanguage.English));
            if (romanianButton != null)
                romanianButton.onClick.AddListener(() => SelectLanguage(HanLanguage.Romanian));
            if (hungarianButton != null)
                hungarianButton.onClick.AddListener(() => SelectLanguage(HanLanguage.Hungarian));
        }

        private void SelectLanguage(HanLanguage lang)
        {
            OnLanguagePicked?.Invoke();
            if (HanLocalization.CurrentLanguage != lang)
            {
                HanLocalization.SetLanguage(lang);
            }
            else
            {
                RefreshVisuals();
            }
        }

        public void RefreshVisuals()
        {
            ResolveReferences();

            if (rowLabel != null)
            {
                rowLabel.text = HanLocalization.Get("settings.language");
                HanUITheme.ApplyCrispSDF(rowLabel, HanUITheme.GetProjectFont());
            }

            HanLanguage current = HanLocalization.CurrentLanguage;
            ApplySegmentVisual(englishButton, current == HanLanguage.English);
            ApplySegmentVisual(romanianButton, current == HanLanguage.Romanian);
            ApplySegmentVisual(hungarianButton, current == HanLanguage.Hungarian);
        }

        private void ResolveReferences()
        {
            if (rowLabel == null)
                rowLabel = transform.Find("Label")?.GetComponent<TMP_Text>();
            if (englishButton == null)
                englishButton = transform.Find("SegmentTrack/LangBtn_EN")?.GetComponent<Button>();
            if (romanianButton == null)
                romanianButton = transform.Find("SegmentTrack/LangBtn_RO")?.GetComponent<Button>();
            if (hungarianButton == null)
                hungarianButton = transform.Find("SegmentTrack/LangBtn_HU")?.GetComponent<Button>();
        }

        private static void ApplySegmentVisual(Button btn, bool active)
        {
            if (btn == null) return;

            Image plate = btn.GetComponent<Image>();
            if (plate != null)
            {
                plate.color = active ? HanUITheme.StampCrimson : HanUITheme.CharcoalSurface;
            }

            Outline outline = btn.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = active
                    ? HanUITheme.BurnishedGold
                    : new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.25f);
            }

            Transform barTr = btn.transform.Find("ActiveBar");
            if (barTr != null)
                barTr.gameObject.SetActive(active);

            TMP_Text txt = btn.GetComponentInChildren<TMP_Text>(true);
            if (txt != null)
            {
                txt.color = active ? HanUITheme.WarmParchment : HanUITheme.AgedPaper;
                txt.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }

            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = active ? new Color(1.08f, 1.05f, 1.0f, 1f) : new Color(1.28f, 1.18f, 1.05f, 1f);
            cb.pressedColor = new Color(0.85f, 0.80f, 0.74f, 1f);
            cb.selectedColor = cb.highlightedColor;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
        }

        /// <summary>
        /// Ensures an archival LanguageRow exists directly inside the given SettingsModalCard.
        /// Safe to call in both Edit Mode and Runtime Play Mode.
        /// </summary>
        public static HanSettingsLanguageRow EnsureOrCreate(
            Transform settingsCard,
            TMP_FontAsset font = null,
            Vector2? anchoredPos = null,
            Vector2? rowSize = null)
        {
            if (settingsCard == null) return null;

            font = font != null ? font : HanUITheme.GetProjectFont();
            Vector2 pos = anchoredPos ?? new Vector2(0f, -86f);
            Vector2 size = rowSize ?? new Vector2(510f, 50f);

            // Hide any legacy standalone SettingsLanguageButton if present
            Transform legacyBtn = settingsCard.Find("SettingsLanguageButton");
            if (legacyBtn != null && legacyBtn.gameObject.activeSelf)
            {
                legacyBtn.gameObject.SetActive(false);
            }

            Transform existingTr = settingsCard.Find(RowObjectName);
            GameObject rowObj;
            if (existingTr != null)
            {
                rowObj = existingTr.gameObject;
                if (rowObj.transform is RectTransform existingRt)
                {
                    existingRt.anchoredPosition = pos;
                    existingRt.sizeDelta = size;
                }
                HanSettingsLanguageRow existingComp = rowObj.GetComponent<HanSettingsLanguageRow>();
                if (existingComp == null) existingComp = rowObj.AddComponent<HanSettingsLanguageRow>();
                existingComp.RefreshVisuals();
                return existingComp;
            }

            rowObj = new GameObject(RowObjectName, typeof(RectTransform));
            rowObj.transform.SetParent(settingsCard, false);
            RectTransform rowRt = rowObj.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = pos;
            rowRt.sizeDelta = size;

            // Left Label matching MasterVolumeRow / DoorHighlightRow
            GameObject lblObj = new GameObject("Label", typeof(RectTransform));
            lblObj.transform.SetParent(rowObj.transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0.5f);
            lblRt.anchorMax = new Vector2(0.54f, 0.5f);
            lblRt.pivot = new Vector2(0f, 0.5f);
            lblRt.anchoredPosition = Vector2.zero;
            lblRt.sizeDelta = new Vector2(0f, 40f);

            TMP_Text lbl = lblObj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(lbl, font);
            lbl.text = HanLocalization.Get("settings.language");
            lbl.fontSize = 16f;
            lbl.enableAutoSizing = true;
            lbl.fontSizeMin = 11f;
            lbl.fontSizeMax = 16f;
            lbl.fontStyle = FontStyles.Bold;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;
            lbl.color = HanUITheme.AgedPaper;
            lbl.raycastTarget = false;

            // Right Segmented Track Container
            GameObject trackObj = new GameObject("SegmentTrack", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(HorizontalLayoutGroup));
            trackObj.transform.SetParent(rowObj.transform, false);
            RectTransform trackRt = trackObj.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0.56f, 0.5f);
            trackRt.anchorMax = new Vector2(1f, 0.5f);
            trackRt.pivot = new Vector2(0.5f, 0.5f);
            trackRt.anchoredPosition = Vector2.zero;
            trackRt.sizeDelta = new Vector2(0f, 40f);

            Image trackImg = trackObj.GetComponent<Image>();
            trackImg.sprite = HanUITheme.GetButtonPlateSprite();
            trackImg.type = Image.Type.Sliced;
            trackImg.color = HanUITheme.DeepSepiaInk;

            Outline trackOutline = trackObj.GetComponent<Outline>();
            trackOutline.effectColor = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.55f);
            trackOutline.effectDistance = new Vector2(1f, -1f);

            HorizontalLayoutGroup layout = trackObj.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            Button enBtn = CreateSegmentButton(trackObj.transform, "LangBtn_EN", "EN", font);
            Button roBtn = CreateSegmentButton(trackObj.transform, "LangBtn_RO", "RO", font);
            Button huBtn = CreateSegmentButton(trackObj.transform, "LangBtn_HU", "HU", font);

            HanSettingsLanguageRow comp = rowObj.AddComponent<HanSettingsLanguageRow>();
            comp.rowLabel = lbl;
            comp.englishButton = enBtn;
            comp.romanianButton = roBtn;
            comp.hungarianButton = huBtn;
            comp.BindSegmentButtons();
            comp.RefreshVisuals();
            return comp;
        }

        private static Button CreateSegmentButton(Transform parent, string objName, string codeText, TMP_FontAsset font)
        {
            GameObject btnObj = new GameObject(objName, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            Image plate = btnObj.GetComponent<Image>();
            plate.sprite = HanUITheme.GetButtonPlateSprite();
            plate.type = Image.Type.Sliced;
            plate.color = HanUITheme.CharcoalSurface;

            Outline outline = btnObj.GetComponent<Outline>();
            outline.effectColor = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.25f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button btn = btnObj.GetComponent<Button>();
            btn.targetGraphic = plate;
            btn.navigation = new Navigation { mode = Navigation.Mode.Automatic };

            GameObject barObj = new GameObject("ActiveBar", typeof(RectTransform), typeof(Image));
            barObj.transform.SetParent(btnObj.transform, false);
            RectTransform barRt = barObj.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.12f, 0f);
            barRt.anchorMax = new Vector2(0.88f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(0f, 2f);
            barRt.sizeDelta = new Vector2(0f, 2.5f);
            Image barImg = barObj.GetComponent<Image>();
            barImg.color = HanUITheme.BurnishedGold;
            barImg.raycastTarget = false;

            GameObject lblObj = new GameObject("Text", typeof(RectTransform));
            lblObj.transform.SetParent(btnObj.transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(2f, 2f);
            lblRt.offsetMax = new Vector2(-2f, -2f);

            TMP_Text txt = lblObj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(txt, font);
            txt.text = codeText;
            txt.fontSize = 15f;
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 10.5f;
            txt.fontSizeMax = 15.5f;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = HanUITheme.AgedPaper;
            txt.raycastTarget = false;

            return btn;
        }
    }
}
