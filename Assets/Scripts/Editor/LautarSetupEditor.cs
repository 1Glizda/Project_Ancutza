#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Han.Tavern;

namespace Han.EditorTools
{
    public static class LautarSetupEditor
    {
        [MenuItem("Tools/Han Tavern/Setup Lautar on Carpet", false, 20)]
        public static void SetupLautarMenuItem()
        {
            var carpet = GameObject.Find("carpet_lautar");
            if (carpet == null)
            {
                EditorUtility.DisplayDialog("Error", "GameObject 'carpet_lautar' not found in active scene!", "OK");
                return;
            }

            var lautar = carpet.GetComponent<LautarCharacterSystem>();
            if (lautar == null)
            {
                lautar = Undo.AddComponent<LautarCharacterSystem>(carpet);
            }

            lautar.AutoConfigureHierarchyAndAssets();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(carpet.scene);
            Selection.activeGameObject = carpet;

            EditorUtility.DisplayDialog(
                "Lăutar Character Configured",
                "Successfully configured Lăutar Character on 'carpet_lautar':\n\n" +
                "- Body Sprite: Florin.png (billboarded to face the Player)\n" +
                "- Mouth: 4 Singing Mouth Sprites (Mouth_0 to Mouth_3)\n" +
                "- Audio: 2 Song Tracks assigned\n" +
                "- 3D AudioSource + Hall Reverb Filter (Concerthall acoustics)\n" +
                "- Singing Movement: Procedural left-right tilt/sway & rhythmic bobbing\n" +
                "- Night Persistence: Visits every 1-3 nights (hook: OnNewNight)\n" +
                "- Debug Toggle: 'L' Key in Play Mode\n\n" +
                "You can fine-tune sway angle, frequency, and reverb preset directly in the Inspector.",
                "OK"
            );
        }
    }
}
#endif
