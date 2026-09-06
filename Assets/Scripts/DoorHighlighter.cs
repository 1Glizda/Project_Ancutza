using System.Collections.Generic;
using UnityEngine;

public class DoorHighlighter : MonoBehaviour
{
    [SerializeField] private Material outlineMaterial;
    private readonly List<GameObject> outlineObjects = new List<GameObject>();
    private bool isHighlighted = false;

    private void Awake()
    {
        EnsureMaterial();
    }

    private void EnsureMaterial()
    {
        if (outlineMaterial == null)
        {
#if UNITY_EDITOR
            outlineMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/DoorBlurOutlineMat.mat");
#endif
            if (outlineMaterial == null)
            {
                Shader shader = Shader.Find("Custom/DoorBlurOutline");
                if (shader != null)
                {
                    outlineMaterial = new Material(shader);
                }
            }
        }
    }

    public void CreateOutlineMeshes()
    {
        EnsureMaterial();
        if (outlineMaterial == null) return;

        // Clean up any existing or dead references
        for (int i = outlineObjects.Count - 1; i >= 0; i--)
        {
            if (outlineObjects[i] != null)
            {
                DestroyImmediate(outlineObjects[i]);
            }
        }
        outlineObjects.Clear();

        MeshRenderer[] renderers = GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer mr in renderers)
        {
            if (mr.name.EndsWith("_BlurOutline")) continue;

            MeshFilter mf = mr.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;

            GameObject outlineChild = new GameObject(mr.name + "_BlurOutline");
            outlineChild.transform.SetParent(mr.transform, false);
            outlineChild.transform.localPosition = Vector3.zero;
            outlineChild.transform.localRotation = Quaternion.identity;
            outlineChild.transform.localScale = Vector3.one;

            MeshFilter childMf = outlineChild.AddComponent<MeshFilter>();
            childMf.sharedMesh = mf.sharedMesh;

            MeshRenderer childMr = outlineChild.AddComponent<MeshRenderer>();
            childMr.sharedMaterial = outlineMaterial;
            childMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            childMr.receiveShadows = false;

            outlineChild.SetActive(false);
            outlineObjects.Add(outlineChild);
        }
    }

    public void SetHighlight(bool active)
    {
        // Respect global toggle
        if (active && !DoorInteractionController.GlobalHighlightEnabled)
        {
            active = false;
        }

        if (isHighlighted == active && outlineObjects.Count > 0) return;
        isHighlighted = active;

        if (outlineObjects.Count == 0 && active)
        {
            CreateOutlineMeshes();
        }

        for (int i = 0; i < outlineObjects.Count; i++)
        {
            if (outlineObjects[i] != null)
            {
                outlineObjects[i].SetActive(active);
            }
        }
    }

    private void OnDisable()
    {
        SetHighlight(false);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < outlineObjects.Count; i++)
        {
            if (outlineObjects[i] != null)
            {
                Destroy(outlineObjects[i]);
            }
        }
        outlineObjects.Clear();
    }
}
