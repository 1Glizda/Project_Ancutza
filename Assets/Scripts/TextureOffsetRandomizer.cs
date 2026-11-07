using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Renderer))]
[ExecuteAlways]
public class TextureOffsetRandomizer : MonoBehaviour
{
    [Tooltip("The shader property name for the main texture map. In URP Lit this is '_BaseMap'. In Built-in Standard shader, this is '_MainTex'.")]
    [SerializeField] private string texturePropertyName = "_BaseMap";

    [Header("Randomization Settings")]
    [SerializeField] private bool randomizeOffset = true;
    [SerializeField] private bool randomizeTiling = false;

    [Header("Tiling (Scale) Range")]
    [SerializeField] private Vector2 tilingScaleX = new Vector2(0.9f, 1.1f);
    [SerializeField] private Vector2 tilingScaleY = new Vector2(0.9f, 1.1f);

    // Keep track of the generated values so they don't change on every editor redraw
    [HideInInspector] [SerializeField] private Vector4 appliedST = new Vector4(1, 1, 0, 0);
    [HideInInspector] [SerializeField] private bool hasBeenRandomized = false;

    private Material baseSharedMaterial;
    private static readonly Dictionary<(Material, Vector4), Material> s_SrpBatcherMaterialCache = new Dictionary<(Material, Vector4), Material>();

    void Start()
    {
        if (!hasBeenRandomized)
        {
            Randomize();
        }
        else
        {
            ApplyProperties();
        }
    }

    private void OnValidate()
    {
        // Keep the material property block in sync when changing properties in the inspector
        ApplyProperties();
    }

    [ContextMenu("Randomize")]
    public void Randomize()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) return;

        if (baseSharedMaterial == null)
        {
            baseSharedMaterial = renderer.sharedMaterial;
        }

        string stPropertyName = texturePropertyName + "_ST";
        
        // Fetch current Scale/Offset values from the material as a base
        Vector4 currentST = baseSharedMaterial.HasProperty(stPropertyName) 
            ? baseSharedMaterial.GetVector(stPropertyName) 
            : new Vector4(1, 1, 0, 0);

        float offsetX = randomizeOffset ? Random.Range(0f, 1f) : currentST.z;
        float offsetY = randomizeOffset ? Random.Range(0f, 1f) : currentST.w;
        
        float scaleX = randomizeTiling ? Random.Range(tilingScaleX.x, tilingScaleX.y) : currentST.x;
        float scaleY = randomizeTiling ? Random.Range(tilingScaleY.x, tilingScaleY.y) : currentST.y;

        appliedST = new Vector4(scaleX, scaleY, offsetX, offsetY);
        hasBeenRandomized = true;

        ApplyProperties();
    }

    public void ApplyProperties()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null) return;

        string stPropertyName = texturePropertyName + "_ST";

        if (Application.isPlaying)
        {
            // Clear MaterialPropertyBlock so URP SRP Batcher stays 100% active across all randomized meshes
            renderer.SetPropertyBlock(null);

            if (baseSharedMaterial == null)
            {
                baseSharedMaterial = renderer.sharedMaterial;
            }

            var key = (baseSharedMaterial, appliedST);
            if (!s_SrpBatcherMaterialCache.TryGetValue(key, out Material variantMat) || variantMat == null)
            {
                variantMat = new Material(baseSharedMaterial)
                {
                    name = $"{baseSharedMaterial.name}_ST_{appliedST.z:F2}_{appliedST.w:F2}",
                    enableInstancing = true
                };
                if (variantMat.HasProperty(stPropertyName))
                {
                    variantMat.SetVector(stPropertyName, appliedST);
                }
                variantMat.mainTextureScale = new Vector2(appliedST.x, appliedST.y);
                variantMat.mainTextureOffset = new Vector2(appliedST.z, appliedST.w);
                s_SrpBatcherMaterialCache[key] = variantMat;
            }

            renderer.sharedMaterial = variantMat;
        }
        else
        {
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(propBlock);
            propBlock.SetVector(stPropertyName, appliedST);
            renderer.SetPropertyBlock(propBlock);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TextureOffsetRandomizer))]
[CanEditMultipleObjects]
public class TextureOffsetRandomizerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(15);
        
        if (GUILayout.Button("Randomize Texture Now", GUILayout.Height(35)))
        {
            foreach (var targetObj in targets)
            {
                var randomizer = (TextureOffsetRandomizer)targetObj;
                
                // Allow undoing this action in the editor
                Undo.RecordObject(randomizer, "Randomize Texture Offset");
                
                randomizer.Randomize();
                
                // Mark the component as dirty so Unity saves the new random offset values
                EditorUtility.SetDirty(randomizer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(randomizer);
            }
        }
    }
}
#endif
