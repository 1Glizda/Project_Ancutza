using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Han.CheckIn
{
    /// <summary>
    /// Runtime and startup performance optimizer designed for uncompressed WebGL builds
    /// running on low-RAM (2GB-4GB) and integrated/software GPU ("almost no GPU") systems.
    /// Includes:
    /// - Geometric raycast Light Occlusion & strict active-light budgeting in Low-End Mode
    ///   (prevents unshadowed point lights from bleeding through walls/floors/closed doors while disabling occluded lights for max FPS).
    /// - Automatic Sound Occlusion wiring for the Fire Pit and torches across ALL quality levels.
    /// - Toggleable at any time via the Settings Menu checkbox or [F8] hotkey.
    /// </summary>
    [DisallowMultipleComponent]
    public class HanLowEndOptimizer : MonoBehaviour
    {
        public const string PREF_KEY = "Han_LowEndPCMode";

        public static HanLowEndOptimizer Instance { get; private set; }
        public static bool IsLowEndModeEnabled { get; private set; }

        public static event Action<bool> OnLowEndModeChanged;

        [Header("Low-End Mode Settings")]
        [Tooltip("Hotkey to quickly toggle Low-End PC Mode at any time during gameplay or menus.")]
        [SerializeField] private KeyCode toggleHotkey = KeyCode.F8;

        [Tooltip("Internal URP render scale when Low-End Mode is ON (0.55 = ~70% fewer pixels shaded, while UI stays native resolution).")]
        [SerializeField, Range(0.35f, 0.85f)] private float lowEndRenderScale = 0.55f;

        [Tooltip("Camera far clip plane distance in meters when Low-End Mode is ON.")]
        [SerializeField, Range(25f, 120f)] private float lowEndFarClipPlane = 45f;

        [Header("Room-Bound Light Occlusion & Performance Budget")]
        [Tooltip("Maximum number of unoccluded realtime point/spot lights allowed simultaneously in Default/Standard Mode (matches URP AdditionalLightsPerObjectLimit).")]
        [SerializeField, Range(4, 12)] private int maxSimultaneousStandardLights = 8;

        [Tooltip("Maximum distance (meters) beyond which lights are culled in Default/Standard Mode.")]
        [SerializeField, Range(15f, 45f)] private float maxStandardLightDistance = 30f;

        [Tooltip("Maximum number of unoccluded realtime point/spot lights allowed to stay enabled simultaneously in Low-End Mode.")]
        [SerializeField, Range(2, 8)] private int maxSimultaneousLowEndLights = 4;

        [Tooltip("Maximum distance (meters) beyond which lights are culled in Low-End Mode.")]
        [SerializeField, Range(10f, 35f)] private float maxLightActiveDistance = 22f;

        [Tooltip("Number of lights raycast-tested per frame in round-robin order to keep CPU overhead near zero.")]
        [SerializeField, Range(2, 16)] private int lightsCheckedPerFrame = 8;

        [Tooltip("Speed at which lights fade in/out when occluded or revealed by doors/walls.")]
        [SerializeField, Range(2f, 12f)] private float lightOcclusionFadeSpeed = 7.5f;

        private static readonly string[] StructuralOccluderKeywords =
        {
            "wall", "window", "door", "barn wood", "floor", "platform", "roof", "ceiling", "_movable", "wholebarndoor"
        };

        private static readonly string[] IgnoredPropKeywords =
        {
            "table", "chair", "barrel", "crate", "lamp", "chandelier", "axe", "candle", "firewood",
            "fireplace", "torch", "fire", "props", "npc", "guest", "lautar", "player", "carpet",
            "canvas_on_wall", "bed", "chest"
        };

        private struct LightState
        {
            public Light light;
            public Transform transform;
            public bool wasEnabled;
            public LightShadows shadows;
            public float range;
            public float containedStandardRange;
            public float containedLowRange;
            public float maxCullDistance;
            public float maxStandardCullDistance;
            public float intensity;
            public float currentVisibility;
            public float rawTargetVisibility;
            public float budgetedTargetVisibility;
            public float lastDistance;
        }

        private struct CameraState
        {
            public Camera camera;
            public float farClipPlane;
            public bool renderPostProcessing;
            public bool renderShadows;
            public AntialiasingMode antialiasing;
        }

        private struct ParticleState
        {
            public ParticleSystem ps;
            public ParticleSystemRenderer renderer;
            public bool wasPlaying;
            public bool rendererEnabled;
        }

        private readonly List<LightState> cachedLights = new List<LightState>(32);
        private readonly List<CameraState> cachedCameras = new List<CameraState>(4);
        private readonly List<ParticleState> cachedParticles = new List<ParticleState>(32);
        private readonly List<AudioOcclusion> cachedAudioOcclusions = new List<AudioOcclusion>(8);
        private readonly List<AudioReverbFilter> cachedReverbFilters = new List<AudioReverbFilter>(8);

        private readonly RaycastHit[] lightRayHits = new RaycastHit[16];
        private readonly Dictionary<Collider, bool> structuralColliderCache = new Dictionary<Collider, bool>(128);
        private readonly Dictionary<Collider, bool> floorColliderCache = new Dictionary<Collider, bool>(128);
        private int lightRaycastCursor = 0;

        private bool sceneStatesCached;
        private float baselineRenderScale = 0.85f;
        private float baselineShadowDistance = 22f;
        private bool baselineSupportsHDR = false;

        private bool baselineFog;
        private FogMode baselineFogMode;
        private Color baselineFogColor;
        private float baselineFogStart;
        private float baselineFogEnd;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitializeOnLoad()
        {
            // Clamp WebGL/Low-RAM thread & async upload budgets early before heavy scene deserialization
            Application.backgroundLoadingPriority = ThreadPriority.Low;
            QualitySettings.asyncUploadTimeSlice = 2;
            QualitySettings.asyncUploadBufferSize = 8;
            QualitySettings.asyncUploadPersistentBuffer = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeAfterSceneLoad()
        {
            EnsureInstance();
        }

        public static HanLowEndOptimizer EnsureInstance()
        {
            if (Instance != null) return Instance;

            Instance = FindFirstObjectByType<HanLowEndOptimizer>(FindObjectsInactive.Include);
            if (Instance == null)
            {
                GameObject go = new GameObject("HanLowEndOptimizer");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<HanLowEndOptimizer>();
            }
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CacheBaselinePipelineState();

            bool initialMode;
            if (PlayerPrefs.HasKey(PREF_KEY))
            {
                initialMode = PlayerPrefs.GetInt(PREF_KEY, 0) == 1;
            }
            else
            {
                initialMode = DetectLowEndHardware();
                if (initialMode)
                {
                    PlayerPrefs.SetInt(PREF_KEY, 1);
                    PlayerPrefs.Save();
                }
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            SetLowEndMode(initialMode, savePreference: false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            sceneStatesCached = false;
            structuralColliderCache.Clear();
            floorColliderCache.Clear();
            ApplyOptimizations(IsLowEndModeEnabled);
        }

        private void Update()
        {
            if (WasHotkeyPressed())
            {
                ToggleLowEndMode();
                HanUIAudioManager.PlayClick();
            }

            // Keep room-bound light occlusion active on ALL settings levels so lights never bleed through walls
            // and never overflow URP's per-object / shadow-atlas limits.
            UpdateLightOcclusion();
        }

        private bool WasHotkeyPressed()
        {
            if (toggleHotkey == KeyCode.None) return false;

            if (Keyboard.current != null && toggleHotkey == KeyCode.F8 && Keyboard.current.f8Key.wasPressedThisFrame)
            {
                return true;
            }

            try
            {
                if (Input.GetKeyDown(toggleHotkey)) return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Heuristic detection for low-RAM or software/integrated GPU systems on first run.
        /// </summary>
        public static bool DetectLowEndHardware()
        {
            int sysRamMB = SystemInfo.systemMemorySize;
            int vramMB = SystemInfo.graphicsMemorySize;
            string gpuName = (SystemInfo.graphicsDeviceName ?? string.Empty).ToLowerInvariant();

            // Software renderers or virtualized/unaccelerated WebGL contexts ("almost no GPU")
            if (gpuName.Contains("swiftshader") ||
                gpuName.Contains("llvmpipe") ||
                gpuName.Contains("software") ||
                gpuName.Contains("basic render") ||
                gpuName.Contains("gdi generic") ||
                gpuName.Contains("chromium"))
            {
                Debug.Log($"[HanLowEndOptimizer] Software/unaccelerated GPU detected ('{SystemInfo.graphicsDeviceName}'). Enabling Low-End PC Mode.");
                return true;
            }

            // Very low system RAM (<= 4 GB) or very low reported VRAM (<= 512 MB)
            if ((sysRamMB > 0 && sysRamMB <= 4096) || (vramMB > 0 && vramMB <= 512))
            {
                Debug.Log($"[HanLowEndOptimizer] Low memory environment detected (RAM: {sysRamMB} MB, VRAM: {vramMB} MB). Enabling Low-End PC Mode.");
                return true;
            }

            return false;
        }

        public static void ToggleLowEndMode()
        {
            SetLowEndMode(!IsLowEndModeEnabled, savePreference: true);
        }

        public static void SetLowEndMode(bool enabled, bool savePreference = true)
        {
            EnsureInstance();
            IsLowEndModeEnabled = enabled;

            if (savePreference)
            {
                PlayerPrefs.SetInt(PREF_KEY, enabled ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (Instance != null)
            {
                Instance.ApplyOptimizations(enabled);
            }

            OnLowEndModeChanged?.Invoke(enabled);
            Debug.Log($"<color=#E6C17A>[HanLowEndOptimizer]</color> Low-End PC Mode {(enabled ? "ENABLED (Max FPS / Low RAM / Light Occlusion Active)" : "DISABLED (Standard Quality)")}.");
        }

        private void CacheBaselinePipelineState()
        {
            UniversalRenderPipelineAsset urp = GetActiveURPAsset();
            if (urp != null)
            {
                baselineRenderScale = Mathf.Clamp(urp.renderScale, 0.5f, 1.0f);
                baselineShadowDistance = Mathf.Max(15f, urp.shadowDistance);
                baselineSupportsHDR = urp.supportsHDR;
            }

            baselineFog = RenderSettings.fog;
            baselineFogMode = RenderSettings.fogMode;
            baselineFogColor = RenderSettings.fogColor;
            baselineFogStart = RenderSettings.fogStartDistance;
            baselineFogEnd = RenderSettings.fogEndDistance;
        }

        private void EnsureSceneObjectsCached()
        {
            if (sceneStatesCached) return;
            sceneStatesCached = true;

            cachedLights.Clear();
            cachedCameras.Clear();
            cachedParticles.Clear();
            cachedAudioOcclusions.Clear();
            cachedReverbFilters.Clear();

            // 1. Ensure Fire Pit & Torch AudioSources always have AudioOcclusion attached on ALL settings levels
            EnsureFirePitSoundOcclusion();

            // 2. Cache all scene point/spot lights (excluding player's diegetic InspectionLight and Directional lights)
            Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                if (l == null) continue;
                if (l.type == LightType.Directional) continue;
                if (l.name.IndexOf("InspectionLight", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                // Contain oversized ranges so lights stay bound within their rooms on both Standard and Low-End settings
                float origRange = l.range;
                float containedStandard = origRange > 14f
                    ? Mathf.Clamp(origRange * 0.72f, 12f, 16.5f)
                    : origRange;
                float containedRange = origRange > 12f
                    ? Mathf.Clamp(origRange * 0.58f, 9.5f, 13.5f)
                    : Mathf.Max(2.2f, origRange * 0.85f);

                float maxCull = origRange <= 6f
                    ? Mathf.Min(maxLightActiveDistance, 13.5f)
                    : maxLightActiveDistance;
                float maxStdCull = origRange <= 6f
                    ? Mathf.Min(maxStandardLightDistance, 18f)
                    : maxStandardLightDistance;

                cachedLights.Add(new LightState
                {
                    light = l,
                    transform = l.transform,
                    wasEnabled = l.enabled,
                    shadows = l.shadows,
                    range = origRange,
                    containedStandardRange = containedStandard,
                    containedLowRange = containedRange,
                    maxCullDistance = maxCull,
                    maxStandardCullDistance = maxStdCull,
                    intensity = l.intensity,
                    currentVisibility = 1f,
                    rawTargetVisibility = 1f,
                    budgetedTargetVisibility = 1f,
                    lastDistance = 0f
                });
            }

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera cam = cameras[i];
                if (cam == null) continue;
                var addData = cam.GetComponent<UniversalAdditionalCameraData>();
                cachedCameras.Add(new CameraState
                {
                    camera = cam,
                    farClipPlane = cam.farClipPlane,
                    renderPostProcessing = addData != null && addData.renderPostProcessing,
                    renderShadows = addData == null || addData.renderShadows,
                    antialiasing = addData != null ? addData.antialiasing : AntialiasingMode.None
                });
            }

            ParticleSystem[] particles = FindObjectsByType<ParticleSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem ps = particles[i];
                if (ps == null) continue;
                var psr = ps.GetComponent<ParticleSystemRenderer>();
                cachedParticles.Add(new ParticleState
                {
                    ps = ps,
                    renderer = psr,
                    wasPlaying = ps.isPlaying,
                    rendererEnabled = psr != null && psr.enabled
                });
            }

            AudioOcclusion[] occlusions = FindObjectsByType<AudioOcclusion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < occlusions.Length; i++)
            {
                if (occlusions[i] != null) cachedAudioOcclusions.Add(occlusions[i]);
            }

            AudioReverbFilter[] reverbs = FindObjectsByType<AudioReverbFilter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < reverbs.Length; i++)
            {
                if (reverbs[i] != null) cachedReverbFilters.Add(reverbs[i]);
            }
        }

        /// <summary>
        /// Ensures the Fire Pit (VFX_Fire_01_Big_Smoke / Fireplace_01_stone) and Torch audio sources
        /// have geometric AudioOcclusion enabled on ALL quality levels.
        /// </summary>
        private static void EnsureFirePitSoundOcclusion()
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource src = sources[i];
                if (src == null) continue;

                string objName = src.gameObject.name;
                string clipName = src.clip != null ? src.clip.name : string.Empty;

                bool isFireAudio = objName.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   objName.IndexOf("Fireplace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   objName.IndexOf("Torch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   clipName.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   clipName.IndexOf("Candle", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isFireAudio)
                {
                    AudioOcclusion occ = src.GetComponent<AudioOcclusion>();
                    if (occ == null)
                    {
                        occ = src.gameObject.AddComponent<AudioOcclusion>();
                    }
                    occ.enabled = true;
                }
            }
        }

        private void ApplyOptimizations(bool lowEnd)
        {
            EnsureSceneObjectsCached();

            // 1. URP Render Pipeline Asset Adjustments (Fill-Rate & Shadow Map Elimination)
            UniversalRenderPipelineAsset urp = GetActiveURPAsset();
            if (urp != null)
            {
                urp.renderScale = lowEnd ? lowEndRenderScale : baselineRenderScale;
                urp.shadowDistance = lowEnd ? 0f : baselineShadowDistance;
                urp.supportsHDR = lowEnd ? false : baselineSupportsHDR;
                // Depth texture is required by Vefects Fire/Smoke/Candle DepthFade shaders in Standard Mode;
                // disable in Low-End Mode where particles are off to save the depth copy pass.
                urp.supportsCameraDepthTexture = !lowEnd;
            }

            // 2. QualitySettings (VRAM Mipmap Stripping, LOD Bias, Skin Weights, Anisotropic)
            if (lowEnd)
            {
                QualitySettings.globalTextureMipmapLimit = 2;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                QualitySettings.shadowDistance = 0f;
                QualitySettings.lodBias = 0.45f;
                QualitySettings.maximumLODLevel = 1;
                QualitySettings.particleRaycastBudget = 0;
                QualitySettings.softParticles = false;
                QualitySettings.softVegetation = false;
                QualitySettings.skinWeights = SkinWeights.OneBone;
            }
            else
            {
                QualitySettings.globalTextureMipmapLimit = 1;
                QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                QualitySettings.shadowDistance = baselineShadowDistance;
                QualitySettings.lodBias = 0.75f;
                QualitySettings.maximumLODLevel = 0;
                QualitySettings.particleRaycastBudget = 16;
                QualitySettings.softParticles = false;
                QualitySettings.softVegetation = false;
                QualitySettings.skinWeights = SkinWeights.TwoBones;
            }

            // 3. Camera Post-Processing, Far Clip Plane, Spherical Layer Culling & Atmospheric Fog
            float[] cullDistances = new float[32];
            if (lowEnd)
            {
                for (int i = 0; i < 32; i++) cullDistances[i] = lowEndFarClipPlane;
            }

            for (int i = 0; i < cachedCameras.Count; i++)
            {
                CameraState cs = cachedCameras[i];
                if (cs.camera == null) continue;

                bool isMainOrWorldCam = cs.camera.CompareTag("MainCamera") || cs.farClipPlane > 50f;
                if (isMainOrWorldCam)
                {
                    cs.camera.farClipPlane = lowEnd ? lowEndFarClipPlane : cs.farClipPlane;
                    cs.camera.layerCullDistances = lowEnd ? cullDistances : new float[32];
                    cs.camera.layerCullSpherical = lowEnd;
                }

                var addData = cs.camera.GetComponent<UniversalAdditionalCameraData>();
                if (addData != null)
                {
                    addData.renderPostProcessing = lowEnd ? false : cs.renderPostProcessing;
                    addData.renderShadows = lowEnd ? false : cs.renderShadows;
                    addData.antialiasing = lowEnd ? AntialiasingMode.None : cs.antialiasing;
                }
            }

            if (lowEnd)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(0.05f, 0.04f, 0.04f, 1f);
                RenderSettings.fogStartDistance = lowEndFarClipPlane * 0.52f;
                RenderSettings.fogEndDistance = lowEndFarClipPlane * 0.96f;
            }
            else
            {
                RenderSettings.fog = baselineFog;
                RenderSettings.fogMode = baselineFogMode;
                RenderSettings.fogColor = baselineFogColor;
                RenderSettings.fogStartDistance = baselineFogStart;
                RenderSettings.fogEndDistance = baselineFogEnd;
            }

            // 4. Realtime Lights: Configure shadows & room-contained ranges, then run immediate occlusion snap
            for (int i = 0; i < cachedLights.Count; i++)
            {
                LightState ls = cachedLights[i];
                if (ls.light == null) continue;

                if (lowEnd)
                {
                    ls.light.shadows = LightShadows.None;
                    ls.light.range = ls.containedLowRange;
                }
                else
                {
                    ls.light.enabled = ls.wasEnabled;
                    ls.light.shadows = ls.shadows;
                    ls.light.range = ls.containedStandardRange;
                    ls.light.intensity = ls.intensity;
                }
                cachedLights[i] = ls;
            }

            // Perform an immediate full-pass light occlusion check so the initial frame has zero wall bleed
            EvaluateLightOcclusionBatch(cachedLights.Count, immediateSnap: true);

            // 5. Particle Systems (Eliminate Transparent Fire/Candle Alpha Overdraw in Low Mode)
            bool hasOpaqueTex = urp != null && urp.supportsCameraOpaqueTexture;
            for (int i = 0; i < cachedParticles.Count; i++)
            {
                ParticleState psState = cachedParticles[i];
                if (psState.ps == null) continue;

                if (lowEnd)
                {
                    if (psState.ps.isPlaying)
                        psState.ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (psState.renderer != null)
                        psState.renderer.enabled = false;
                }
                else
                {
                    bool isHeatHazeWithoutOpaque = !hasOpaqueTex &&
                        psState.renderer != null &&
                        psState.renderer.sharedMaterial != null &&
                        psState.renderer.sharedMaterial.shader != null &&
                        psState.renderer.sharedMaterial.shader.name.IndexOf("Heat_Haze", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (psState.renderer != null)
                        psState.renderer.enabled = psState.rendererEnabled && !isHeatHazeWithoutOpaque;
                    if (psState.wasPlaying && !psState.ps.isPlaying && !isHeatHazeWithoutOpaque)
                        psState.ps.Play(true);
                }
            }

            // 6. Keep AudioOcclusion ACTIVE on all quality levels (including Fire Pit & Lăutar)
            for (int i = 0; i < cachedAudioOcclusions.Count; i++)
            {
                if (cachedAudioOcclusions[i] != null)
                    cachedAudioOcclusions[i].enabled = true;
            }

            // Disable heavy convolution/reverb DSP only in Low-End Mode
            for (int i = 0; i < cachedReverbFilters.Count; i++)
            {
                if (cachedReverbFilters[i] != null)
                    cachedReverbFilters[i].enabled = !lowEnd;
            }

            // 7. Reclaim Wasm Heap / GPU Memory immediately when enabling Low-End Mode
            if (lowEnd)
            {
                Resources.UnloadUnusedAssets();
                GC.Collect();
            }
        }

        // ------------------------------------------------------------------
        //  Room-Bound Geometric Light Occlusion & Budget Culling (All Modes)
        // ------------------------------------------------------------------
        private void UpdateLightOcclusion()
        {
            if (cachedLights.Count == 0) return;

            bool lowEnd = IsLowEndModeEnabled;

            // 1. Time-sliced raycast check for a small subset of lights this frame
            int batchCount = Mathf.Min(lightsCheckedPerFrame, cachedLights.Count);
            EvaluateLightOcclusionBatch(batchCount, immediateSnap: false);

            // 2. Smoothly fade all lights toward their budgeted visibility and disable occluded lights
            float step = Time.unscaledDeltaTime * lightOcclusionFadeSpeed;
            for (int i = 0; i < cachedLights.Count; i++)
            {
                LightState ls = cachedLights[i];
                if (ls.light == null || !ls.light.gameObject.activeInHierarchy || !ls.wasEnabled) continue;

                ls.currentVisibility = Mathf.MoveTowards(ls.currentVisibility, ls.budgetedTargetVisibility, step);

                if (ls.currentVisibility <= 0.005f)
                {
                    if (ls.light.enabled) ls.light.enabled = false;
                }
                else
                {
                    if (!ls.light.enabled) ls.light.enabled = true;
                    float targetRange = lowEnd ? ls.containedLowRange : ls.containedStandardRange;
                    ls.light.intensity = ls.intensity * ls.currentVisibility;
                    ls.light.range = targetRange * Mathf.Lerp(0.45f, 1.0f, ls.currentVisibility);
                }

                cachedLights[i] = ls;
            }
        }

        private void EvaluateLightOcclusionBatch(int countToProcess, bool immediateSnap)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            bool lowEnd = IsLowEndModeEnabled;
            Vector3 camPos = cam.transform.position;
            Vector3 camForward = cam.transform.forward;

            // Probe point 1.4m ahead of the camera (if unobstructed) so lights inside open doorways fade in smoothly as you approach
            Vector3 portalProbePos = camPos;
            if (CountStructuralOccluders(camPos, camPos + camForward * 1.4f, out _) == 0)
            {
                portalProbePos = camPos + camForward * 1.4f;
            }

            int total = cachedLights.Count;
            if (total == 0) return;

            for (int n = 0; n < countToProcess; n++)
            {
                int idx = (lightRaycastCursor + n) % total;
                LightState ls = cachedLights[idx];
                if (ls.light == null || !ls.light.gameObject.activeInHierarchy || !ls.wasEnabled)
                {
                    ls.rawTargetVisibility = 0f;
                    ls.budgetedTargetVisibility = 0f;
                    cachedLights[idx] = ls;
                    continue;
                }

                Vector3 lightPos = ls.transform.position;
                Vector3 toLight = lightPos - camPos;
                float dist = toLight.magnitude;
                ls.lastDistance = dist;

                float maxDist = lowEnd ? ls.maxCullDistance : ls.maxStandardCullDistance;
                float activeRange = lowEnd ? ls.containedLowRange : ls.containedStandardRange;

                if (dist > maxDist)
                {
                    ls.rawTargetVisibility = 0f;
                }
                else if (dist < 0.6f)
                {
                    ls.rawTargetVisibility = 1f;
                }
                else
                {
                    // View cone check: only cull behind-camera lights if their sphere cannot reach in front of the camera
                    float dot = Vector3.Dot(camForward, toLight / dist);
                    bool behindAndOutOfView = lowEnd
                        ? (dot < -0.45f && dist > activeRange * 0.9f)
                        : (dot < -0.55f && dist > activeRange);

                    if (behindAndOutOfView)
                    {
                        ls.rawTargetVisibility = 0f;
                    }
                    else
                    {
                        int floorHits;
                        int directWalls = CountStructuralOccluders(camPos, lightPos, out floorHits);

                        if (directWalls == 0)
                        {
                            ls.rawTargetVisibility = 1.0f;
                        }
                        else if (directWalls == 1 && floorHits == 0)
                        {
                            // Blocked by 1 wall or closed door on the same story: check if visible from portal probe in front of player
                            int portalFloors;
                            int portalWalls = CountStructuralOccluders(portalProbePos, lightPos, out portalFloors);
                            if (portalWalls == 0 && portalFloors == 0)
                            {
                                ls.rawTargetVisibility = 0.48f;
                            }
                            else
                            {
                                ls.rawTargetVisibility = 0.0f;
                            }
                        }
                        else if (floorHits == 1 && directWalls == 1 && dist < 9.5f)
                        {
                            // Near stairwell between 1st and 2nd floor: check portal probe
                            int portalFloors;
                            int portalWalls = CountStructuralOccluders(portalProbePos + Vector3.up * 0.35f, lightPos, out portalFloors);
                            ls.rawTargetVisibility = (portalWalls == 0 && portalFloors == 0) ? 0.35f : 0.0f;
                        }
                        else
                        {
                            // Blocked by solid floor/ceiling or 2+ walls/doors -> 100% occluded
                            ls.rawTargetVisibility = 0.0f;
                        }
                    }
                }

                cachedLights[idx] = ls;
            }

            lightRaycastCursor = (lightRaycastCursor + countToProcess) % total;

            // Enforce strict active-light performance budget across all lights (top N closest/most visible unoccluded lights)
            EnforceLightPerformanceBudget(immediateSnap);
        }

        private void EnforceLightPerformanceBudget(bool immediateSnap)
        {
            int total = cachedLights.Count;
            if (total == 0) return;

            bool lowEnd = IsLowEndModeEnabled;

            // First, default all budgeted targets to 0
            for (int i = 0; i < total; i++)
            {
                LightState ls = cachedLights[i];
                ls.budgetedTargetVisibility = 0f;
                cachedLights[i] = ls;
            }

            // Pick up to maxSimultaneous lights with highest score (visibility priority, then proximity)
            int budgetLimit = lowEnd ? maxSimultaneousLowEndLights : maxSimultaneousStandardLights;
            int allowed = Mathf.Clamp(budgetLimit, 1, total);
            for (int slot = 0; slot < allowed; slot++)
            {
                int bestIdx = -1;
                float bestScore = -99999f;

                for (int i = 0; i < total; i++)
                {
                    LightState ls = cachedLights[i];
                    if (ls.rawTargetVisibility <= 0.005f) continue;
                    if (ls.budgetedTargetVisibility > 0f) continue; // Already selected

                    // Score favors unoccluded lights closest to the player
                    float score = ls.rawTargetVisibility * 30f - ls.lastDistance;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIdx = i;
                    }
                }

                if (bestIdx < 0) break;

                LightState winner = cachedLights[bestIdx];
                winner.budgetedTargetVisibility = winner.rawTargetVisibility;
                cachedLights[bestIdx] = winner;
            }

            if (immediateSnap)
            {
                for (int i = 0; i < total; i++)
                {
                    LightState ls = cachedLights[i];
                    ls.currentVisibility = ls.budgetedTargetVisibility;
                    if (ls.light != null && ls.wasEnabled)
                    {
                        if (ls.currentVisibility <= 0.005f)
                        {
                            ls.light.enabled = false;
                        }
                        else
                        {
                            ls.light.enabled = true;
                            float targetRange = lowEnd ? ls.containedLowRange : ls.containedStandardRange;
                            ls.light.intensity = ls.intensity * ls.currentVisibility;
                            ls.light.range = targetRange * Mathf.Lerp(0.45f, 1.0f, ls.currentVisibility);
                        }
                    }
                    cachedLights[i] = ls;
                }
            }
        }

        private int CountStructuralOccluders(Vector3 from, Vector3 to, out int floorCount)
        {
            floorCount = 0;
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.05f) return 0;

            int hitCount = Physics.RaycastNonAlloc(from, dir / dist, lightRayHits, dist, ~0, QueryTriggerInteraction.Ignore);
            int walls = 0;

            for (int i = 0; i < hitCount; i++)
            {
                Collider c = lightRayHits[i].collider;
                if (c == null) continue;

                if (!IsStructuralOccluder(c, out bool isFloor)) continue;

                walls++;
                if (isFloor) floorCount++;
            }

            return walls;
        }

        private bool IsStructuralOccluder(Collider c, out bool isFloor)
        {
            if (structuralColliderCache.TryGetValue(c, out bool cachedResult))
            {
                isFloor = floorColliderCache.TryGetValue(c, out bool cachedFloor) && cachedFloor;
                return cachedResult;
            }

            bool matchesStructure = false;
            bool matchesIgnore = false;
            bool detectedFloor = false;

            Transform t = c.transform;
            for (int depth = 0; t != null && depth <= 6; depth++, t = t.parent)
            {
                string n = t.name;
                if (!matchesIgnore && MatchesAnyKeyword(n, IgnoredPropKeywords))
                {
                    matchesIgnore = true;
                }
                if (!matchesStructure && MatchesAnyKeyword(n, StructuralOccluderKeywords))
                {
                    matchesStructure = true;
                }
                if (!detectedFloor && (n.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       n.IndexOf("platform", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                       n.IndexOf("ceiling", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    detectedFloor = true;
                }
            }

            bool finalResult = matchesStructure && !matchesIgnore;
            isFloor = finalResult && detectedFloor;

            structuralColliderCache[c] = finalResult;
            floorColliderCache[c] = isFloor;
            return finalResult;
        }

        private static bool MatchesAnyKeyword(string name, string[] keywords)
        {
            if (string.IsNullOrEmpty(name) || keywords == null) return false;
            for (int i = 0; i < keywords.Length; i++)
            {
                if (!string.IsNullOrEmpty(keywords[i]) &&
                    name.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static UniversalRenderPipelineAsset GetActiveURPAsset()
        {
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset qUrp)
                return qUrp;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset gUrp)
                return gUrp;
            return null;
        }
    }
}
