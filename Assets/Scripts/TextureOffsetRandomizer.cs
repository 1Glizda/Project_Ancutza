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

        string stPropertyName = texturePropertyName + "_ST";
        
        // Fetch current Scale/Offset values from the material as a base
        Vector4 currentST = renderer.sharedMaterial.HasProperty(stPropertyName) 
            ? renderer.sharedMaterial.GetVector(stPropertyName) 
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
        if (renderer == null) return;

        MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(propBlock);

        string stPropertyName = texturePropertyName + "_ST";
        propBlock.SetVector(stPropertyName, appliedST);
        renderer.SetPropertyBlock(propBlock);
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
