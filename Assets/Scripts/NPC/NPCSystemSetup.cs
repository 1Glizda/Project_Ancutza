using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.AI;
#endif

/// <summary>
/// Editor-only utility to set up the NPC system in the scene.
/// Adds SmartObject components to furniture, bakes NavMesh, and creates NPC prefab template.
/// </summary>
public class NPCSystemSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/NPC System/Setup Scene Furniture")]
    public static void SetupFurniture()
    {
        int configured = 0;

        // Find all bench objects
        var allTransforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in allTransforms)
        {
            string nameLower = t.name.ToLower();

            // Skip if already has SmartObject
            if (t.GetComponent<SmartObject>() != null) continue;

            if (nameLower == "bench" || nameLower.StartsWith("bench.") || nameLower.StartsWith("bench ("))
            {
                var so = Undo.AddComponent<SmartObject>(t.gameObject);
                var serialized = new SerializedObject(so);
                serialized.FindProperty("smartObjectType").enumValueIndex = 1; // Bench
                serialized.ApplyModifiedProperties();
                configured++;
                Debug.Log($"[NPC Setup] Added SmartObject (Bench) to: {t.name}");
            }
            else if (nameLower == "bed" || nameLower.StartsWith("bed.") || nameLower.StartsWith("bed ("))
            {
                // Skip bedside_table etc
                if (nameLower.Contains("side") || nameLower.Contains("table")) continue;

                var so = Undo.AddComponent<SmartObject>(t.gameObject);
                var serialized = new SerializedObject(so);
                serialized.FindProperty("smartObjectType").enumValueIndex = 2; // Bed
                serialized.ApplyModifiedProperties();
                configured++;
                Debug.Log($"[NPC Setup] Added SmartObject (Bed) to: {t.name}");
            }
            else if (nameLower == "chair" || nameLower.StartsWith("chair.") || nameLower.StartsWith("chair ("))
            {
                var so = Undo.AddComponent<SmartObject>(t.gameObject);
                var serialized = new SerializedObject(so);
                serialized.FindProperty("smartObjectType").enumValueIndex = 0; // Chair
                serialized.ApplyModifiedProperties();
                configured++;
                Debug.Log($"[NPC Setup] Added SmartObject (Chair) to: {t.name}");
            }
        }

        Debug.Log($"[NPC Setup] Configured {configured} furniture objects with SmartObject components.");
    }

    [MenuItem("Tools/NPC System/Create NPC Template")]
    public static void CreateNPCTemplate()
    {
        // Create a simple NPC GameObject
        var npc = new GameObject("NPC_Template");
        npc.transform.position = new Vector3(-20f, 4.2f, 90f);

        // Add NavMeshAgent
        var agent = npc.AddComponent<NavMeshAgent>();
        agent.speed = 2f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.3f;
        agent.radius = 0.3f;
        agent.height = 1.8f;
        agent.baseOffset = 0f;

        // Create sprite child
        var spriteObj = new GameObject("Sprite");
        spriteObj.transform.SetParent(npc.transform);
        spriteObj.transform.localPosition = new Vector3(0f, 0.9f, 0f);

        var sr = spriteObj.AddComponent<SpriteRenderer>();
        sr.color = Color.white;

        // Add billboard to sprite child
        System.Type bbType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            bbType = asm.GetType("BillboardSprite");
            if (bbType != null) break;
        }
        if (bbType != null)
            spriteObj.AddComponent(bbType);

        // Add NPCController to root
        System.Type npcType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            npcType = asm.GetType("NPCController");
            if (npcType != null) break;
        }
        if (npcType != null)
            npc.AddComponent(npcType);

        // Add SpeechBubble to root
        System.Type sbType = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            sbType = asm.GetType("SpeechBubble");
            if (sbType != null) break;
        }
        if (sbType != null)
            npc.AddComponent(sbType);

        Undo.RegisterCreatedObjectUndo(npc, "Create NPC Template");
        Selection.activeGameObject = npc;

        Debug.Log("[NPC Setup] Created NPC_Template. Assign a sprite and DialogueData asset, then duplicate as needed.");
    }
#endif
}
