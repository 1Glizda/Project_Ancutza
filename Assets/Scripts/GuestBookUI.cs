using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Han.CheckIn
{
    /// <summary>
    /// Archival Ledger UI & Typography Controller for the 3D Check-In Register Book.
    /// Features:
    /// - Stratified Z-depth separation & Layer 8 enforcement (eliminates all Z-fighting and submesh clipping).
    /// - High-resolution SDF TextMeshPro configuration with custom face dilation & crisp close-up anti-aliasing.
    /// - Rich, non-pure-black 1600s iron-gall & sepia ink color palette.
    /// - Rhythmic mechanical ink-typewriter animation with per-character vertex baseline settle & ink absorption.
    /// - Multi-stage photo reveal: instant shutter flash + physical drop/stamp animation with rotation variation & overshoot bounce.
    /// </summary>
    [ExecuteAlways]
    public class GuestBookUI : MonoBehaviour
    {
        [Header("Text Bindings (TextMeshPro)")]
        [SerializeField] private TMP_Text titleHeaderText;
        [SerializeField] private TMP_Text notesHeaderText;
        [SerializeField] private TMP_Text guestNameText;
        [SerializeField] private TMP_Text roomNumberText;
        [SerializeField] private TMP_Text arrivalTimeText;
        [SerializeField] private TMP_Text notesText;
        [SerializeField] private TMP_Text pageCounterText;

        [Header("Image / 2D Sprite Bindings")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private SpriteRenderer portraitRenderer;
        [SerializeField] private Sprite defaultSilhouetteSprite;
        [SerializeField] private Image horseIcon;
        [SerializeField] private SpriteRenderer horseIconRenderer;
        [SerializeField] private GameObject horseStabledBadge;
        [SerializeField] private GameObject checkedInStamp;

        [Header("Archival Ink Palette (Non-Pure-Black Iron-Gall & Sepia)")]
        [SerializeField] private Color headerInkColor = new Color(0.129f, 0.078f, 0.051f, 0.98f);       // #21140D Deep Iron-Gall
        [SerializeField] private Color primaryEntryInkColor = new Color(0.161f, 0.098f, 0.063f, 0.97f); // #291910 Rich Walnut Ink
        [SerializeField] private Color metadataInkColor = new Color(0.212f, 0.133f, 0.086f, 0.95f);     // #362216 Warm Sepia Ink
        [SerializeField] private Color stampOxbloodColor = new Color(0.455f, 0.090f, 0.071f, 0.92f);    // #741712 Wax/Seal Oxblood
        [SerializeField] private Color horsePresentColor = new Color(0.82f, 0.64f, 0.28f, 0.95f);       // Warm Brass Gold
        [SerializeField] private Color horseAbsentColor = new Color(0.36f, 0.26f, 0.18f, 0.22f);        // Faded Sepia Ghost

        [Header("High-Resolution SDF Typography Settings")]
        [Range(-0.1f, 0.3f)]
        [SerializeField] private float sdfFaceDilate = 0.08f;
        [Range(0f, 0.15f)]
        [SerializeField] private float sdfOutlineWidth = 0.025f;
        [Range(0f, 0.2f)]
        [SerializeField] private float sdfOutlineSoftness = 0.035f;

        [Header("Stratified Depth Offsets (Anti Z-Fighting)")]
        [Tooltip("Negative Z brings elements cleanly toward the camera in front of the +0.004m page bed.")]
        [SerializeField] private float portraitDepthZ = -0.0045f;
        [SerializeField] private float textDepthZ = -0.0065f;
        [SerializeField] private float stampDepthZ = -0.0085f;

        [Header("Ink-Typewriter & Photo Drop Animation Settings")]
        [SerializeField] private float charSettleDuration = 0.065f;
        [SerializeField] private float charSettleVerticalOffset = 0.0020f;
        [SerializeField] private float charSettleInitialScale = 1.18f;
        [SerializeField] private float photoDropDuration = 0.16f;
        [SerializeField] private float photoBounceDuration = 0.20f;
        [SerializeField] private float photoMaxTiltDegrees = 3.2f;

        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pageTurnSound;
        [SerializeField] private AudioClip typewriterClickSound;
        [SerializeField] private AudioClip carriageReturnSound;
        [SerializeField] private AudioClip photoSlapSound;
        [SerializeField] private AudioClip stampThudSound;
        [SerializeField] private AudioClip shutterFlashSound;

        [Header("Ledger Typewriter Cadence")]
        [Tooltip("Play a typewriter click every N non-whitespace characters.")]
        [Range(1, 8)] [SerializeField] private int charsPerSoundTick = 2;
        [Tooltip("Minimum interval in seconds between consecutive ledger typing sounds.")]
        [Range(0.005f, 0.20f)] [SerializeField] private float minTickSoundInterval = 0.024f;
        [Range(0.004f, 0.12f)] [SerializeField] private float baseNameCharInterval = 0.024f;
        [Range(0.004f, 0.12f)] [SerializeField] private float baseMetaCharInterval = 0.019f;
        [Range(0.004f, 0.12f)] [SerializeField] private float baseNotesCharInterval = 0.013f;
        [Range(1.0f, 5.0f)] [SerializeField] private float punctuationCadenceMultiplier = 2.1f;
        [Range(0.5f, 1.5f)] [SerializeField] private float soundPitchMin = 0.92f;
        [Range(0.5f, 1.5f)] [SerializeField] private float soundPitchMax = 1.14f;
        [Range(0f, 1f)] [SerializeField] private float soundVolume = 0.38f;

        private float _lastBookTypeTickTime = -10f;

        private Image portraitFlashOverlay;
        private SpriteRenderer portraitFlashSpriteOverlay;
        private SpriteRenderer portraitBackingFrame;
        private Coroutine activeTypewriteRoutine;
        private Coroutine activeStampRoutine;
        private FirstPersonHandsController handsController;
        private CheckInBookViewController bookViewController;
        private static Material sharedCrispSdfMaterial;

        private Vector3 basePortraitLocalPos = new Vector3(-0.121f, 0.049f, -0.0045f);
        private Vector3 basePortraitLocalScale = new Vector3(0.076f, 0.076f, 0.076f);

        public event System.Action OnShutterFlash;
        public event System.Action<char> OnCharacterTyped;
        public bool IsTypewriting => activeTypewriteRoutine != null;

        [Header("Sample / Test Data")]
        [SerializeField] private List<GuestData> guestRegistry = new List<GuestData>();
        [SerializeField] private int currentPageIndex = 0;

        [Header("Night Loop")]
        [Tooltip("When true the built-in sample guests are never created (the HanGameDirector fills the ledger).")]
        [SerializeField] private bool suppressDefaultGuests = false;

        public bool SuppressDefaultGuests
        {
            get => suppressDefaultGuests;
            set => suppressDefaultGuests = value;
        }

        public int CurrentPageIndex => currentPageIndex;
        public int TotalGuests => guestRegistry.Count;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            handsController = GetComponent<FirstPersonHandsController>();
            bookViewController = GetComponent<CheckInBookViewController>();

            AutoBindHeadersIfMissing();
            ApplyArchivalLayoutAndDepthFixes();

            if (Application.isPlaying)
            {
                EnsureDefaultGuests();
            }
        }

        private void OnEnable()
        {
            HanLocalization.OnLanguageChanged -= HandleLanguageChanged;
            HanLocalization.OnLanguageChanged += HandleLanguageChanged;

            AutoBindHeadersIfMissing();
            TagAndLocalizeDefaultSampleGuests();
            ApplyArchivalLayoutAndDepthFixes();
        }

        private void OnDisable()
        {
            HanLocalization.OnLanguageChanged -= HandleLanguageChanged;
        }

        private void HandleLanguageChanged(HanLanguage _)
        {
            RefreshLocalization();
        }

        /// <summary>
        /// Refreshes headers, sample guests, and the currently visible page when the language changes.
        /// </summary>
        public void RefreshLocalization()
        {
            TagAndLocalizeDefaultSampleGuests();
            ApplyArchivalLayoutAndDepthFixes();

            if (!IsTypewriting)
            {
                if (guestRegistry != null && guestRegistry.Count > 0)
                    DisplayGuest(currentPageIndex);
                else
                    DisplayEmpty();
            }
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            EnsureDefaultGuests();
            TagAndLocalizeDefaultSampleGuests();
            ApplyArchivalLayoutAndDepthFixes();

            if (guestRegistry.Count > 0)
            {
                DisplayGuest(currentPageIndex);
            }
            else
            {
                DisplayEmpty();
            }
        }

        private void AutoBindHeadersIfMissing()
        {
            if (titleHeaderText == null)
            {
                Transform t = transform.Find("TitleText");
                if (t != null) titleHeaderText = t.GetComponent<TMP_Text>();
            }
            if (notesHeaderText == null)
            {
                Transform t = transform.Find("NotesHeader");
                if (t != null) notesHeaderText = t.GetComponent<TMP_Text>();
            }
        }

        /// <summary>
        /// Eliminates all Z-fighting, depth-buffer clipping, layer mismatches, and SDF font blurriness
        /// across every element of the 3D ledger book.
        /// </summary>
        public void ApplyArchivalLayoutAndDepthFixes()
        {
            int bookLayer = gameObject.layer;

            // 1. Stratified Z-Depth & Archival Coordinates
            SetElementTransform(titleHeaderText != null ? titleHeaderText.transform : null, new Vector3(-0.121f, 0.122f, textDepthZ), bookLayer);
            SetElementTransform(notesHeaderText != null ? notesHeaderText.transform : null, new Vector3(0.121f, 0.122f, textDepthZ), bookLayer);
            SetElementTransform(guestNameText != null ? guestNameText.transform : null, new Vector3(-0.121f, -0.016f, textDepthZ), bookLayer);
            SetElementTransform(roomNumberText != null ? roomNumberText.transform : null, new Vector3(-0.121f, -0.045f, textDepthZ), bookLayer);
            SetElementTransform(arrivalTimeText != null ? arrivalTimeText.transform : null, new Vector3(-0.121f, -0.074f, textDepthZ), bookLayer);
            SetElementTransform(notesText != null ? notesText.transform : null, new Vector3(0.121f, 0.026f, textDepthZ), bookLayer);
            SetElementTransform(pageCounterText != null ? pageCounterText.transform : null, new Vector3(0.121f, -0.120f, textDepthZ), bookLayer);

            if (portraitRenderer != null)
            {
                basePortraitLocalPos = new Vector3(-0.121f, 0.049f, portraitDepthZ);
                basePortraitLocalScale = GetNormalizedPortraitScale(portraitRenderer.sprite);
                portraitRenderer.transform.localPosition = basePortraitLocalPos;
                portraitRenderer.transform.localScale = basePortraitLocalScale;
                portraitRenderer.gameObject.layer = bookLayer;
                portraitRenderer.sortingOrder = 14;
                EnsurePhotoPaperBacking(bookLayer);
            }

            if (horseStabledBadge != null)
            {
                horseStabledBadge.transform.localPosition = new Vector3(-0.121f, -0.106f, portraitDepthZ);
                horseStabledBadge.layer = bookLayer;
                if (horseIconRenderer != null) horseIconRenderer.sortingOrder = 14;
            }

            if (checkedInStamp != null)
            {
                checkedInStamp.transform.localPosition = new Vector3(0.121f, -0.066f, stampDepthZ);
                checkedInStamp.layer = bookLayer;
                TMP_Text stampTmp = checkedInStamp.GetComponent<TMP_Text>();
                if (stampTmp != null)
                {
                    ConfigureTmpBox(stampTmp, new Vector2(0.174f, 0.042f), 0.118f, TextAlignmentOptions.Center, false);
                    ConfigureCrispTmpElement(stampTmp, stampOxbloodColor, 22, bookLayer);
                    stampTmp.text = HanLocalization.Get("book.stamp_checked_in");
                }
            }

            // 2. Configure High-Resolution SDF Settings & Rich Non-Pure-Black Ink Colors
            if (titleHeaderText != null)
            {
                titleHeaderText.text = HanLocalization.Get("book.title_header");
                ConfigureTmpBox(titleHeaderText, new Vector2(0.184f, 0.032f), 0.104f, TextAlignmentOptions.Center, false);
                ConfigureCrispTmpElement(titleHeaderText, headerInkColor, 18, bookLayer);
            }
            if (notesHeaderText != null)
            {
                notesHeaderText.text = HanLocalization.Get("book.notes_header");
                ConfigureTmpBox(notesHeaderText, new Vector2(0.184f, 0.032f), 0.090f, TextAlignmentOptions.Center, false);
                ConfigureCrispTmpElement(notesHeaderText, headerInkColor, 18, bookLayer);
            }

            ConfigureTmpBox(guestNameText, new Vector2(0.178f, 0.028f), 0.106f, TextAlignmentOptions.Center, false);
            ConfigureCrispTmpElement(guestNameText, primaryEntryInkColor, 18, bookLayer);

            ConfigureTmpBox(roomNumberText, new Vector2(0.178f, 0.026f), 0.090f, TextAlignmentOptions.Center, false);
            ConfigureCrispTmpElement(roomNumberText, metadataInkColor, 18, bookLayer);

            ConfigureTmpBox(arrivalTimeText, new Vector2(0.178f, 0.026f), 0.084f, TextAlignmentOptions.Center, false);
            ConfigureCrispTmpElement(arrivalTimeText, metadataInkColor, 18, bookLayer);

            ConfigureTmpBox(notesText, new Vector2(0.172f, 0.122f), 0.074f, TextAlignmentOptions.TopLeft, true);
            ConfigureCrispTmpElement(notesText, primaryEntryInkColor, 18, bookLayer);

            ConfigureTmpBox(pageCounterText, new Vector2(0.170f, 0.026f), 0.072f, TextAlignmentOptions.Center, false);
            ConfigureCrispTmpElement(pageCounterText, metadataInkColor, 18, bookLayer);
        }

        private void ConfigureTmpBox(TMP_Text tmp, Vector2 sizeDelta, float fontSize, TextAlignmentOptions alignment, bool wordWrap)
        {
            if (tmp == null) return;
            tmp.transform.localScale = Vector3.one;
            RectTransform rt = tmp.rectTransform;
            if (rt != null)
            {
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = sizeDelta;
            }
            tmp.fontSize = fontSize;
            tmp.enableAutoSizing = !wordWrap;
            if (!wordWrap)
            {
                tmp.fontSizeMax = fontSize;
                tmp.fontSizeMin = fontSize * 0.65f;
            }
            tmp.alignment = alignment;
            tmp.enableWordWrapping = wordWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
        }

        private Vector3 GetNormalizedPortraitScale(Sprite s)
        {
            const float targetSlotWorldSize = 0.080f; // Fits cleanly inside the 0.084m etched left-page photo frame
            if (s == null) return new Vector3(targetSlotWorldSize, targetSlotWorldSize, 1f);
            float maxBounds = Mathf.Max(s.bounds.size.x, s.bounds.size.y);
            if (maxBounds <= 0.0001f) return new Vector3(targetSlotWorldSize, targetSlotWorldSize, 1f);
            float uniformScale = targetSlotWorldSize / maxBounds;
            return new Vector3(uniformScale, uniformScale, 1f);
        }

        private void SetElementTransform(Transform t, Vector3 localPos, int layer)
        {
            if (t == null) return;
            t.localPosition = localPos;
            t.gameObject.layer = layer;
        }

        private void ConfigureCrispTmpElement(TMP_Text tmp, Color inkColor, int sortOrder, int layer)
        {
            if (tmp == null) return;

            tmp.gameObject.layer = layer;
            tmp.color = inkColor;
            tmp.extraPadding = true;
            tmp.enableKerning = true;
            tmp.overrideColorTags = false;

            MeshRenderer mr = tmp.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = sortOrder;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            // Apply high-definition SDF material tuning (custom Face Dilate & anti-aliased edge softness)
            if (tmp.fontSharedMaterial != null)
            {
                if (sharedCrispSdfMaterial == null)
                {
                    sharedCrispSdfMaterial = new Material(tmp.fontSharedMaterial);
                    sharedCrispSdfMaterial.name = "M_Archival_IronGall_SDF";
                }

                if (sharedCrispSdfMaterial.HasProperty(ShaderUtilities.ID_FaceDilate))
                    sharedCrispSdfMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, sdfFaceDilate);
                if (sharedCrispSdfMaterial.HasProperty(ShaderUtilities.ID_OutlineWidth))
                    sharedCrispSdfMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, sdfOutlineWidth);
                if (sharedCrispSdfMaterial.HasProperty(ShaderUtilities.ID_OutlineSoftness))
                    sharedCrispSdfMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, sdfOutlineSoftness);
                if (sharedCrispSdfMaterial.HasProperty(ShaderUtilities.ID_OutlineColor))
                    sharedCrispSdfMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.18f, 0.11f, 0.07f, 0.55f));
                if (sharedCrispSdfMaterial.HasProperty(ShaderUtilities.ID_WeightNormal))
                    sharedCrispSdfMaterial.SetFloat(ShaderUtilities.ID_WeightNormal, 0.12f);

                tmp.fontSharedMaterial = sharedCrispSdfMaterial;
            }

            // Ensure all child TMP_SubMesh objects (used for Romanian diacritics) match Layer 8 and sortingOrder
            SyncTmpSubMeshes(tmp, sortOrder, layer);
        }

        private void SyncTmpSubMeshes(TMP_Text tmp, int sortOrder, int layer)
        {
            if (tmp == null) return;
            var subMeshes = tmp.GetComponentsInChildren<TMP_SubMesh>(true);
            for (int i = 0; i < subMeshes.Length; i++)
            {
                if (subMeshes[i] == null) continue;
                subMeshes[i].gameObject.layer = layer;
                subMeshes[i].transform.localPosition = Vector3.zero;
                Renderer smr = subMeshes[i].renderer;
                if (smr != null)
                {
                    smr.sortingOrder = sortOrder;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    smr.receiveShadows = false;
                }
            }
        }

        private void EnsurePhotoPaperBacking(int layer)
        {
            if (portraitRenderer == null || defaultSilhouetteSprite == null) return;
            float parentScale = Mathf.Max(0.0005f, basePortraitLocalScale.x);
            float cardBounds = Mathf.Max(0.001f, defaultSilhouetteSprite.bounds.size.x);
            float localCardScale = 0.078f / (parentScale * cardBounds);

            Transform existing = portraitRenderer.transform.Find("PhotoPaperMount");
            if (existing != null)
            {
                portraitBackingFrame = existing.GetComponent<SpriteRenderer>();
                if (portraitBackingFrame != null)
                {
                    portraitBackingFrame.sprite = defaultSilhouetteSprite;
                    portraitBackingFrame.color = new Color(0.89f, 0.82f, 0.67f, 0.55f);
                    portraitBackingFrame.sortingOrder = 13;
                }
                existing.localPosition = new Vector3(0f, 0f, 0.008f);
                existing.localScale = new Vector3(localCardScale, localCardScale, 1f);
                existing.gameObject.layer = layer;
                return;
            }

            GameObject mountObj = new GameObject("PhotoPaperMount");
            mountObj.layer = layer;
            mountObj.transform.SetParent(portraitRenderer.transform, false);
            mountObj.transform.localPosition = new Vector3(0f, 0f, 0.008f);
            mountObj.transform.localScale = new Vector3(localCardScale, localCardScale, 1f);

            portraitBackingFrame = mountObj.AddComponent<SpriteRenderer>();
            portraitBackingFrame.sprite = defaultSilhouetteSprite;
            portraitBackingFrame.color = new Color(0.89f, 0.82f, 0.67f, 0.55f);
            portraitBackingFrame.sortingOrder = 13;
        }

        /// <summary>
        /// Populates authentic 1600s Transylvanian thriller guests if the registry is empty.
        /// </summary>
        private void EnsureDefaultGuests()
        {
            if (suppressDefaultGuests) return;
            if (guestRegistry != null && guestRegistry.Count > 0)
            {
                TagAndLocalizeDefaultSampleGuests();
                return;
            }

            guestRegistry = new List<GuestData>
            {
                new GuestData
                {
                    defaultSampleId = 1,
                    arrivalTimeKey = "arrival.sunset",
                    guestName = "Florin din Vatra",
                    roomNumber = 3,
                    arrivalTime = HanLocalization.Get("arrival.sunset"),
                    hasHorse = true,
                    isCheckedIn = true,
                    notes = HanLocalization.Get("sample.guest.1.notes")
                },
                new GuestData
                {
                    defaultSampleId = 2,
                    arrivalTimeKey = "arrival.dusk",
                    guestName = "Gheorghe Străinul",
                    roomNumber = 6,
                    arrivalTime = HanLocalization.Get("arrival.dusk"),
                    hasHorse = false,
                    isCheckedIn = true,
                    notes = HanLocalization.Get("sample.guest.2.notes")
                },
                new GuestData
                {
                    defaultSampleId = 3,
                    arrivalTimeKey = "arrival.nightfall",
                    guestName = "Doamna Ilinca",
                    roomNumber = 2,
                    arrivalTime = HanLocalization.Get("arrival.nightfall"),
                    hasHorse = true,
                    isCheckedIn = false,
                    notes = HanLocalization.Get("sample.guest.3.notes")
                }
            };
        }

        private void TagAndLocalizeDefaultSampleGuests()
        {
            if (guestRegistry == null || guestRegistry.Count == 0) return;
            for (int i = 0; i < guestRegistry.Count; i++)
            {
                GuestData g = guestRegistry[i];
                if (g == null) continue;

                if (g.defaultSampleId == 0)
                {
                    if (g.guestName == "Florin din Vatra")
                    {
                        g.defaultSampleId = 1;
                        g.arrivalTimeKey = "arrival.sunset";
                    }
                    else if (g.guestName == "Gheorghe Străinul")
                    {
                        g.defaultSampleId = 2;
                        g.arrivalTimeKey = "arrival.dusk";
                    }
                    else if (g.guestName == "Doamna Ilinca")
                    {
                        g.defaultSampleId = 3;
                        g.arrivalTimeKey = "arrival.nightfall";
                    }
                }

                if (g.defaultSampleId > 0 || !string.IsNullOrEmpty(g.neutralNoteKey) || !string.IsNullOrEmpty(g.arrivalTimeKey))
                {
                    GuestGenerator.ApplyLocalization(g);
                }
            }
        }

        public void LoadGuestRegistry(List<GuestData> newGuests, int initialIndex = 0)
        {
            guestRegistry = newGuests ?? new List<GuestData>();
            currentPageIndex = Mathf.Clamp(initialIndex, 0, Mathf.Max(0, guestRegistry.Count - 1));

            if (guestRegistry.Count > 0)
                DisplayGuest(currentPageIndex);
            else
                DisplayEmpty();
        }

        public void DisplayGuest(int index)
        {
            if (guestRegistry == null || guestRegistry.Count == 0)
            {
                DisplayEmpty();
                return;
            }

            currentPageIndex = Mathf.Clamp(index, 0, guestRegistry.Count - 1);
            GuestData data = guestRegistry[currentPageIndex];

            int bookLayer = gameObject.layer;

            if (guestNameText != null)
            {
                guestNameText.maxVisibleCharacters = 99999;
                guestNameText.text = string.IsNullOrEmpty(data.guestName) ? HanLocalization.Get("book.unknown_traveler") : data.guestName;
                SyncTmpSubMeshes(guestNameText, 18, bookLayer);
            }

            if (roomNumberText != null)
            {
                roomNumberText.maxVisibleCharacters = 99999;
                roomNumberText.text = data.roomNumber > 0 ? HanLocalization.Format("book.room_format", data.roomNumber) : HanLocalization.Get("book.no_room");
                SyncTmpSubMeshes(roomNumberText, 18, bookLayer);
            }

            if (arrivalTimeText != null)
            {
                arrivalTimeText.maxVisibleCharacters = 99999;
                arrivalTimeText.text = HanLocalization.Format("book.arrival_format", data.arrivalTime);
                SyncTmpSubMeshes(arrivalTimeText, 18, bookLayer);
            }

            if (notesText != null)
            {
                notesText.maxVisibleCharacters = 99999;
                notesText.text = string.IsNullOrEmpty(data.notes) ? HanLocalization.Get("book.no_notes") : data.notes;
                SyncTmpSubMeshes(notesText, 18, bookLayer);
            }

            if (pageCounterText != null)
            {
                pageCounterText.maxVisibleCharacters = 99999;
                pageCounterText.text = HanLocalization.Format("book.page_counter", currentPageIndex + 1, guestRegistry.Count);
                SyncTmpSubMeshes(pageCounterText, 18, bookLayer);
            }

            Sprite targetPortrait = data.portrait != null ? data.portrait : defaultSilhouetteSprite;
            if (portraitRenderer != null)
            {
                portraitRenderer.sprite = targetPortrait;
                portraitRenderer.color = Color.white;
                basePortraitLocalScale = GetNormalizedPortraitScale(targetPortrait);
                portraitRenderer.transform.localPosition = basePortraitLocalPos;
                portraitRenderer.transform.localScale = basePortraitLocalScale;
                portraitRenderer.transform.localRotation = Quaternion.identity;
                EnsurePhotoPaperBacking(bookLayer);
            }
            if (portraitImage != null)
            {
                portraitImage.sprite = targetPortrait;
                portraitImage.color = targetPortrait != null ? Color.white : new Color(0.16f, 0.11f, 0.08f, 0.85f);
            }

            if (horseStabledBadge != null)
                horseStabledBadge.SetActive(data.hasHorse);

            if (horseIconRenderer != null)
                horseIconRenderer.color = data.hasHorse ? horsePresentColor : horseAbsentColor;

            if (horseIcon != null)
                horseIcon.color = data.hasHorse ? horsePresentColor : horseAbsentColor;

            if (checkedInStamp != null)
            {
                checkedInStamp.layer = bookLayer;
                checkedInStamp.SetActive(data.isCheckedIn);
            }
        }

        public void DisplayEmpty()
        {
            if (guestNameText != null) guestNameText.text = HanLocalization.Get("book.empty_title");
            if (roomNumberText != null) roomNumberText.text = HanLocalization.Get("book.empty_room");
            if (arrivalTimeText != null) arrivalTimeText.text = HanLocalization.Get("book.empty_arrival");
            if (notesText != null) notesText.text = HanLocalization.Get("book.empty_notes");
            if (pageCounterText != null) pageCounterText.text = HanLocalization.Get("book.empty_page");
            if (horseStabledBadge != null) horseStabledBadge.SetActive(false);
            if (checkedInStamp != null) checkedInStamp.SetActive(false);
            if (portraitImage != null) portraitImage.color = new Color(0.18f, 0.12f, 0.08f, 0.35f);
            if (portraitRenderer != null)
            {
                portraitRenderer.sprite = defaultSilhouetteSprite;
                portraitRenderer.color = new Color(0.28f, 0.20f, 0.14f, 0.35f);
            }
            if (horseIconRenderer != null) horseIconRenderer.color = horseAbsentColor;
        }

        public void NextPage()
        {
            if (guestRegistry.Count <= 1 || currentPageIndex >= guestRegistry.Count - 1)
                return;

            currentPageIndex++;
            DisplayGuest(currentPageIndex);
            PlayPageSound();
        }

        public void PreviousPage()
        {
            if (guestRegistry.Count <= 1 || currentPageIndex <= 0)
                return;

            currentPageIndex--;
            DisplayGuest(currentPageIndex);
            PlayPageSound();
        }

        /// <summary>
        /// Premium Ink-Typewriter & Multi-Stage Photo Drop Sequence.
        /// 1. Rhythmic mechanical letter-by-letter reveal with per-character baseline settle & audio hooks.
        /// 2. Instant camera shutter flash + physical photo drop/stamp animation with rotation variation & overshoot bounce.
        /// </summary>
        public Coroutine TypewriteGuest(GuestData data, System.Action onComplete = null)
        {
            if (activeTypewriteRoutine != null)
                StopCoroutine(activeTypewriteRoutine);

            activeTypewriteRoutine = StartCoroutine(TypewriteRoutine(data, onComplete));
            return activeTypewriteRoutine;
        }

        private IEnumerator TypewriteRoutine(GuestData data, System.Action onComplete)
        {
            EnsureAudioClips();
            ApplyArchivalLayoutAndDepthFixes();

            if (handsController == null) handsController = GetComponent<FirstPersonHandsController>();
            if (bookViewController == null) bookViewController = GetComponent<CheckInBookViewController>();

            // Clear all fields before typing
            if (guestNameText != null) guestNameText.text = "";
            if (roomNumberText != null) roomNumberText.text = "";
            if (arrivalTimeText != null) arrivalTimeText.text = "";
            if (notesText != null) notesText.text = "";
            if (checkedInStamp != null) checkedInStamp.SetActive(false);
            if (horseStabledBadge != null) horseStabledBadge.SetActive(false);
            if (portraitRenderer != null) portraitRenderer.color = new Color(1f, 1f, 1f, 0f);
            if (portraitImage != null) portraitImage.color = new Color(1f, 1f, 1f, 0f);
            if (horseIconRenderer != null) horseIconRenderer.color = horseAbsentColor;
            if (horseIcon != null) horseIcon.color = horseAbsentColor;

            if (pageCounterText != null)
            {
                pageCounterText.text = HanLocalization.Get("book.recording_entry");
                SyncTmpSubMeshes(pageCounterText, 18, gameObject.layer);
            }

            if (handsController != null)
                handsController.SetTypingActive(true);

            // 1. Ink-Typewrite Guest Name with Baseline Settle
            string nameStr = string.IsNullOrEmpty(data.guestName) ? HanLocalization.Get("book.unknown_traveler") : data.guestName;
            yield return TypeTextWithBaselineSettle(guestNameText, nameStr, Mathf.Max(0.004f, baseNameCharInterval), primaryEntryInkColor);
            PlayCarriageReturnSound();
            yield return new WaitForSeconds(0.08f);

            // 2. Ink-Typewrite Room & Arrival Metadata
            string roomStr = data.roomNumber > 0 ? HanLocalization.Format("book.room_format", data.roomNumber) : HanLocalization.Get("book.no_room");
            string arrivalStr = HanLocalization.Format("book.arrival_format", data.arrivalTime);
            yield return TypeTextWithBaselineSettle(roomNumberText, roomStr, Mathf.Max(0.004f, baseMetaCharInterval), metadataInkColor);
            yield return TypeTextWithBaselineSettle(arrivalTimeText, arrivalStr, Mathf.Max(0.004f, baseMetaCharInterval), metadataInkColor);
            PlayCarriageReturnSound();
            yield return new WaitForSeconds(0.08f);

            // 3. Stamp Horse Stable Status if present
            if (data.hasHorse)
            {
                if (horseStabledBadge != null)
                {
                    horseStabledBadge.SetActive(true);
                    horseStabledBadge.layer = gameObject.layer;
                }
                if (horseIconRenderer != null) horseIconRenderer.color = horsePresentColor;
                if (horseIcon != null) horseIcon.color = horsePresentColor;
                PlayTypeSound();
            }
            yield return new WaitForSeconds(0.07f);

            // 4. Ink-Typewrite Archival Notes & Clues on Right Page
            string noteStr = string.IsNullOrEmpty(data.notes) ? HanLocalization.Get("book.no_special_notes") : data.notes;
            yield return TypeTextWithBaselineSettle(notesText, noteStr, Mathf.Max(0.004f, baseNotesCharInterval), primaryEntryInkColor);
            PlayCarriageReturnSound();

            if (handsController != null)
                handsController.SetTypingActive(false);

            yield return new WaitForSeconds(0.10f);

            // 5. Multi-Stage Photo Reveal: Instant Shutter Flash + Physical Drop/Stamp with Overshoot Bounce
            yield return RevealPortraitMultiStageCoroutine(data);

            if (pageCounterText != null)
            {
                pageCounterText.text = HanLocalization.Format("book.page_single", Mathf.Max(1, guestRegistry.Count + 1));
            }

            activeTypewriteRoutine = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Rhythmic mechanical letter-by-letter reveal with per-character vertex baseline settle
        /// and wet-to-dry iron-gall ink absorption.
        /// </summary>
        private IEnumerator TypeTextWithBaselineSettle(TMP_Text textComponent, string fullText, float charInterval, Color finalInkColor)
        {
            if (textComponent == null || string.IsNullOrEmpty(fullText)) yield break;

            int bookLayer = gameObject.layer;
            textComponent.color = finalInkColor;
            textComponent.text = fullText;
            textComponent.maxVisibleCharacters = 0;
            textComponent.ForceMeshUpdate();
            SyncTmpSubMeshes(textComponent, 18, bookLayer);

            TMP_TextInfo textInfo = textComponent.textInfo;
            int totalChars = textInfo.characterCount;
            if (totalChars <= 0)
            {
                textComponent.maxVisibleCharacters = 99999;
                yield break;
            }

            // Cache clean baseline vertices per material mesh
            Vector3[][] cleanVertices = new Vector3[textInfo.meshInfo.Length][];
            for (int m = 0; m < textInfo.meshInfo.Length; m++)
            {
                Vector3[] src = textInfo.meshInfo[m].vertices;
                if (src != null)
                {
                    cleanVertices[m] = new Vector3[src.Length];
                    System.Array.Copy(src, cleanVertices[m], src.Length);
                }
            }

            float[] revealTimestamps = new float[totalChars];
            for (int i = 0; i < totalChars; i++) revealTimestamps[i] = -1f;

            int revealedCount = 0;
            float nextCharTimer = 0f;
            Color32 targetCol32 = finalInkColor;

            while (true)
            {
                float now = Time.time;
                nextCharTimer -= Time.deltaTime;

                while (revealedCount < totalChars && nextCharTimer <= 0f)
                {
                    revealTimestamps[revealedCount] = now;
                    char c = textInfo.characterInfo[revealedCount].character;
                    revealedCount++;
                    textComponent.maxVisibleCharacters = revealedCount;

                    if (!char.IsWhiteSpace(c))
                    {
                        int step = Mathf.Max(1, charsPerSoundTick);
                        if ((revealedCount - 1) % step == 0) PlayTypeSound();
                        OnCharacterTyped?.Invoke(c);
                    }

                    // Rhythmic mechanical cadence: slight pause on punctuation
                    bool isPunct = c == '.' || c == ',' || c == ':' || c == ';' || c == '!' || c == '?' || c == '-' || c == '…';
                    float cadenceMultiplier = isPunct ? Mathf.Max(1f, punctuationCadenceMultiplier) : Random.Range(0.82f, 1.18f);
                    nextCharTimer += charInterval * cadenceMultiplier;
                }

                bool anySettling = false;

                for (int i = 0; i < revealedCount; i++)
                {
                    TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;

                    int matIdx = charInfo.materialReferenceIndex;
                    int vertIdx = charInfo.vertexIndex;
                    if (cleanVertices[matIdx] == null || vertIdx + 3 >= cleanVertices[matIdx].Length) continue;

                    Vector3[] dstVerts = textInfo.meshInfo[matIdx].vertices;
                    Color32[] dstCols = textInfo.meshInfo[matIdx].colors32;
                    Vector3[] baseVerts = cleanVertices[matIdx];

                    float age = now - revealTimestamps[i];
                    if (age < charSettleDuration)
                    {
                        anySettling = true;
                        float u = Mathf.Clamp01(age / Mathf.Max(0.005f, charSettleDuration));
                        // Crisp mechanical strike curve: rapid drop onto baseline with slight settle
                        float settle = 1f - Mathf.Pow(1f - u, 3f);

                        Vector3 center = (baseVerts[vertIdx] + baseVerts[vertIdx + 2]) * 0.5f;
                        float scale = Mathf.Lerp(charSettleInitialScale, 1f, settle);
                        float yDrop = Mathf.Lerp(charSettleVerticalOffset, 0f, settle);

                        for (int v = 0; v < 4; v++)
                        {
                            Vector3 rel = baseVerts[vertIdx + v] - center;
                            dstVerts[vertIdx + v] = center + rel * scale + new Vector3(0f, yDrop, 0f);
                        }

                        if (dstCols != null && vertIdx + 3 < dstCols.Length)
                        {
                            byte alpha = (byte)Mathf.RoundToInt(Mathf.Lerp(150f, targetCol32.a, settle));
                            Color32 wetInk = new Color32(targetCol32.r, targetCol32.g, targetCol32.b, alpha);
                            dstCols[vertIdx + 0] = wetInk;
                            dstCols[vertIdx + 1] = wetInk;
                            dstCols[vertIdx + 2] = wetInk;
                            dstCols[vertIdx + 3] = wetInk;
                        }
                    }
                    else
                    {
                        dstVerts[vertIdx + 0] = baseVerts[vertIdx + 0];
                        dstVerts[vertIdx + 1] = baseVerts[vertIdx + 1];
                        dstVerts[vertIdx + 2] = baseVerts[vertIdx + 2];
                        dstVerts[vertIdx + 3] = baseVerts[vertIdx + 3];

                        if (dstCols != null && vertIdx + 3 < dstCols.Length)
                        {
                            dstCols[vertIdx + 0] = targetCol32;
                            dstCols[vertIdx + 1] = targetCol32;
                            dstCols[vertIdx + 2] = targetCol32;
                            dstCols[vertIdx + 3] = targetCol32;
                        }
                    }
                }

                textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);

                if (revealedCount >= totalChars && !anySettling)
                    break;

                yield return null;
            }

            textComponent.maxVisibleCharacters = 99999;
        }

        /// <summary>
        /// Multi-stage photo reveal:
        /// Stage 1: Instant camera shutter flash overlay & audio.
        /// Stage 2: Physical drop/stamp animation of the character photo onto the left-page photo slot
        ///          with subtle rotation variation and overshoot bounce.
        /// </summary>
        private IEnumerator RevealPortraitMultiStageCoroutine(GuestData data)
        {
            Sprite targetPortrait = data.portrait != null ? data.portrait : defaultSilhouetteSprite;
            basePortraitLocalScale = GetNormalizedPortraitScale(targetPortrait);

            if (portraitRenderer != null)
            {
                portraitRenderer.sprite = targetPortrait;
                portraitRenderer.color = Color.white;
                EnsurePhotoPaperBacking(gameObject.layer);
                EnsurePortraitSpriteFlashOverlay();
                if (portraitFlashSpriteOverlay != null)
                    portraitFlashSpriteOverlay.sprite = targetPortrait;
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = targetPortrait;
                portraitImage.color = Color.white;
                EnsurePortraitFlashOverlay();
            }

            // Stage 1: Instant Shutter Flash
            PlayShutterFlashSound();
            OnShutterFlash?.Invoke();

            if (portraitFlashOverlay != null)
            {
                portraitFlashOverlay.gameObject.SetActive(true);
                portraitFlashOverlay.color = Color.white;
            }
            if (portraitFlashSpriteOverlay != null)
            {
                portraitFlashSpriteOverlay.gameObject.SetActive(true);
                portraitFlashSpriteOverlay.color = Color.white;
            }

            // Stage 2: Physical Drop / Stamp onto Designated Photo Slot
            float targetAngleZ = Random.Range(-photoMaxTiltDegrees, photoMaxTiltDegrees);
            float startAngleZ = targetAngleZ * 2.2f;

            Vector3 dropStartPos = basePortraitLocalPos + new Vector3(0f, 0.010f, -0.022f);
            Vector3 dropStartScale = basePortraitLocalScale * 1.48f;
            Vector3 impactCompressedScale = basePortraitLocalScale * 0.91f;
            Vector3 overshootScale = basePortraitLocalScale * 1.06f;

            // Phase A: Drop onto the page (0 -> photoDropDuration)
            float dropDur = Mathf.Max(0.08f, photoDropDuration);
            for (float t = 0f; t < dropDur; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / dropDur);
                // Accelerating gravity/stamp drop curve (u^2)
                float dropCurve = u * u;

                if (portraitRenderer != null)
                {
                    portraitRenderer.transform.localPosition = Vector3.LerpUnclamped(dropStartPos, basePortraitLocalPos, dropCurve);
                    portraitRenderer.transform.localScale = Vector3.LerpUnclamped(dropStartScale, impactCompressedScale, dropCurve);
                    portraitRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startAngleZ, targetAngleZ, dropCurve));
                }

                float flashAlpha = Mathf.Clamp01(1f - u * 0.8f);
                if (portraitFlashOverlay != null)
                    portraitFlashOverlay.color = new Color(1f, 0.99f, 0.94f, flashAlpha);
                if (portraitFlashSpriteOverlay != null)
                    portraitFlashSpriteOverlay.color = new Color(1f, 0.99f, 0.94f, flashAlpha);

                yield return null;
            }

            // Impact Moment: Photo slaps onto the page!
            PlayPhotoSlapSound();
            if (handsController != null) handsController.TriggerImpactRecoil(0.65f);
            if (bookViewController != null) bookViewController.TriggerBookImpulse(0.45f);

            // Phase B: Overshoot Bounce & Settle (0 -> photoBounceDuration)
            float bounceDur = Mathf.Max(0.10f, photoBounceDuration);
            for (float t = 0f; t < bounceDur; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / bounceDur);
                Vector3 curScale;
                if (u < 0.45f)
                {
                    float bu = u / 0.45f;
                    curScale = Vector3.LerpUnclamped(impactCompressedScale, overshootScale, Mathf.SmoothStep(0f, 1f, bu));
                }
                else
                {
                    float su = (u - 0.45f) / 0.55f;
                    curScale = Vector3.LerpUnclamped(overshootScale, basePortraitLocalScale, Mathf.SmoothStep(0f, 1f, su));
                }

                if (portraitRenderer != null)
                {
                    portraitRenderer.transform.localPosition = basePortraitLocalPos;
                    portraitRenderer.transform.localScale = curScale;
                    portraitRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, targetAngleZ);
                }

                float flashAlpha = Mathf.Clamp01((1f - u) * 0.25f);
                if (portraitFlashOverlay != null)
                    portraitFlashOverlay.color = new Color(1f, 0.99f, 0.94f, flashAlpha);
                if (portraitFlashSpriteOverlay != null)
                    portraitFlashSpriteOverlay.color = new Color(1f, 0.99f, 0.94f, flashAlpha);

                yield return null;
            }

            if (portraitRenderer != null)
            {
                portraitRenderer.transform.localPosition = basePortraitLocalPos;
                portraitRenderer.transform.localScale = basePortraitLocalScale;
                portraitRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, targetAngleZ);
                portraitRenderer.color = Color.white;
            }
            if (portraitImage != null)
                portraitImage.color = Color.white;

            if (portraitFlashOverlay != null)
            {
                portraitFlashOverlay.color = new Color(1f, 1f, 1f, 0f);
                portraitFlashOverlay.gameObject.SetActive(false);
            }
            if (portraitFlashSpriteOverlay != null)
            {
                portraitFlashSpriteOverlay.color = new Color(1f, 1f, 1f, 0f);
                portraitFlashSpriteOverlay.gameObject.SetActive(false);
            }

            yield return new WaitForSeconds(0.14f);
        }

        private void EnsurePortraitFlashOverlay()
        {
            if (portraitFlashOverlay != null || portraitImage == null) return;

            GameObject flashObj = new GameObject("PortraitShutterFlash");
            flashObj.layer = gameObject.layer;
            flashObj.transform.SetParent(portraitImage.transform, false);
            portraitFlashOverlay = flashObj.AddComponent<Image>();
            portraitFlashOverlay.color = new Color(1f, 1f, 1f, 0f);
            portraitFlashOverlay.raycastTarget = false;

            RectTransform rt = flashObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            flashObj.SetActive(false);
        }

        private void EnsurePortraitSpriteFlashOverlay()
        {
            if (portraitFlashSpriteOverlay != null || portraitRenderer == null) return;

            Transform existing = portraitRenderer.transform.Find("PortraitSpriteFlash");
            if (existing != null)
            {
                portraitFlashSpriteOverlay = existing.GetComponent<SpriteRenderer>();
                return;
            }

            GameObject flashObj = new GameObject("PortraitSpriteFlash");
            flashObj.layer = gameObject.layer;
            flashObj.transform.SetParent(portraitRenderer.transform, false);
            flashObj.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            flashObj.transform.localScale = Vector3.one;

            portraitFlashSpriteOverlay = flashObj.AddComponent<SpriteRenderer>();
            portraitFlashSpriteOverlay.sprite = defaultSilhouetteSprite;
            portraitFlashSpriteOverlay.color = new Color(1f, 1f, 1f, 0f);
            portraitFlashSpriteOverlay.sortingOrder = 24;
            flashObj.SetActive(false);
        }

        /// <summary>
        /// Slams down the CHECKED IN stamp onto the ledger with physical bounce and recoil.
        /// </summary>
        public void StampCheckedIn()
        {
            if (activeTypewriteRoutine != null)
            {
                StopCoroutine(activeTypewriteRoutine);
                activeTypewriteRoutine = null;
                if (handsController != null) handsController.SetTypingActive(false);
                if (guestRegistry != null && guestRegistry.Count > 0)
                    DisplayGuest(currentPageIndex);
            }

            if (checkedInStamp != null)
            {
                checkedInStamp.layer = gameObject.layer;
                checkedInStamp.SetActive(true);
                if (Application.isPlaying)
                {
                    if (activeStampRoutine != null) StopCoroutine(activeStampRoutine);
                    activeStampRoutine = StartCoroutine(StampSlamAnimationRoutine());
                }
            }

            PlayStampSound();
            if (handsController != null) handsController.TriggerImpactRecoil(1.0f);
            if (bookViewController != null) bookViewController.TriggerBookImpulse(0.75f);
        }

        private IEnumerator StampSlamAnimationRoutine()
        {
            if (checkedInStamp == null) yield break;

            Transform st = checkedInStamp.transform;
            Vector3 targetPos = new Vector3(0.121f, -0.066f, stampDepthZ);
            float angleZ = -14f + Random.Range(-2.2f, 2.2f);
            st.localPosition = targetPos;
            st.localRotation = Quaternion.Euler(0f, 0f, angleZ);

            const float dur = 0.22f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float u = Mathf.Clamp01(t / dur);
                float scale;
                if (u < 0.4f)
                    scale = Mathf.Lerp(1.55f, 0.88f, u / 0.4f);
                else if (u < 0.75f)
                    scale = Mathf.Lerp(0.88f, 1.05f, (u - 0.4f) / 0.35f);
                else
                    scale = Mathf.Lerp(1.05f, 1.0f, (u - 0.75f) / 0.25f);

                st.localScale = Vector3.one * scale;
                yield return null;
            }

            st.localScale = Vector3.one;
            activeStampRoutine = null;
        }

        public void SetTypingCadence(int tickEveryNChars, float minIntervalSeconds, float pitchMin = -1f, float pitchMax = -1f, float volume = -1f)
        {
            charsPerSoundTick = Mathf.Clamp(tickEveryNChars, 1, 8);
            minTickSoundInterval = Mathf.Max(0.005f, minIntervalSeconds);
            if (pitchMin > 0f) soundPitchMin = Mathf.Clamp(pitchMin, 0.5f, 1.5f);
            if (pitchMax > 0f) soundPitchMax = Mathf.Clamp(Mathf.Max(soundPitchMin, pitchMax), 0.5f, 1.5f);
            if (volume >= 0f) soundVolume = Mathf.Clamp01(volume);
        }

        private void PlayTypeSound()
        {
            if (Time.unscaledTime - _lastBookTypeTickTime < Mathf.Max(0.004f, minTickSoundInterval)) return;
            _lastBookTypeTickTime = Time.unscaledTime;

            if (audioSource != null && typewriterClickSound != null)
            {
                float lo = Mathf.Min(soundPitchMin, soundPitchMax);
                float hi = Mathf.Max(soundPitchMin, soundPitchMax);
                audioSource.pitch = Random.Range(lo, hi);
                audioSource.PlayOneShot(typewriterClickSound, Mathf.Clamp01(soundVolume));
            }
        }

        private void PlayCarriageReturnSound()
        {
            if (audioSource != null && carriageReturnSound != null)
            {
                audioSource.pitch = Random.Range(0.96f, 1.04f);
                audioSource.PlayOneShot(carriageReturnSound, 0.45f);
            }
        }

        private void PlayPhotoSlapSound()
        {
            EnsureAudioClips();
            if (audioSource != null && photoSlapSound != null)
            {
                audioSource.pitch = Random.Range(0.96f, 1.05f);
                audioSource.PlayOneShot(photoSlapSound, 0.85f);
            }
        }

        private void PlayStampSound()
        {
            EnsureAudioClips();
            if (audioSource != null && stampThudSound != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(stampThudSound, 0.9f);
            }
        }

        private void PlayShutterFlashSound()
        {
            EnsureAudioClips();
            if (audioSource != null && shutterFlashSound != null)
            {
                audioSource.pitch = Random.Range(0.98f, 1.04f);
                audioSource.PlayOneShot(shutterFlashSound, 0.95f);
            }
        }

        private void EnsureAudioClips()
        {
            if (typewriterClickSound == null)
            {
                const int samples = 512;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float env = Mathf.Exp(-i * 0.02f);
                    data[i] = (Random.value * 2f - 1f) * env;
                }
                typewriterClickSound = AudioClip.Create("TypewriterBlip", samples, 1, 22050, false);
                typewriterClickSound.SetData(data, 0);
            }

            if (carriageReturnSound == null)
            {
                const int samples = 1400;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float slide = (Random.value * 2f - 1f) * Mathf.Exp(-t * 5f) * 0.35f;
                    float bell = Mathf.Sin(2f * Mathf.PI * 1680f * (float)i / 22050f) * Mathf.Exp(-t * 7f) * 0.25f;
                    data[i] = slide + bell;
                }
                carriageReturnSound = AudioClip.Create("CarriageReturnBell", samples, 1, 22050, false);
                carriageReturnSound.SetData(data, 0);
            }

            if (photoSlapSound == null)
            {
                const int samples = 1800;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float freq = Mathf.Lerp(240f, 65f, t);
                    float body = Mathf.Sin(2f * Mathf.PI * freq * (float)i / 22050f) * Mathf.Exp(-t * 9f);
                    float crisp = (Random.value * 2f - 1f) * Mathf.Exp(-t * 18f) * 0.65f;
                    data[i] = Mathf.Clamp((body + crisp) * 0.85f, -1f, 1f);
                }
                photoSlapSound = AudioClip.Create("PhotoPaperSlap", samples, 1, 22050, false);
                photoSlapSound.SetData(data, 0);
            }

            if (shutterFlashSound == null)
            {
                const int samples = 3200;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float click1 = Mathf.Exp(-i * 0.015f) * (Random.value * 2f - 1f);
                    float click2 = i > 550 ? Mathf.Exp(-(i - 550) * 0.018f) * (Random.value * 2f - 1f) * 0.8f : 0f;
                    float pop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(900f, 180f, t) * (float)i / 22050f) * Mathf.Exp(-t * 9f) * 0.45f;
                    data[i] = Mathf.Clamp(click1 + click2 + pop, -1f, 1f);
                }
                shutterFlashSound = AudioClip.Create("CameraShutterFlash", samples, 1, 22050, false);
                shutterFlashSound.SetData(data, 0);
            }

            if (stampThudSound == null)
            {
                const int samples = 2200;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float freq = Mathf.Lerp(120f, 40f, t);
                    float sin = Mathf.Sin(2f * Mathf.PI * freq * (float)i / 22050f);
                    float noise = (Random.value * 2f - 1f) * 0.3f;
                    float env = Mathf.Exp(-t * 6f);
                    data[i] = (sin + noise) * env;
                }
                stampThudSound = AudioClip.Create("StampThud", samples, 1, 22050, false);
                stampThudSound.SetData(data, 0);
            }
        }

        private void PlayPageSound()
        {
            if (audioSource != null && pageTurnSound != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                audioSource.PlayOneShot(pageTurnSound);
            }
        }
    }
}
