using UnityEditor;
using UnityEngine;

namespace HanGame.Editor
{
    /// <summary>
    /// Ensures WebGL and fullscreen display settings stay synced with the running Unity Editor
    /// so WebGL builds default to 16:9 (1280x720) and scale edge-to-edge across the entire
    /// display when toggling fullscreen.
    /// </summary>
    [InitializeOnLoad]
    public static class HanWebGLBuildConfigurator
    {
        private const string PrefKey = "Han_WebGLFullscreenConfig_v1";
        private const string TemplateId = "PROJECT:HanFullscreen";
        private const int TargetWebWidth = 1280;
        private const int TargetWebHeight = 720;

        static HanWebGLBuildConfigurator()
        {
            if (EditorPrefs.GetBool(PrefKey, false))
                return;

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                ApplyWebGLFullscreenSettings(silent: true);
                EditorPrefs.SetBool(PrefKey, true);
            };
        }

        [MenuItem("Tools/Han/Apply WebGL Fullscreen & Display Settings")]
        public static void ApplySettingsFromMenu()
        {
            ApplyWebGLFullscreenSettings(silent: false);
            EditorPrefs.SetBool(PrefKey, true);
        }

        public static void ApplyWebGLFullscreenSettings(bool silent = false)
        {
            bool changed = false;

            if (PlayerSettings.defaultWebScreenWidth != TargetWebWidth)
            {
                PlayerSettings.defaultWebScreenWidth = TargetWebWidth;
                changed = true;
            }

            if (PlayerSettings.defaultWebScreenHeight != TargetWebHeight)
            {
                PlayerSettings.defaultWebScreenHeight = TargetWebHeight;
                changed = true;
            }

            if (!PlayerSettings.resizableWindow)
            {
                PlayerSettings.resizableWindow = true;
                changed = true;
            }

            if (PlayerSettings.fullScreenMode != FullScreenMode.FullScreenWindow)
            {
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                changed = true;
            }

            if (!PlayerSettings.allowFullscreenSwitch)
            {
                PlayerSettings.allowFullscreenSwitch = true;
                changed = true;
            }

            if (!PlayerSettings.defaultIsNativeResolution)
            {
                PlayerSettings.defaultIsNativeResolution = true;
                changed = true;
            }

            if (!string.Equals(PlayerSettings.WebGL.template, TemplateId, System.StringComparison.Ordinal))
            {
                PlayerSettings.WebGL.template = TemplateId;
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
            }

            if (!silent || changed)
            {
                Debug.Log($"[HanWebGLBuildConfigurator] Applied WebGL/Display settings: {TargetWebWidth}x{TargetWebHeight}, ResizableWindow=true, FullScreenMode=FullScreenWindow, NativeResolution=true, Template={TemplateId}.");
            }
        }
    }
}
