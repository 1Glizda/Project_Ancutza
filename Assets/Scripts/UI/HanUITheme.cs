using UnityEngine;
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

        /// <summary>
        /// Resolves the project's primary TextMeshPro SDF font asset.
        /// </summary>
        public static TMP_FontAsset GetProjectFont()
        {
            if (_cachedFont != null) return _cachedFont;

#if UNITY_EDITOR
            _cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Minecraft-Regular SDF.asset");
            if (_cachedFont == null)
                _cachedFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
#endif
            if (_cachedFont == null)
                _cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

            if (_cachedFont == null)
                _cachedFont = TMP_Settings.defaultFontAsset;

            return _cachedFont;
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

            tex.Apply();
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

            tex.Apply();
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

            tex.Apply();
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

            tex.Apply();
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

            tex.Apply();
            _dividerOrnamentSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            return _dividerOrnamentSprite;
        }
    }
}
