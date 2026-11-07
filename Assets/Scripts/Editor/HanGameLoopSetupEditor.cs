#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

namespace Han.CheckIn.Editor
{
    /// <summary>
    /// Editor utility that builds and wires the complete tactile vintage/bureaucratic Game-Loop UI,
    /// HanUITheme asset, NightConfigData asset, URP Pause Blur Volume, HanUIAudioManager,
    /// HanUIFocusNavigator, and GameLoopManager in the active scene.
    /// </summary>
    public static class HanGameLoopSetupEditor
    {
        private const string NIGHT_ASSET_RESOURCES_PATH = "Assets/Resources/NightConfigData.asset";
        private const string THEME_ASSET_RESOURCES_PATH = "Assets/Resources/HanUITheme.asset";
        private const string PAUSE_VOLUME_PROFILE_PATH = "Assets/Resources/HanPauseBlurProfile.asset";
        private const string INTRO_VIDEO_PATH = "Assets/Videos/Han_Intro.mp4";

        [MenuItem("Han/Setup Full Game Loop UI & Managers")]
        public static void SetupFullGameLoopMenu()
        {
            SetupFullGameLoop();
        }

        public static void SetupFullGameLoop()
        {
            if (!Directory.Exists("Assets/Resources"))
            {
                Directory.CreateDirectory("Assets/Resources");
            }

            // 1. Ensure NightConfigData & HanUITheme ScriptableObject assets
            NightConfigData nightConfig = EnsureNightConfigAsset();
            TMP_FontAsset font = HanUITheme.GetProjectFont();
            EnsureThemeAsset(font);

            // 2. Ensure EventSystem exists in the scene for Canvas & Gamepad/Keyboard navigation
            EnsureEventSystem();

            // 3. Create or rebuild GameLoopCanvas
            GameObject existingCanvas = GameObject.Find("GameLoopCanvas");
            if (existingCanvas != null)
            {
                Object.DestroyImmediate(existingCanvas);
            }

            GameObject canvasObj = new GameObject("GameLoopCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.dynamicPixelsPerUnit = 16f;

            canvasObj.AddComponent<GraphicRaycaster>();

            // --- A. Screen Fade Overlay (Deep Sepia Ink - never #000000) ---
            GameObject fadeOverlayObj = CreateFullScreenPanel(canvasObj.transform, "ScreenFadeOverlay", HanUITheme.DeepSepiaInk);
            AddVignetteGrainOverlay(fadeOverlayObj.transform, 0.45f);
            CanvasGroup screenFadeGroup = fadeOverlayObj.AddComponent<CanvasGroup>();
            screenFadeGroup.alpha = 0f;
            screenFadeGroup.blocksRaycasts = false;
            screenFadeGroup.interactable = false;

            // --- B. In-Game Archival HUD Panel ---
            GameObject hudPanel = CreateFullScreenPanel(canvasObj.transform, "InGameHudPanel", new Color(0f, 0f, 0f, 0f));
            var hudBgImg = hudPanel.GetComponent<Image>();
            if (hudBgImg != null) hudBgImg.raycastTarget = false;

            GameObject hudBadge = CreateArchivalModalCard(
                hudPanel.transform,
                "HudTopRightDossier",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-28f, -24f),
                new Vector2(450f, 104f),
                HanUITheme.DeepSepiaInk);

            TMP_Text hudNightTitleText = CreateTMPText(hudBadge.transform, "HudNightTitleText", "[ NIGHT ONE ]", font, 22f, FontStyles.Bold, TextAlignmentOptions.TopLeft, HanUITheme.BurnishedGold);
            SetRect(hudNightTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(4f, -14f), new Vector2(-36f, 30f));

            CreateOrnamentalDivider(hudBadge.transform, "HudDivider", new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(400f, 6f), 0.55f);

            TMP_Text hudObjectiveText = CreateTMPText(hudBadge.transform, "HudObjectiveText", "Inspect arriving travelers at the ledger.  [ESC] Pause  |  [F6] End Night", font, 14.5f, FontStyles.Normal, TextAlignmentOptions.BottomLeft, HanUITheme.AgedPaper);
            SetRect(hudObjectiveText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(4f, 12f), new Vector2(-36f, 38f));
            hudPanel.SetActive(false);

            // --- C. Main Menu Panel (Archival Ledger Dossier Style) ---
            GameObject mainMenuPanel = CreateFullScreenPanel(canvasObj.transform, "MainMenuPanel", HanUITheme.ModalBackdropDim);
            AddVignetteGrainOverlay(mainMenuPanel.transform, 0.65f);
            CanvasGroup mainMenuCanvasGroup = mainMenuPanel.AddComponent<CanvasGroup>();

            GameObject menuCard = CreateArchivalModalCard(
                mainMenuPanel.transform,
                "MainMenuCard",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(600f, 660f),
                HanUITheme.CharcoalSurface);

            TMP_Text dossierTag = CreateTMPText(menuCard.transform, "DossierHeaderTag", "ARCHIVA HANULUI  |  ANNO 1648", font, 14f, FontStyles.Bold | FontStyles.UpperCase, TextAlignmentOptions.Center, HanUITheme.SubduedBrass);
            SetRect(dossierTag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(480f, 24f));

            TMP_Text gameTitle = CreateTMPText(menuCard.transform, "GameTitle", "HANUL ANCUȚEI", font, 46f, FontStyles.Bold, TextAlignmentOptions.Center, HanUITheme.WarmParchment);
            SetRect(gameTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(520f, 58f));

            TMP_Text gameSubtitle = CreateTMPText(menuCard.transform, "GameSubtitle", "THE INNKEEPER'S LEDGER", font, 18f, FontStyles.Italic, TextAlignmentOptions.Center, HanUITheme.AgedPaper);
            SetRect(gameSubtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(460f, 28f));

            CreateOrnamentalDivider(menuCard.transform, "TitleDivider", new Vector2(0.5f, 1f), new Vector2(0f, -162f), new Vector2(460f, 10f), 0.85f);

            Button playButton = CreateTactilePlateButton(menuCard.transform, "PlayButton", "BEGIN WATCH", font, new Vector2(0f, 72f), new Vector2(380f, 60f), isPrimary: true);
            Button settingsButton = CreateTactilePlateButton(menuCard.transform, "SettingsButton", "LEDGER SETTINGS", font, new Vector2(0f, -4f), new Vector2(380f, 56f), isPrimary: false);
            Button creditsButton = CreateTactilePlateButton(menuCard.transform, "CreditsButton", "ARCHIVAL CREDITS", font, new Vector2(0f, -78f), new Vector2(380f, 56f), isPrimary: false);
            Button exitButton = CreateTactilePlateButton(menuCard.transform, "ExitButton", "CLOSE LEDGER (EXIT)", font, new Vector2(0f, -152f), new Vector2(380f, 56f), isPrimary: false);

            TMP_Text footerNote = CreateTMPText(menuCard.transform, "MenuFooterNote", "Use Mouse or [W/S / Arrow Keys + Enter] to Navigate", font, 13.5f, FontStyles.Italic, TextAlignmentOptions.Center, HanUITheme.DisabledInk);
            SetRect(footerNote.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(480f, 22f));

            // --- D. Intro Video Panel ---
            GameObject introVideoPanel = CreateFullScreenPanel(canvasObj.transform, "IntroVideoPanel", HanUITheme.DeepSepiaInk);
            GameObject rawImgObj = new GameObject("VideoRawImage");
            rawImgObj.transform.SetParent(introVideoPanel.transform, false);
            RectTransform rawRt = rawImgObj.AddComponent<RectTransform>();
            SetFullStretch(rawRt);
            RawImage videoRawImage = rawImgObj.AddComponent<RawImage>();
            videoRawImage.color = Color.white;

            AddVignetteGrainOverlay(introVideoPanel.transform, 0.35f);

            VideoPlayer videoPlayer = introVideoPanel.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = false;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

            VideoClip introClip = AssetDatabase.LoadAssetAtPath<VideoClip>(INTRO_VIDEO_PATH);
            if (introClip != null)
            {
                videoPlayer.clip = introClip;
            }

            Button skipVideoButton = CreateTactilePlateButton(introVideoPanel.transform, "SkipVideoButton", "SKIP INTRO [SPACE]", font, new Vector2(-170f, 48f), new Vector2(280f, 50f), isPrimary: false);
            SetRect(skipVideoButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-38f, 34f), new Vector2(280f, 50f));
            introVideoPanel.SetActive(false);

            // --- E. Night Teaser Panel ---
            GameObject nightTeaserPanel = CreateFullScreenPanel(canvasObj.transform, "NightTeaserPanel", HanUITheme.DeepSepiaInk);
            AddVignetteGrainOverlay(nightTeaserPanel.transform, 0.55f);
            CanvasGroup nightTeaserCanvasGroup = nightTeaserPanel.AddComponent<CanvasGroup>();
            nightTeaserCanvasGroup.alpha = 0f;

            TMP_Text teaserTitleText = CreateTMPText(nightTeaserPanel.transform, "TeaserTitleText", "--  NIGHT ONE  --", font, 22f, FontStyles.Bold | FontStyles.UpperCase, TextAlignmentOptions.Center, HanUITheme.SubduedBrass);
            SetRect(teaserTitleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 84f), new Vector2(900f, 38f));

            CreateOrnamentalDivider(nightTeaserPanel.transform, "TeaserTopDivider", new Vector2(0.5f, 0.5f), new Vector2(0f, 52f), new Vector2(520f, 8f), 0.75f);

            TMP_Text teaserHeadlineText = CreateTMPText(nightTeaserPanel.transform, "TeaserHeadlineText", "Night One, good luck", font, 54f, FontStyles.Bold, TextAlignmentOptions.Center, HanUITheme.WarmParchment);
            SetRect(teaserHeadlineText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(1200f, 86f));

            CreateOrnamentalDivider(nightTeaserPanel.transform, "TeaserBottomDivider", new Vector2(0.5f, 0.5f), new Vector2(0f, -52f), new Vector2(520f, 8f), 0.75f);

            TMP_Text teaserSubtitleText = CreateTMPText(nightTeaserPanel.transform, "TeaserSubtitleText", "good luck", font, 23f, FontStyles.Italic, TextAlignmentOptions.Center, HanUITheme.AgedPaper);
            SetRect(teaserSubtitleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -86f), new Vector2(900f, 40f));
            nightTeaserPanel.SetActive(false);

            // --- F. In-Game Pause Menu Panel (Smooth Dim + Vignette + Framed Ledger Card) ---
            GameObject pauseMenuPanel = CreateFullScreenPanel(canvasObj.transform, "PauseMenuPanel", HanUITheme.ModalBackdropDim);
            AddVignetteGrainOverlay(pauseMenuPanel.transform, 0.72f);
            CanvasGroup pauseMenuCanvasGroup = pauseMenuPanel.AddComponent<CanvasGroup>();
            pauseMenuCanvasGroup.alpha = 0f;

            GameObject pauseCard = CreateArchivalModalCard(
                pauseMenuPanel.transform,
                "PauseModalCard",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(540f, 500f),
                HanUITheme.CharcoalSurface);

            TMP_Text pauseSubTag = CreateTMPText(pauseCard.transform, "PauseDossierTag", "DESK WATCH SUSPENDED", font, 13.5f, FontStyles.Bold | FontStyles.UpperCase, TextAlignmentOptions.Center, HanUITheme.SubduedBrass);
            SetRect(pauseSubTag.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(400f, 22f));

            TMP_Text pauseTitle = CreateTMPText(pauseCard.transform, "PauseTitle", "PAUSED", font, 42f, FontStyles.Bold, TextAlignmentOptions.Center, HanUITheme.WarmParchment);
            SetRect(pauseTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(420f, 52f));

            CreateOrnamentalDivider(pauseCard.transform, "PauseDivider", new Vector2(0.5f, 1f), new Vector2(0f, -124f), new Vector2(420f, 10f), 0.85f);

            Button resumeButton = CreateTactilePlateButton(pauseCard.transform, "ResumeButton", "RESUME WATCH", font, new Vector2(0f, 38f), new Vector2(360f, 58f), isPrimary: true);
            Button pauseSettingsButton = CreateTactilePlateButton(pauseCard.transform, "PauseSettingsButton", "LEDGER SETTINGS", font, new Vector2(0f, -38f), new Vector2(360f, 56f), isPrimary: false);
            Button pauseExitButton = CreateTactilePlateButton(pauseCard.transform, "PauseExitButton", "EXIT GAME", font, new Vector2(0f, -114f), new Vector2(360f, 56f), isPrimary: false);
            pauseMenuPanel.SetActive(false);

            // --- G. Settings Modal Sub-Panel (Shared between Main Menu & Pause Menu) ---
            GameObject settingsPanel = CreateFullScreenPanel(canvasObj.transform, "SettingsPanel", new Color(0.06f, 0.05f, 0.04f, 0.78f));
            CanvasGroup settingsCanvasGroup = settingsPanel.AddComponent<CanvasGroup>();
            GameObject settingsCard = CreateArchivalModalCard(
                settingsPanel.transform,
                "SettingsModalCard",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(600f, 520f),
                HanUITheme.CharcoalSurface);

            TMP_Text settingsTitle = CreateTMPText(settingsCard.transform, "SettingsTitle", "LEDGER SETTINGS", font, 34f, FontStyles.Bold, TextAlignmentOptions.Center, HanUITheme.WarmParchment);
            SetRect(settingsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(480f, 46f));

            CreateOrnamentalDivider(settingsCard.transform, "SettingsDivider", new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(460f, 8f), 0.8f);

            Slider masterVolSlider = CreateArchivalSliderRow(settingsCard.transform, "MasterVolumeRow", "MASTER AUDIO VOLUME", font, new Vector2(0f, 92f), 0f, 1f, 0.85f);
            Slider lookSensSlider = CreateArchivalSliderRow(settingsCard.transform, "LookSensitivityRow", "CAMERA LOOK SENSITIVITY", font, new Vector2(0f, 22f), 4f, 45f, 20f);
            Toggle doorHighlightToggle = CreateArchivalToggleRow(settingsCard.transform, "DoorHighlightRow", "INTERACTION OUTLINE HIGHLIGHTS", font, new Vector2(0f, -46f), true);

            TMP_Text controlsHint = CreateTMPText(settingsCard.transform, "ControlsHint", "[ESC] Pause  |  [TAB] Guest Book  |  [E / SPACE] Advance Dialogue  |  [F6] End Night", font, 14f, FontStyles.Italic, TextAlignmentOptions.Center, HanUITheme.AgedPaper);
            SetRect(controlsHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(520f, 26f));

            Button settingsBackButton = CreateTactilePlateButton(settingsCard.transform, "SettingsBackButton", "RETURN", font, new Vector2(0f, -182f), new Vector2(280f, 54f), isPrimary: true);
            settingsPanel.SetActive(false);

            // --- H. Credits Modal Sub-Panel ---
            GameObject creditsPanel = CreateFullScreenPanel(canvasObj.transform, "CreditsPanel", new Color(0.06f, 0.05f, 0.04f, 0.78f));
            CanvasGroup creditsCanvasGroup = creditsPanel.AddComponent<CanvasGroup>();
            GameObject creditsCard = CreateArchivalModalCard(
                creditsPanel.transform,
                "CreditsModalCard",
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(580f, 460f),
                HanUITheme.CharcoalSurface);

            TMP_Text creditsTitle = CreateTMPText(creditsCard.transform, "CreditsTitle", "ARCHIVAL CREDITS", font, 34f, FontStyles.Bold, TextAlignmentOptions.Center, HanUITheme.WarmParchment);
            SetRect(creditsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(460f, 46f));

            CreateOrnamentalDivider(creditsCard.transform, "CreditsDivider", new Vector2(0.5f, 1f), new Vector2(0f, -92f), new Vector2(440f, 8f), 0.8f);

            TMP_Text creditsBody = CreateTMPText(creditsCard.transform, "CreditsBody", "PROIECTUL ANCUȚA\n\nNarrative, Systems & Bureaucratic Desk Direction\nTransylvanian Inn Mystery Team\n\nArchival Typography & Tactile Interface System", font, 19.5f, FontStyles.Normal, TextAlignmentOptions.Center, HanUITheme.AgedPaper);
            SetRect(creditsBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 18f), new Vector2(480f, 190f));

            Button creditsBackButton = CreateTactilePlateButton(creditsCard.transform, "CreditsBackButton", "RETURN", font, new Vector2(0f, -158f), new Vector2(280f, 54f), isPrimary: true);
            creditsPanel.SetActive(false);

            // 4. Create or update GameLoopManager + URP Pause Blur Volume + Audio + Focus Navigator
            GameObject existingManager = GameObject.Find("GameLoopManager");
            if (existingManager != null)
            {
                Object.DestroyImmediate(existingManager);
            }

            GameObject managerObj = new GameObject("GameLoopManager");
            AudioSource uiAudio = managerObj.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            uiAudio.ignoreListenerPause = true;

            HanUIAudioManager uiAudioManager = managerObj.AddComponent<HanUIAudioManager>();
            HanUIFocusNavigator focusNavigator = managerObj.AddComponent<HanUIFocusNavigator>();
            GameLoopManager loopManager = managerObj.AddComponent<GameLoopManager>();
            managerObj.AddComponent<NightCompletionTrigger>();

            // Create URP Pause Depth-of-Field Blur Volume child
            Volume pauseBlurVolume = CreatePauseBlurVolume(managerObj.transform);

            // Create DeskPOVAnchor child at Player's desk position
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            GameObject deskAnchorObj = new GameObject("DeskPOVAnchor");
            deskAnchorObj.transform.SetParent(managerObj.transform, false);
            if (playerObj != null)
            {
                deskAnchorObj.transform.SetPositionAndRotation(playerObj.transform.position, playerObj.transform.rotation);
            }
            else
            {
                deskAnchorObj.transform.SetPositionAndRotation(new Vector3(-36.25f, 0.35f, 94.6f), Quaternion.Euler(0f, 180f, 0f));
            }

            HanGameDirector director = Object.FindFirstObjectByType<HanGameDirector>(FindObjectsInactive.Include);

            // 5. Bind all SerializedProperties on HanUIFocusNavigator
            SerializedObject navSo = new SerializedObject(focusNavigator);
            navSo.FindProperty("mainMenuDefaultSelectable").objectReferenceValue = playButton;
            navSo.FindProperty("pauseMenuDefaultSelectable").objectReferenceValue = resumeButton;
            navSo.FindProperty("settingsDefaultSelectable").objectReferenceValue = settingsBackButton;
            navSo.FindProperty("creditsDefaultSelectable").objectReferenceValue = creditsBackButton;
            navSo.FindProperty("introVideoDefaultSelectable").objectReferenceValue = skipVideoButton;
            navSo.ApplyModifiedProperties();

            // 6. Bind all SerializedProperties on GameLoopManager
            SerializedObject so = new SerializedObject(loopManager);
            so.FindProperty("mainMenuPanel").objectReferenceValue = mainMenuPanel;
            so.FindProperty("mainMenuCanvasGroup").objectReferenceValue = mainMenuCanvasGroup;
            so.FindProperty("mainMenuCardRect").objectReferenceValue = menuCard.GetComponent<RectTransform>();
            so.FindProperty("playButton").objectReferenceValue = playButton;
            so.FindProperty("settingsButton").objectReferenceValue = settingsButton;
            so.FindProperty("creditsButton").objectReferenceValue = creditsButton;
            so.FindProperty("exitButton").objectReferenceValue = exitButton;

            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.FindProperty("settingsCanvasGroup").objectReferenceValue = settingsCanvasGroup;
            so.FindProperty("settingsCardRect").objectReferenceValue = settingsCard.GetComponent<RectTransform>();
            so.FindProperty("settingsBackButton").objectReferenceValue = settingsBackButton;
            so.FindProperty("masterVolumeSlider").objectReferenceValue = masterVolSlider;
            so.FindProperty("lookSensitivitySlider").objectReferenceValue = lookSensSlider;
            so.FindProperty("doorHighlightToggle").objectReferenceValue = doorHighlightToggle;

            so.FindProperty("creditsPanel").objectReferenceValue = creditsPanel;
            so.FindProperty("creditsCanvasGroup").objectReferenceValue = creditsCanvasGroup;
            so.FindProperty("creditsCardRect").objectReferenceValue = creditsCard.GetComponent<RectTransform>();
            so.FindProperty("creditsBackButton").objectReferenceValue = creditsBackButton;

            so.FindProperty("introVideoPanel").objectReferenceValue = introVideoPanel;
            so.FindProperty("introVideoPlayer").objectReferenceValue = videoPlayer;
            so.FindProperty("introVideoRawImage").objectReferenceValue = videoRawImage;
            so.FindProperty("introVideoClip").objectReferenceValue = introClip;
            so.FindProperty("skipVideoButton").objectReferenceValue = skipVideoButton;

            so.FindProperty("nightConfigData").objectReferenceValue = nightConfig;
            so.FindProperty("nightTeaserPanel").objectReferenceValue = nightTeaserPanel;
            so.FindProperty("nightTeaserCanvasGroup").objectReferenceValue = nightTeaserCanvasGroup;
            so.FindProperty("teaserHeadlineText").objectReferenceValue = teaserHeadlineText;
            so.FindProperty("teaserTitleText").objectReferenceValue = teaserTitleText;
            so.FindProperty("teaserSubtitleText").objectReferenceValue = teaserSubtitleText;

            so.FindProperty("screenFadeCanvasGroup").objectReferenceValue = screenFadeGroup;
            so.FindProperty("deskPovAnchor").objectReferenceValue = deskAnchorObj.transform;
            so.FindProperty("inGameHudPanel").objectReferenceValue = hudPanel;
            so.FindProperty("hudNightTitleText").objectReferenceValue = hudNightTitleText;
            so.FindProperty("hudObjectiveText").objectReferenceValue = hudObjectiveText;

            so.FindProperty("pauseMenuPanel").objectReferenceValue = pauseMenuPanel;
            so.FindProperty("pauseMenuCanvasGroup").objectReferenceValue = pauseMenuCanvasGroup;
            so.FindProperty("pauseModalCardRect").objectReferenceValue = pauseCard.GetComponent<RectTransform>();
            so.FindProperty("pauseBlurVolume").objectReferenceValue = pauseBlurVolume;
            so.FindProperty("resumeButton").objectReferenceValue = resumeButton;
            so.FindProperty("pauseSettingsButton").objectReferenceValue = pauseSettingsButton;
            so.FindProperty("pauseExitButton").objectReferenceValue = pauseExitButton;

            so.FindProperty("gameDirector").objectReferenceValue = director;
            if (playerObj != null)
                so.FindProperty("playerTransform").objectReferenceValue = playerObj.transform;
            so.FindProperty("uiAudioSource").objectReferenceValue = uiAudio;
            so.FindProperty("uiAudioManager").objectReferenceValue = uiAudioManager;
            so.FindProperty("focusNavigator").objectReferenceValue = focusNavigator;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(managerObj);
            EditorUtility.SetDirty(canvasObj);
            EditorSceneManager.MarkSceneDirty(managerObj.scene);

            Debug.Log("[HanGameLoopSetup] Overhauled Archival UI/UX, Tactile Buttons, URP Pause Blur Volume, Audio Hooks, and Focus Navigator configured!");
        }

        // ------------------------------------------------------------------
        //  Asset & URP Volume Builders
        // ------------------------------------------------------------------
        private static NightConfigData EnsureNightConfigAsset()
        {
            NightConfigData asset = AssetDatabase.LoadAssetAtPath<NightConfigData>(NIGHT_ASSET_RESOURCES_PATH);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<NightConfigData>();
                asset.EnsureDefaultEntries();
                AssetDatabase.CreateAsset(asset, NIGHT_ASSET_RESOURCES_PATH);
                AssetDatabase.SaveAssets();
            }
            else
            {
                asset.EnsureDefaultEntries();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
            }
            return asset;
        }

        private static HanUITheme EnsureThemeAsset(TMP_FontAsset font)
        {
            HanUITheme theme = AssetDatabase.LoadAssetAtPath<HanUITheme>(THEME_ASSET_RESOURCES_PATH);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<HanUITheme>();
                theme.primaryFont = font;
                AssetDatabase.CreateAsset(theme, THEME_ASSET_RESOURCES_PATH);
                AssetDatabase.SaveAssets();
            }
            else
            {
                theme.primaryFont = font;
                EditorUtility.SetDirty(theme);
                AssetDatabase.SaveAssets();
            }
            return theme;
        }

        private static Volume CreatePauseBlurVolume(Transform parent)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PAUSE_VOLUME_PROFILE_PATH);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PAUSE_VOLUME_PROFILE_PATH);
            }

            if (!profile.TryGet(out DepthOfField dof))
            {
                dof = profile.Add<DepthOfField>(overrides: true);
            }
            dof.active = true;
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(0.45f);
            dof.focalLength.Override(72f);
            dof.aperture.Override(3.2f);

            if (!profile.TryGet(out Vignette vignette))
            {
                vignette = profile.Add<Vignette>(overrides: true);
            }
            vignette.active = true;
            vignette.color.Override(new Color(0.08f, 0.05f, 0.03f, 1f));
            vignette.intensity.Override(0.42f);
            vignette.smoothness.Override(0.55f);

            if (!profile.TryGet(out FilmGrain grain))
            {
                grain = profile.Add<FilmGrain>(overrides: true);
            }
            grain.active = true;
            grain.type.Override(FilmGrainLookup.Medium1);
            grain.intensity.Override(0.35f);
            grain.response.Override(0.8f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            GameObject volObj = new GameObject("PauseBlurVolume");
            volObj.transform.SetParent(parent, false);
            Volume vol = volObj.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 50f;
            vol.weight = 0f;
            vol.sharedProfile = profile;
            return vol;
        }

        private static void EnsureEventSystem()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (es == null)
            {
                GameObject esObj = new GameObject("EventSystem");
                esObj.AddComponent<EventSystem>();
                esObj.AddComponent<InputSystemUIInputModule>();
            }
        }

        // ------------------------------------------------------------------
        //  Archival UI Element Builders
        // ------------------------------------------------------------------
        private static GameObject CreateFullScreenPanel(Transform parent, string name, Color bgColor)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rt = panel.AddComponent<RectTransform>();
            SetFullStretch(rt);
            Image img = panel.AddComponent<Image>();
            img.color = bgColor;
            return panel;
        }

        private static void AddVignetteGrainOverlay(Transform parent, float opacity)
        {
            GameObject vigObj = new GameObject("VignetteGrainOverlay");
            vigObj.transform.SetParent(parent, false);
            RectTransform rt = vigObj.AddComponent<RectTransform>();
            SetFullStretch(rt);
            Image img = vigObj.AddComponent<Image>();
            img.sprite = HanUITheme.GetVignetteGrainSprite();
            img.color = new Color(1f, 1f, 1f, opacity);
            img.raycastTarget = false;
        }

        private static GameObject CreateArchivalModalCard(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Color surfaceColor)
        {
            GameObject cardObj = new GameObject(name);
            cardObj.transform.SetParent(parent, false);
            RectTransform rt = cardObj.AddComponent<RectTransform>();
            SetRect(rt, anchorMin, anchorMax, pivot, anchoredPos, sizeDelta);

            Image bgImg = cardObj.AddComponent<Image>();
            bgImg.sprite = HanUITheme.GetFramedCardSprite();
            bgImg.type = Image.Type.Sliced;
            bgImg.color = surfaceColor;

            Shadow cardShadow = cardObj.AddComponent<Shadow>();
            cardShadow.effectColor = new Color(0.04f, 0.03f, 0.02f, 0.85f);
            cardShadow.effectDistance = new Vector2(6f, -8f);

            // Inner Double Brass Border Frame
            GameObject innerBorderObj = new GameObject("InnerBrassFrame");
            innerBorderObj.transform.SetParent(cardObj.transform, false);
            RectTransform borderRt = innerBorderObj.AddComponent<RectTransform>();
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(10f, 10f);
            borderRt.offsetMax = new Vector2(-10f, -10f);
            Image borderImg = innerBorderObj.AddComponent<Image>();
            borderImg.sprite = HanUITheme.GetDoubleBorderFrameSprite();
            borderImg.type = Image.Type.Sliced;
            borderImg.color = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.55f);
            borderImg.raycastTarget = false;

            // Subtle Paper Grain Overlay inside Card
            AddVignetteGrainOverlay(cardObj.transform, 0.22f);

            return cardObj;
        }

        private static void CreateOrnamentalDivider(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, Vector2 size, float alpha)
        {
            GameObject divObj = new GameObject(name);
            divObj.transform.SetParent(parent, false);
            RectTransform rt = divObj.AddComponent<RectTransform>();
            SetRect(rt, anchor, anchor, new Vector2(0.5f, 0.5f), anchoredPos, size);
            Image img = divObj.AddComponent<Image>();
            img.sprite = HanUITheme.GetDividerOrnamentSprite();
            img.color = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, alpha);
            img.raycastTarget = false;
        }

        private static TMP_Text CreateTMPText(
            Transform parent,
            string name,
            string text,
            TMP_FontAsset font,
            float fontSize,
            FontStyles style,
            TextAlignmentOptions alignment,
            Color color)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            HanUITheme.ApplyCrispSDF(tmp, font);
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>
        /// Builds a styled vintage tab/plate button with a HanTactileButton controller,
        /// left brass index tab, animated underline draw-in bar, right ornamental glyph, and tactile shadow.
        /// </summary>
        private static Button CreateTactilePlateButton(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font,
            Vector2 anchoredPos,
            Vector2 size,
            bool isPrimary)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);
            RectTransform rt = btnObj.AddComponent<RectTransform>();
            SetRect(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, size);

            Image plateImg = btnObj.AddComponent<Image>();
            plateImg.sprite = HanUITheme.GetButtonPlateSprite();
            plateImg.type = Image.Type.Sliced;
            plateImg.color = isPrimary ? HanUITheme.StampCrimson : HanUITheme.MutedSlate;

            Shadow shadow = btnObj.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.04f, 0.03f, 0.02f, 0.85f);
            shadow.effectDistance = new Vector2(3f, -4f);

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(HanUITheme.SubduedBrass.r, HanUITheme.SubduedBrass.g, HanUITheme.SubduedBrass.b, 0.65f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button btn = btnObj.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = plateImg;

            // 1. Left Brass Index Tab
            GameObject leftTabObj = new GameObject("LeftBrassTab");
            leftTabObj.transform.SetParent(btnObj.transform, false);
            RectTransform leftTabRt = leftTabObj.AddComponent<RectTransform>();
            leftTabRt.anchorMin = new Vector2(0f, 0f);
            leftTabRt.anchorMax = new Vector2(0f, 1f);
            leftTabRt.pivot = new Vector2(0f, 0.5f);
            leftTabRt.anchoredPosition = Vector2.zero;
            leftTabRt.sizeDelta = new Vector2(6f, 0f);
            Image leftTabImg = leftTabObj.AddComponent<Image>();
            leftTabImg.color = isPrimary ? HanUITheme.BurnishedGold : HanUITheme.SubduedBrass;
            leftTabImg.raycastTarget = false;

            // 2. Animated Bottom Underline Draw-In Bar
            GameObject ulObj = new GameObject("UnderlineBar");
            ulObj.transform.SetParent(btnObj.transform, false);
            RectTransform ulRt = ulObj.AddComponent<RectTransform>();
            ulRt.anchorMin = new Vector2(0f, 0f);
            ulRt.anchorMax = new Vector2(0f, 0f);
            ulRt.pivot = new Vector2(0f, 0f);
            ulRt.anchoredPosition = new Vector2(18f, 5f);
            ulRt.sizeDelta = new Vector2(0f, 2.2f);
            Image ulImg = ulObj.AddComponent<Image>();
            ulImg.color = HanUITheme.BurnishedGold;
            ulImg.raycastTarget = false;

            // 3. Button Label (Crisp TMP SDF)
            TMP_Text txt = CreateTMPText(btnObj.transform, "Label", label, font, 21f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, HanUITheme.WarmParchment);
            RectTransform txtRt = txt.rectTransform;
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            txtRt.offsetMin = new Vector2(24f, 2f);
            txtRt.offsetMax = new Vector2(-36f, -2f);

            // 4. Right Ornamental Indicator Glyph
            TMP_Text glyph = CreateTMPText(btnObj.transform, "RightGlyph", isPrimary ? ">>" : ">", font, 20f, FontStyles.Bold, TextAlignmentOptions.MidlineRight, HanUITheme.BurnishedGold);
            RectTransform glyphRt = glyph.rectTransform;
            glyphRt.anchorMin = Vector2.zero;
            glyphRt.anchorMax = Vector2.one;
            glyphRt.offsetMin = new Vector2(16f, 0f);
            glyphRt.offsetMax = new Vector2(-18f, 0f);

            // 5. Attach & Bind HanTactileButton
            HanTactileButton tactile = btnObj.AddComponent<HanTactileButton>();
            SerializedObject tSo = new SerializedObject(tactile);
            tSo.FindProperty("isPrimaryAction").boolValue = isPrimary;
            tSo.FindProperty("targetButton").objectReferenceValue = btn;
            tSo.FindProperty("plateImage").objectReferenceValue = plateImg;
            tSo.FindProperty("leftTabImage").objectReferenceValue = leftTabImg;
            tSo.FindProperty("underlineRect").objectReferenceValue = ulRt;
            tSo.FindProperty("underlineImage").objectReferenceValue = ulImg;
            tSo.FindProperty("labelText").objectReferenceValue = txt;
            tSo.FindProperty("rightGlyphText").objectReferenceValue = glyph;
            tSo.FindProperty("dropShadow").objectReferenceValue = shadow;
            tSo.FindProperty("maxUnderlineWidth").floatValue = size.x - 42f;
            tSo.ApplyModifiedProperties();

            return btn;
        }

        private static Slider CreateArchivalSliderRow(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font,
            Vector2 anchoredPos,
            float minVal,
            float maxVal,
            float defaultVal)
        {
            GameObject rowObj = new GameObject(name);
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRt = rowObj.AddComponent<RectTransform>();
            SetRect(rowRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(480f, 54f));

            TMP_Text lbl = CreateTMPText(rowObj.transform, "Label", label, font, 16f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, HanUITheme.AgedPaper);
            SetRect(lbl.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.55f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 40f));

            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(rowObj.transform, false);
            RectTransform slRt = sliderObj.AddComponent<RectTransform>();
            SetRect(slRt, new Vector2(0.58f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 24f));

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRt = bgObj.AddComponent<RectTransform>();
            SetRect(bgRt, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.color = HanUITheme.DeepSepiaInk;

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform faRt = fillArea.AddComponent<RectTransform>();
            SetRect(faRt, new Vector2(0f, 0.3f), new Vector2(1f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6f, 0f));

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillArea.transform, false);
            RectTransform fillRt = fillObj.AddComponent<RectTransform>();
            SetFullStretch(fillRt);
            Image fillImg = fillObj.AddComponent<Image>();
            fillImg.color = HanUITheme.SubduedBrass;

            GameObject handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderObj.transform, false);
            RectTransform haRt = handleArea.AddComponent<RectTransform>();
            SetFullStretch(haRt);

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleArea.transform, false);
            RectTransform hRt = handleObj.AddComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(14f, 24f);
            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = HanUITheme.WarmParchment;

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = hRt;
            slider.targetGraphic = handleImg;
            slider.minValue = minVal;
            slider.maxValue = maxVal;
            slider.value = defaultVal;

            return slider;
        }

        private static Toggle CreateArchivalToggleRow(
            Transform parent,
            string name,
            string label,
            TMP_FontAsset font,
            Vector2 anchoredPos,
            bool defaultOn)
        {
            GameObject rowObj = new GameObject(name);
            rowObj.transform.SetParent(parent, false);
            RectTransform rowRt = rowObj.AddComponent<RectTransform>();
            SetRect(rowRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(480f, 50f));

            TMP_Text lbl = CreateTMPText(rowObj.transform, "Label", label, font, 16f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft, HanUITheme.AgedPaper);
            SetRect(lbl.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.82f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 40f));

            GameObject boxObj = new GameObject("Checkbox");
            boxObj.transform.SetParent(rowObj.transform, false);
            RectTransform boxRt = boxObj.AddComponent<RectTransform>();
            SetRect(boxRt, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(32f, 32f));
            Image boxImg = boxObj.AddComponent<Image>();
            boxImg.sprite = HanUITheme.GetButtonPlateSprite();
            boxImg.type = Image.Type.Sliced;
            boxImg.color = HanUITheme.DeepSepiaInk;

            GameObject checkObj = new GameObject("Checkmark");
            checkObj.transform.SetParent(boxObj.transform, false);
            RectTransform checkRt = checkObj.AddComponent<RectTransform>();
            SetRect(checkRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20f, 20f));
            Image checkImg = checkObj.AddComponent<Image>();
            checkImg.color = HanUITheme.BurnishedGold;

            Toggle toggle = rowObj.AddComponent<Toggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            toggle.isOn = defaultOn;

            return toggle;
        }

        private static void SetFullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }
    }
}
#endif
