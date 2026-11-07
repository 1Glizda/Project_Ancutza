using System;
using UnityEngine;
using UnityEngine.Events;

namespace Han.CheckIn
{
    /// <summary>
    /// Centralized UI Audio Manager & Acoustic Synthesizer for tactile bureaucratic desk interactions.
    /// Provides Inspector-assignable AudioClips, UnityEvents, static C# events, and automatic
    /// procedural synthesis fallbacks for:
    /// - UI Hover Blip
    /// - Firm Mechanical Click / Stamp Down
    /// - Archival Paper Rustle
    /// - Modal Menu Pop-In / Fade-Out
    /// - Typewriter Transcript Key Tick
    /// </summary>
    [DisallowMultipleComponent]
    public class HanUIAudioManager : MonoBehaviour
    {
        public static HanUIAudioManager Instance { get; private set; }

        [Header("Audio Source")]
        [SerializeField] private AudioSource uiAudioSource;
        [Range(0f, 1f)]
        [SerializeField] private float masterUIVolume = 0.85f;

        [Header("UI Audio Clips (Optional - Synthesizes Fallbacks if Empty)")]
        [Tooltip("Subtle brass/mechanical blip when hovering or focusing an interactable element.")]
        [SerializeField] private AudioClip hoverBlipClip;

        [Tooltip("Firm mechanical plate click when pressing a menu button.")]
        [SerializeField] private AudioClip firmClickClip;

        [Tooltip("Heavy tactile ink-stamp / ledger latch sound for primary actions and 'Go on' prompt.")]
        [SerializeField] private AudioClip stampDownClip;

        [Tooltip("Warm archival paper rustle when dialogue cards or ledger panels open.")]
        [SerializeField] private AudioClip paperRustleClip;

        [Tooltip("Low atmospheric latch / pop-in sound when opening Pause / Settings modals.")]
        [SerializeField] private AudioClip menuOpenClip;

        [Tooltip("Soft mechanical release sound when closing modals or resuming gameplay.")]
        [SerializeField] private AudioClip menuCloseClip;

        [Tooltip("Subtle vintage ribbon tick for NPC dialogue transcript typing.")]
        [SerializeField] private AudioClip typewriterTickClip;

        [Header("Inspector Audio Event Hooks")]
        public UnityEvent onUIHover;
        public UnityEvent onUIClick;
        public UnityEvent onStampDown;
        public UnityEvent onPaperRustle;
        public UnityEvent onMenuOpen;
        public UnityEvent onMenuClose;

        // Static C# Event Hooks for external audio middleware (FMOD / Wwise / Custom)
        public static event Action OnUIHoverTriggered;
        public static event Action OnUIClickTriggered;
        public static event Action OnStampDownTriggered;
        public static event Action OnPaperRustleTriggered;
        public static event Action OnMenuOpenTriggered;
        public static event Action OnMenuCloseTriggered;

        private float _lastHoverTime = -10f;
        private float _lastTypeTickTime = -10f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }
            Instance = this;
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static HanUIAudioManager EnsureInstance()
        {
            if (Instance != null) return Instance;

            Instance = FindFirstObjectByType<HanUIAudioManager>(FindObjectsInactive.Include);
            if (Instance != null)
            {
                Instance.EnsureInitialized();
                return Instance;
            }

            if (GameLoopManager.Instance != null)
            {
                Instance = GameLoopManager.Instance.gameObject.AddComponent<HanUIAudioManager>();
            }
            else
            {
                GameObject go = new GameObject("HanUIAudioManager");
                Instance = go.AddComponent<HanUIAudioManager>();
            }

            Instance.EnsureInitialized();
            return Instance;
        }

        private void EnsureInitialized()
        {
            if (uiAudioSource == null)
            {
                uiAudioSource = GetComponent<AudioSource>();
                if (uiAudioSource == null)
                    uiAudioSource = gameObject.AddComponent<AudioSource>();
            }

            uiAudioSource.playOnAwake = false;
            uiAudioSource.spatialBlend = 0f;
            uiAudioSource.ignoreListenerPause = true;

            EnsureSynthesizedClips();
        }

        // ------------------------------------------------------------------
        //  Public Static & Instance Audio API
        // ------------------------------------------------------------------
        public static void PlayHover()
        {
            var inst = EnsureInstance();
            if (inst != null) inst.TriggerHover();
        }

        public static void PlayClick()
        {
            var inst = EnsureInstance();
            if (inst != null) inst.TriggerClick();
        }

        public static void PlayStampClick()
        {
            var inst = EnsureInstance();
            if (inst != null) inst.TriggerStampDown();
        }

        public static void PlayPaperRustle()
        {
            var inst = EnsureInstance();
            if (inst != null) inst.TriggerPaperRustle();
        }

        public static void PlayMenuOpen()
        {
            var inst = EnsureInstance();
            if (inst != null) inst.TriggerMenuOpen();
        }

        public static void PlayMenuClose()
        {
            var inst = EnsureInstance();
            if (inst != null) inst.TriggerMenuClose();
        }

        public static void PlayTypewriterTick()
        {
            var inst = EnsureInstance();
            if (inst != null) inst.TriggerTypewriterTick();
        }

        public void TriggerHover()
        {
            if (Time.unscaledTime - _lastHoverTime < 0.035f) return;
            _lastHoverTime = Time.unscaledTime;

            EnsureInitialized();
            if (hoverBlipClip != null && uiAudioSource != null)
            {
                uiAudioSource.pitch = UnityEngine.Random.Range(0.96f, 1.05f);
                uiAudioSource.PlayOneShot(hoverBlipClip, masterUIVolume * 0.42f);
            }

            onUIHover?.Invoke();
            OnUIHoverTriggered?.Invoke();
        }

        public void TriggerClick()
        {
            EnsureInitialized();
            if (firmClickClip != null && uiAudioSource != null)
            {
                uiAudioSource.pitch = UnityEngine.Random.Range(0.95f, 1.04f);
                uiAudioSource.PlayOneShot(firmClickClip, masterUIVolume * 0.80f);
            }

            onUIClick?.Invoke();
            OnUIClickTriggered?.Invoke();
        }

        public void TriggerStampDown()
        {
            EnsureInitialized();
            if (stampDownClip != null && uiAudioSource != null)
            {
                uiAudioSource.pitch = UnityEngine.Random.Range(0.94f, 1.03f);
                uiAudioSource.PlayOneShot(stampDownClip, masterUIVolume * 0.90f);
            }

            onStampDown?.Invoke();
            OnStampDownTriggered?.Invoke();
        }

        public void TriggerPaperRustle()
        {
            EnsureInitialized();
            if (paperRustleClip != null && uiAudioSource != null)
            {
                uiAudioSource.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
                uiAudioSource.PlayOneShot(paperRustleClip, masterUIVolume * 0.55f);
            }

            onPaperRustle?.Invoke();
            OnPaperRustleTriggered?.Invoke();
        }

        public void TriggerMenuOpen()
        {
            EnsureInitialized();
            if (menuOpenClip != null && uiAudioSource != null)
            {
                uiAudioSource.pitch = 1.0f;
                uiAudioSource.PlayOneShot(menuOpenClip, masterUIVolume * 0.72f);
            }

            onMenuOpen?.Invoke();
            OnMenuOpenTriggered?.Invoke();
        }

        public void TriggerMenuClose()
        {
            EnsureInitialized();
            if (menuCloseClip != null && uiAudioSource != null)
            {
                uiAudioSource.pitch = 0.96f;
                uiAudioSource.PlayOneShot(menuCloseClip, masterUIVolume * 0.65f);
            }

            onMenuClose?.Invoke();
            OnMenuCloseTriggered?.Invoke();
        }

        public void TriggerTypewriterTick()
        {
            if (Time.unscaledTime - _lastTypeTickTime < 0.028f) return;
            _lastTypeTickTime = Time.unscaledTime;

            EnsureInitialized();
            if (typewriterTickClip != null && uiAudioSource != null)
            {
                uiAudioSource.pitch = UnityEngine.Random.Range(0.90f, 1.12f);
                uiAudioSource.PlayOneShot(typewriterTickClip, masterUIVolume * 0.30f);
            }
        }

        // ------------------------------------------------------------------
        //  Procedural Vintage Acoustic Synthesis (Fallback Clips)
        // ------------------------------------------------------------------
        private void EnsureSynthesizedClips()
        {
            const int sampleRate = 22050;

            // 1. Subtle mechanical brass hover blip
            if (hoverBlipClip == null)
            {
                const int samples = 680;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float env = Mathf.Exp(-t * 14f);
                    float tone = Mathf.Sin(2f * Mathf.PI * 520f * i / sampleRate) * 0.6f +
                                 Mathf.Sin(2f * Mathf.PI * 1040f * i / sampleRate) * 0.25f;
                    float click = (i < 35) ? ((i % 2 == 0 ? 1f : -1f) * 0.3f) : 0f;
                    data[i] = (tone + click) * env;
                }
                hoverBlipClip = AudioClip.Create("Han_UI_HoverBlip", samples, 1, sampleRate, false);
                hoverBlipClip.SetData(data, 0);
            }

            // 2. Firm mechanical desk button click
            if (firmClickClip == null)
            {
                const int samples = 1400;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float freq = Mathf.Lerp(260f, 85f, t);
                    float env = Mathf.Exp(-t * 9.5f);
                    float body = Mathf.Sin(2f * Mathf.PI * freq * i / sampleRate);
                    float transient = (i < 90) ? Mathf.Sin(2f * Mathf.PI * 1450f * i / sampleRate) * Mathf.Exp(-i * 0.04f) : 0f;
                    data[i] = Mathf.Clamp((body * 0.75f + transient * 0.55f) * env, -1f, 1f);
                }
                firmClickClip = AudioClip.Create("Han_UI_FirmClick", samples, 1, sampleRate, false);
                firmClickClip.SetData(data, 0);
            }

            // 3. Heavy tactile wooden ink-stamp / mechanical latch thud
            if (stampDownClip == null)
            {
                const int samples = 2400;
                float[] data = new float[samples];
                uint rng = 1337u;
                for (int i = 0; i < samples; i++)
                {
                    rng = rng * 1664525u + 1013904223u;
                    float noise = ((rng & 0xFFFF) / 32768f - 1f);
                    float t = (float)i / samples;
                    float freq = Mathf.Lerp(155f, 44f, t);
                    float env = Mathf.Exp(-t * 6.8f);
                    float thud = Mathf.Sin(2f * Mathf.PI * freq * i / sampleRate);
                    float latch = (i > 220 && i < 420) ? Mathf.Sin(2f * Mathf.PI * 820f * i / sampleRate) * 0.35f : 0f;
                    data[i] = Mathf.Clamp((thud * 0.8f + noise * 0.28f + latch) * env, -1f, 1f);
                }
                stampDownClip = AudioClip.Create("Han_UI_StampDown", samples, 1, sampleRate, false);
                stampDownClip.SetData(data, 0);
            }

            // 4. Warm archival paper rustle
            if (paperRustleClip == null)
            {
                const int samples = 3200;
                float[] data = new float[samples];
                uint rng = 98765u;
                float prev = 0f;
                for (int i = 0; i < samples; i++)
                {
                    rng = rng * 1664525u + 1013904223u;
                    float white = ((rng & 0xFFFF) / 32768f - 1f);
                    // Band-shaped filtered noise for crisp parchment slide
                    float filtered = white - prev * 0.72f;
                    prev = white;
                    float t = (float)i / samples;
                    float env = Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 2.4f);
                    data[i] = Mathf.Clamp(filtered * env * 0.55f, -1f, 1f);
                }
                paperRustleClip = AudioClip.Create("Han_UI_PaperRustle", samples, 1, sampleRate, false);
                paperRustleClip.SetData(data, 0);
            }

            // 5. Modal menu pop-in (warm low ledger chord + paper slide)
            if (menuOpenClip == null)
            {
                const int samples = 3600;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float env = Mathf.Sin(Mathf.Min(1f, t * 8f) * Mathf.PI * 0.5f) * Mathf.Exp(-t * 4.2f);
                    float f1 = Mathf.Sin(2f * Mathf.PI * 196f * i / sampleRate);
                    float f2 = Mathf.Sin(2f * Mathf.PI * 293.66f * i / sampleRate) * 0.6f;
                    data[i] = (f1 + f2) * 0.5f * env;
                }
                menuOpenClip = AudioClip.Create("Han_UI_MenuOpen", samples, 1, sampleRate, false);
                menuOpenClip.SetData(data, 0);
            }

            // 6. Modal menu close (descending latch release)
            if (menuCloseClip == null)
            {
                const int samples = 2600;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples;
                    float env = Mathf.Exp(-t * 5.5f);
                    float freq = Mathf.Lerp(260f, 146f, t);
                    data[i] = Mathf.Sin(2f * Mathf.PI * freq * i / sampleRate) * env * 0.65f;
                }
                menuCloseClip = AudioClip.Create("Han_UI_MenuClose", samples, 1, sampleRate, false);
                menuCloseClip.SetData(data, 0);
            }

            // 7. Typewriter key tick
            if (typewriterTickClip == null)
            {
                const int samples = 480;
                float[] data = new float[samples];
                uint rng = 4242u;
                for (int i = 0; i < samples; i++)
                {
                    rng = rng * 1664525u + 1013904223u;
                    float noise = ((rng & 0xFFFF) / 32768f - 1f);
                    float env = Mathf.Exp(-i * 0.022f);
                    data[i] = noise * env * 0.7f;
                }
                typewriterTickClip = AudioClip.Create("Han_UI_TypeTick", samples, 1, sampleRate, false);
                typewriterTickClip.SetData(data, 0);
            }
        }
    }
}
