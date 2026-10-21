using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Han.CheckIn
{
    /// <summary>
    /// Constructs and configures the complete 3D Book Viewmodel with 2D Billboard Hands,
    /// 3D Open Ledger Mesh, World-Space Canvas Pages, and Diegetic Inspection Light.
    /// Can be executed from the context menu or at runtime.
    /// </summary>
    public class HanBookViewmodelBuilder : MonoBehaviour
    {
        [ContextMenu("Build 3D Book Viewmodel Rig")]
        public void BuildRig()
        {
            GameObject root = this.gameObject;
            root.name = "Han_3DBook_Viewmodel";

            var controller = root.GetComponent<CheckInBookViewController>();
            if (controller == null) controller = root.AddComponent<CheckInBookViewController>();

            var audioSource = root.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = root.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;

            // 2. 3D Book Mesh Child
            Transform meshChild = root.transform.Find("Book3D_Mesh");
            if (meshChild == null)
            {
                var go = new GameObject("Book3D_Mesh");
                go.transform.SetParent(root.transform, false);
                meshChild = go.transform;
            }

            var meshGen = meshChild.GetComponent<OpenBookMeshGenerator>();
            if (meshGen == null) meshGen = meshChild.gameObject.AddComponent<OpenBookMeshGenerator>();
            meshGen.BuildMesh();

            var meshRenderer = meshChild.GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.sharedMaterial == null)
            {
                var urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (urpShader != null)
                {
                    Material bookMat = new Material(urpShader);
                    bookMat.name = "M_BookCover_Leather";
                    bookMat.color = new Color(0.24f, 0.16f, 0.10f, 1f);
                    meshRenderer.sharedMaterial = bookMat;
                }
            }

            // 3. Inspection Point Light
            Transform lightChild = root.transform.Find("InspectionLight");
            if (lightChild == null)
            {
                var go = new GameObject("InspectionLight");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(0f, 0.18f, 0.05f);
                lightChild = go.transform;
            }
            var lightComp = lightChild.GetComponent<Light>();
            if (lightComp == null) lightComp = lightChild.gameObject.AddComponent<Light>();
            lightComp.type = LightType.Point;
            lightComp.color = new Color(1.0f, 0.65f, 0.32f, 1f);
            lightComp.range = 1.2f;
            lightComp.intensity = 0.95f;

            // 4. World Space Canvas for Pages
            GameObject canvasGo = null;
            Transform existingCanvas = root.transform.Find("Pages_WorldCanvas");
            if (existingCanvas != null)
            {
                canvasGo = existingCanvas.gameObject;
            }
            else
            {
                canvasGo = new GameObject("Pages_WorldCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasGo.transform.SetParent(root.transform, false);
            }

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rectTransform = canvasGo.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(1000f, 700f);
            rectTransform.localScale = new Vector3(0.00052f, 0.00052f, 0.00052f);
            rectTransform.localPosition = new Vector3(0f, 0.008f, 0f);
            rectTransform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var guestBookUI = canvasGo.GetComponent<GuestBookUI>();
            if (guestBookUI == null) guestBookUI = canvasGo.AddComponent<GuestBookUI>();

            // 5. 2D Billboard Hands (Left & Right)
            Build2DHand(root.transform, "Hand_Left_2D", new Vector3(-0.27f, 0.04f, -0.05f), new Vector3(20f, 0f, 10f), "Assets/Sprites/Hand_Left.png");
            Build2DHand(root.transform, "Hand_Right_2D", new Vector3(0.27f, 0.04f, -0.05f), new Vector3(20f, 0f, -10f), "Assets/Sprites/Hand_Right.png");

            Debug.Log("[HanBookViewmodelBuilder] 3D Book Viewmodel successfully built with 2D Billboard Hands!");
        }

        private void Build2DHand(Transform parent, string handName, Vector3 localPos, Vector3 localRot, string spritePath)
        {
            Transform handChild = parent.Find(handName);
            if (handChild == null)
            {
                var go = new GameObject(handName);
                go.transform.SetParent(parent, false);
                handChild = go.transform;
            }

            handChild.localPosition = localPos;
            handChild.localRotation = Quaternion.Euler(localRot);
            handChild.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            var spriteRenderer = handChild.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = handChild.gameObject.AddComponent<SpriteRenderer>();

#if UNITY_EDITOR
            Sprite handSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (handSprite != null)
                spriteRenderer.sprite = handSprite;
#endif
            spriteRenderer.sortingOrder = 10;
        }
    }
}
