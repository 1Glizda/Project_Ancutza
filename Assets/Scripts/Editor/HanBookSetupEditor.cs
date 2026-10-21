#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using TMPro;

namespace Han.CheckIn.Editor
{
    public static class HanBookSetupEditor
    {
        private const string LAYER_NAME = "GuestBook";

        [MenuItem("Han/Setup 3D Book Viewmodel on Main Camera")]
        public static void SetupViewmodelMenuItem()
        {
            SetupViewmodel(false);
        }

        public static void SetupViewmodel(bool showDialog = false)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = Object.FindFirstObjectByType<Camera>();
            }

            if (mainCam == null)
            {
                if (showDialog) EditorUtility.DisplayDialog("Error", "No Camera found in current scene!", "OK");
                return;
            }

            // 1. Ensure Layer Exists
            EnsureLayerExists(LAYER_NAME);
            int guestBookLayer = LayerMask.NameToLayer(LAYER_NAME);
            if (guestBookLayer < 0) guestBookLayer = 8;

            // 2. Configure Main Camera (Base)
            var mainCamData = mainCam.GetUniversalAdditionalCameraData();
            if (mainCamData != null)
            {
                mainCam.cullingMask &= ~(1 << guestBookLayer);
            }

            // 3. Create or find Overlay Camera
            Camera overlayCam = null;
            Transform existingOverlay = mainCam.transform.Find("BookOverlayCamera");
            if (existingOverlay != null)
            {
                overlayCam = existingOverlay.GetComponent<Camera>();
            }
            else
            {
                GameObject camObj = new GameObject("BookOverlayCamera");
                camObj.transform.SetParent(mainCam.transform, false);
                overlayCam = camObj.AddComponent<Camera>();
            }

            overlayCam.clearFlags = CameraClearFlags.Depth;
            overlayCam.cullingMask = 1 << guestBookLayer;
            overlayCam.nearClipPlane = 0.01f;
            overlayCam.farClipPlane = 10f;
            overlayCam.fieldOfView = 52f;

            var overlayCamData = overlayCam.GetUniversalAdditionalCameraData();
            if (overlayCamData != null)
            {
                overlayCamData.renderType = CameraRenderType.Overlay;
                overlayCamData.renderPostProcessing = false;
            }

            if (mainCamData != null)
            {
                mainCamData.cameraStack.RemoveAll(c => c == null);
                if (!mainCamData.cameraStack.Contains(overlayCam))
                {
                    mainCamData.cameraStack.Add(overlayCam);
                }
            }

            // 4. Create or clean up 3D Book Viewmodel root
            Transform existingViewmodel = mainCam.transform.Find("Han_3DBook_Viewmodel");
            GameObject viewmodelObj = existingViewmodel != null ? existingViewmodel.gameObject : new GameObject("Han_3DBook_Viewmodel");
            viewmodelObj.transform.SetParent(mainCam.transform, false);
            viewmodelObj.transform.localPosition = new Vector3(0f, -0.015f, 0.48f);
            viewmodelObj.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);

            var controller = viewmodelObj.GetComponent<CheckInBookViewController>();
            if (controller == null) controller = viewmodelObj.AddComponent<CheckInBookViewController>();

            var audioSource = viewmodelObj.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = viewmodelObj.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // Clean up any old children to rebuild cleanly
            for (int i = viewmodelObj.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(viewmodelObj.transform.GetChild(i).gameObject);
            }

            // 5. 3D Open Ledger Mesh with Parchment Material
            GameObject meshObj = new GameObject("Book3D_Mesh");
            meshObj.transform.SetParent(viewmodelObj.transform, false);
            meshObj.transform.localPosition = Vector3.zero;
            meshObj.transform.localRotation = Quaternion.identity;

            var meshGen = meshObj.AddComponent<OpenBookMeshGenerator>();
            meshGen.BuildMesh();

            var meshRenderer = meshObj.GetComponent<MeshRenderer>();
            Material parchmentMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Parchment_Pages.mat");
            if (parchmentMat == null || parchmentMat.shader == null || parchmentMat.shader.name.Contains("Error") || parchmentMat.shader.name.Contains("InternalErrorShader"))
            {
                Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpShader == null) urpShader = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (urpShader == null) urpShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (urpShader == null) urpShader = Shader.Find("Sprites/Default");
                
                parchmentMat = new Material(urpShader);
                Texture2D parchmentTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Parchment_Page.png");
                if (parchmentTex != null)
                {
                    parchmentMat.mainTexture = parchmentTex;
                    if (parchmentMat.HasProperty("_BaseMap")) parchmentMat.SetTexture("_BaseMap", parchmentTex);
                }
                parchmentMat.color = new Color(0.95f, 0.90f, 0.82f, 1f);
                AssetDatabase.CreateAsset(parchmentMat, "Assets/Materials/M_Parchment_Pages.mat");
            }
            else
            {
                // Ensure texture is bound
                Texture2D parchmentTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Parchment_Page.png");
                if (parchmentTex != null)
                {
                    parchmentMat.mainTexture = parchmentTex;
                    if (parchmentMat.HasProperty("_BaseMap")) parchmentMat.SetTexture("_BaseMap", parchmentTex);
                }
            }
            meshRenderer.sharedMaterial = parchmentMat;

            // 6. Diegetic Inspection Light (Warm soft candlelight, spread evenly)
            GameObject lightObj = new GameObject("InspectionLight");
            lightObj.transform.SetParent(viewmodelObj.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.28f, -0.28f);

            var lightComp = lightObj.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.color = new Color(1.0f, 0.82f, 0.55f, 1f);
            lightComp.range = 2.4f;
            lightComp.intensity = 0.35f;
            lightComp.cullingMask = 1 << guestBookLayer;

            // 7. Dynamic Data Holder & UI Link
            var guestBookUI = viewmodelObj.GetComponent<GuestBookUI>();
            if (guestBookUI == null) guestBookUI = viewmodelObj.AddComponent<GuestBookUI>();

            // Left Page Elements (Center X = -0.115m)
            var titleTmp = Create3DText(viewmodelObj.transform, "TitleText", "REGISTRUL HANULUI", new Vector3(-0.115f, 0.12f, -0.003f), 0.11f, FontStyles.Bold, TextAlignmentOptions.Center);
            
            // Portrait Sprite (2D Billboard - scaled to fit portrait frame on left page)
            GameObject portraitObj = Create2DSprite(viewmodelObj.transform, "GuestPortrait", new Vector3(-0.115f, 0.045f, -0.003f), new Vector3(0.024f, 0.024f, 0.024f), "Assets/Sprites/Florin.png", 15);
            
            var nameTmp = Create3DText(viewmodelObj.transform, "GuestNameText", "Florin din Vatra", new Vector3(-0.115f, -0.015f, -0.003f), 0.082f, FontStyles.Bold, TextAlignmentOptions.Center);
            var roomTmp = Create3DText(viewmodelObj.transform, "RoomNumberText", "Odaia #3", new Vector3(-0.115f, -0.042f, -0.003f), 0.072f, FontStyles.Bold, TextAlignmentOptions.Center);
            var arrivalTmp = Create3DText(viewmodelObj.transform, "ArrivalTimeText", "Sosire: Amurg", new Vector3(-0.115f, -0.068f, -0.003f), 0.068f, FontStyles.Bold, TextAlignmentOptions.Center);

            // Horse Badge Sprite (2D Billboard)
            GameObject horseObj = Create2DSprite(viewmodelObj.transform, "HorseBadge", new Vector3(-0.115f, -0.098f, -0.003f), new Vector3(0.065f, 0.065f, 0.065f), "Assets/Sprites/Horse_Icon.png", 15);

            // Right Page Elements (Center X = +0.115m)
            Create3DText(viewmodelObj.transform, "NotesHeader", "Mărturii & Însemnări", new Vector3(0.115f, 0.12f, -0.003f), 0.095f, FontStyles.Bold, TextAlignmentOptions.Center);
            var notesTmp = Create3DText(viewmodelObj.transform, "NotesBody", "Neguțător din Moldova. A sosit cu butoaie ferecate cu sare. Calul său murg trebuie ținut în grajdul de răsărit. Miros ușor de tămâie și pământ reavăn.", new Vector3(0.025f, 0.09f, -0.003f), 0.062f, FontStyles.Normal, TextAlignmentOptions.TopLeft, 0.18f);
            var counterTmp = Create3DText(viewmodelObj.transform, "PageCounter", "Înregistrarea 1 din 3", new Vector3(0.115f, -0.095f, -0.003f), 0.058f, FontStyles.Italic, TextAlignmentOptions.Center);

            // 8. 2D Billboard Hands (Left & Right gripping margins)
            Create2DSprite(viewmodelObj.transform, "Hand_Left_2D", new Vector3(-0.245f, -0.02f, -0.012f), new Vector3(0.35f, 0.35f, 0.35f), "Assets/Sprites/Hand_Left.png", 20);
            Create2DSprite(viewmodelObj.transform, "Hand_Right_2D", new Vector3(0.245f, -0.02f, -0.012f), new Vector3(0.35f, 0.35f, 0.35f), "Assets/Sprites/Hand_Right.png", 20);

            // 9. Assign Layers Recursively
            SetLayerRecursively(viewmodelObj, guestBookLayer);

            // 10. Wire controller references
            SerializedObject ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("cameraTransform").objectReferenceValue = mainCam.transform;
            ctrlSo.FindProperty("bookViewmodelTransform").objectReferenceValue = viewmodelObj.transform;
            ctrlSo.FindProperty("inspectionLight").objectReferenceValue = lightComp;
            ctrlSo.FindProperty("guestBookUI").objectReferenceValue = guestBookUI;
            ctrlSo.FindProperty("raisedLocalPosition").vector3Value = new Vector3(0f, -0.015f, 0.48f);
            ctrlSo.FindProperty("raisedLocalEuler").vector3Value = new Vector3(12f, 0f, 0f);
            ctrlSo.FindProperty("loweredLocalPosition").vector3Value = new Vector3(0f, -0.90f, 0.38f);
            ctrlSo.FindProperty("loweredLocalEuler").vector3Value = new Vector3(60f, 0f, 0f);
            ctrlSo.FindProperty("disableMovementWhileReading").boolValue = true;
            ctrlSo.FindProperty("containScreenLook").boolValue = true;
            ctrlSo.FindProperty("maxInspectionYawDelta").floatValue = 14f;
            ctrlSo.FindProperty("minInspectionPitch").floatValue = 38f;
            ctrlSo.FindProperty("maxInspectionPitch").floatValue = 65f;
            ctrlSo.ApplyModifiedProperties();

            // Wire GuestBookUI references
            SerializedObject uiSo = new SerializedObject(guestBookUI);
            uiSo.FindProperty("guestNameText").objectReferenceValue = nameTmp;
            uiSo.FindProperty("roomNumberText").objectReferenceValue = roomTmp;
            uiSo.FindProperty("arrivalTimeText").objectReferenceValue = arrivalTmp;
            uiSo.FindProperty("notesText").objectReferenceValue = notesTmp;
            uiSo.FindProperty("pageCounterText").objectReferenceValue = counterTmp;
            if (portraitObj != null)
            {
                uiSo.FindProperty("portraitRenderer").objectReferenceValue = portraitObj.GetComponent<SpriteRenderer>();
            }
            if (horseObj != null)
            {
                uiSo.FindProperty("horseIconRenderer").objectReferenceValue = horseObj.GetComponent<SpriteRenderer>();
                uiSo.FindProperty("horseStabledBadge").objectReferenceValue = horseObj;
            }
            uiSo.FindProperty("defaultSilhouetteSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Guest_Silhouette.png");
            uiSo.ApplyModifiedProperties();

            // Populate sample Transylvanian thriller guests
            Sprite florinSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Florin.png");
            Sprite silhouetteSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Guest_Silhouette.png");
            var sampleGuests = new System.Collections.Generic.List<GuestData>
            {
                new GuestData
                {
                    guestName = "Florin din Vatra",
                    roomNumber = 3,
                    arrivalTime = "Sunset",
                    hasHorse = true,
                    isCheckedIn = true,
                    portrait = florinSprite,
                    notes = "Neguțător din Moldova. A sosit cu butoaie ferecate cu sare. Calul său murg trebuie ținut în grajdul de răsărit. Miros ușor de tămâie și pământ reavăn."
                },
                new GuestData
                {
                    guestName = "Gheorghe Străinul",
                    roomNumber = 6,
                    arrivalTime = "Dusk",
                    hasHorse = false,
                    isCheckedIn = true,
                    portrait = silhouetteSprite,
                    notes = "Călător singuratic pe jos prin codru. Nu pare tulburat de urlatul lupilor. Evită oglinzile din han și poartă un medalion ruginit din argint."
                },
                new GuestData
                {
                    guestName = "Doamna Ilinca",
                    roomNumber = 2,
                    arrivalTime = "Midnight",
                    hasHorse = true,
                    isCheckedIn = false,
                    portrait = silhouetteSprite,
                    notes = "Sosită cu puțin înainte de miezul nopții. Refuză să-și ridice vălul negru de doliu. A cerut crengi de scoruș și sare la pragul odăii."
                }
            };
            guestBookUI.LoadGuestRegistry(sampleGuests, 0);

            // 11. Save as Prefab
            string prefabPath = "Assets/Prefabs/Han_3DBook_Viewmodel.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(viewmodelObj, prefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[HanBookSetup] Saved prefab to {prefabPath}");

            Selection.activeGameObject = viewmodelObj;
            EditorUtility.SetDirty(mainCam.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(mainCam.gameObject.scene);

            // 12. Auto-capture updated screenshot
            CaptureRaisedBookScreenshot();

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Success", 
                    "3D Book Viewmodel Rig successfully built and attached to Main Camera!\n\n" +
                    "• 3D Open Ledger Mesh with curved pages\n" +
                    "• 2D Pixel-Art Billboard Hands (Left & Right)\n" +
                    "• Dynamic guest records & portrait\n" +
                    "• Diegetic Candle Inspection Light\n\n" +
                    "Testing in Play Mode:\n" +
                    "• Look down past 45° to raise the book.\n" +
                    "• Press [TAB] key anytime to toggle the book on/off.\n" +
                    "• Use Mouse Scroll Wheel or A/D to cycle guest records.", "Awesome!");
            }
        }

        private static TMP_Text Create3DText(Transform parent, string name, string text, Vector3 localPos, float fontSize, FontStyles style, TextAlignmentOptions alignment, float wrapWidth = 0f)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = Quaternion.identity;

            var tmp = obj.AddComponent<TextMeshPro>();
            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (fontAsset != null) tmp.font = fontAsset;

            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = new Color(0.08f, 0.05f, 0.03f, 1f); // 1600s deep iron gall ink
            tmp.sortingOrder = 15;

            var mr = obj.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 15;

            var rt = obj.GetComponent<RectTransform>();
            if (wrapWidth > 0f)
            {
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(wrapWidth, 0.22f);
                tmp.textWrappingMode = TextWrappingModes.Normal;
            }
            else
            {
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(0.22f, 0.05f);
            }

            return tmp;
        }

        private static GameObject Create2DSprite(Transform parent, string name, Vector3 localPos, Vector3 localScale, string spritePath, int sortingOrder)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPos;
            obj.transform.localRotation = Quaternion.identity;
            obj.transform.localScale = localScale;

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            sr.sortingOrder = sortingOrder;

            return obj;
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static void EnsureLayerExists(string layerName)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;

            SerializedObject tagManager = new SerializedObject(assets[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                if (sp.stringValue == layerName) return;
            }

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[HanBookSetup] Registered layer '{layerName}' at index {i}");
                    return;
                }
            }
        }

        [MenuItem("Han/Capture Raised Book Screenshot")]
        public static void CaptureRaisedBookScreenshot()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) mainCam = Object.FindFirstObjectByType<Camera>();
            if (mainCam == null)
            {
                Debug.LogError("[HanScreenshot] No active Camera found in scene!");
                return;
            }

            Transform vm = mainCam.transform.Find("Han_3DBook_Viewmodel");
            if (vm == null)
            {
                Debug.LogError($"[HanScreenshot] Han_3DBook_Viewmodel not found under {mainCam.name}!");
                return;
            }

            Camera overlayCam = mainCam.transform.Find("BookOverlayCamera")?.GetComponent<Camera>();

            // Temporarily set to raised position
            Vector3 prevPos = vm.localPosition;
            Quaternion prevRot = vm.localRotation;

            vm.localPosition = new Vector3(0f, -0.015f, 0.48f);
            vm.localRotation = Quaternion.Euler(12f, 0f, 0f);

            // Turn on light and ensure data is populated
            Light light = vm.GetComponentInChildren<Light>();
            if (light != null) light.enabled = true;

            var guestBookUI = vm.GetComponent<GuestBookUI>();
            if (guestBookUI != null)
            {
                guestBookUI.DisplayGuest(0);
            }

            // Render camera stack
            string dir = "Assets/Screenshots";
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);

            int width = 1280;
            int height = 720;
            RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevRt = RenderTexture.active;
            RenderTexture prevCamRt = mainCam.targetTexture;

            // 1. Render Base World Camera
            mainCam.targetTexture = rt;
            mainCam.Render();

            // 2. Render Overlay Viewmodel Camera (clearing depth on top of world)
            if (overlayCam != null)
            {
                overlayCam.targetTexture = rt;
                overlayCam.clearFlags = CameraClearFlags.Depth;
                overlayCam.Render();
                overlayCam.targetTexture = null;
            }

            mainCam.targetTexture = prevCamRt;
            RenderTexture.active = rt;

            Texture2D screenShot = new Texture2D(width, height, TextureFormat.RGB24, false);
            screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenShot.Apply();

            RenderTexture.active = prevRt;
            Object.DestroyImmediate(rt);

            // Restore lowered pose
            vm.localPosition = prevPos;
            vm.localRotation = prevRot;

            byte[] bytes = screenShot.EncodeToPNG();
            Object.DestroyImmediate(screenShot);

            string path = System.IO.Path.Combine(dir, "book_raised_preview.png");
            System.IO.File.WriteAllBytes(path, bytes);

            Debug.Log($"[HanScreenshot] 📸 SUCCESS! Captured composited book screenshot to: {path} ({bytes.Length} bytes)");
        }
    }
}
#endif
