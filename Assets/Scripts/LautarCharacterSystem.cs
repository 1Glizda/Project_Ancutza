using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Han.Tavern
{
    public enum SwayMotionMode
    {
        [Tooltip("Pure sinusoidal rhythmic oscillation (traditional beat).")]
        HarmonicSine,
        [Tooltip("Pure Perlin noise oscillation (organic, non-repeating lifelike motion).")]
        OrganicNoise,
        [Tooltip("Rhythmic sine beat layered with organic Perlin noise perturbations.")]
        Blended
    }

    /// <summary>
    /// LautarCharacterSystem:
    /// Manages the traditional Romanian tavern bard ("Lăutar") character attached to 'carpet_lautar'.
    /// 
    /// Features:
    /// 1. Spawn & Persistence:
    ///    - Random visit frequency (every 1 to 3 nights).
    ///    - Exposed OnNewNight(int nightIndex) hook (and auto-subscribes to HanGameDirector.OnNightStarted).
    ///    - 'L' key debug toggle in Update() for instant live testing.
    /// 2. Audio Management & Hall Reverb:
    ///    - Random sequential song playlist without immediate repetitions.
    ///    - Automatic next-song triggering with seamless looping while spawned.
    ///    - Audio stops immediately when despawned.
    ///    - Built-in AudioReverbFilter with customizable hall presets (Concerthall, Auditorium, Room) for rich tavern acoustics.
    /// 3. Singing Mouth Animation:
    ///    - Separate child GameObject with SpriteRenderer for freely customizable mouth placement.
    ///    - Random cycling through 4 mouth shapes at a configurable cadence (0.1s - 0.2s) while singing.
    ///    - Reverts to idle/closed mouth when silent or despawned.
    /// 4. Player-Facing Billboarding:
    ///    - Continuously rotates the 2D bard around the vertical Y-axis to face the player/camera in 3D space.
    /// 5. Rhythmic Singing Sway:
    ///    - Adds smooth procedural left-to-right body tilt (roll), head turning (yaw), and subtle breathing bobbing while singing.
    ///    - Smoothly eases back to upright standing rest when music stops or character despawns.
    /// </summary>
    [DisallowMultipleComponent]
    public class LautarCharacterSystem : MonoBehaviour
    {
        // ------------------------------------------------------------------
        //  Inspector: Visual References
        // ------------------------------------------------------------------
        [Header("Visual Hierarchy References")]
        [Tooltip("Root GameObject containing all Lăutar visual components (body, mouth). Toggled active/inactive on spawn/despawn.")]
        [SerializeField] private GameObject characterVisualRoot;

        [Tooltip("Transform representing the bard's body that rotates/billboards toward the player.")]
        [SerializeField] private Transform bodyTransform;

        [Tooltip("SpriteRenderer displaying the main character sprite (Florin).")]
        [SerializeField] private SpriteRenderer bodyRenderer;

        [Tooltip("Separate child SpriteRenderer for the mouth animation. Adjust its local Transform position in the Inspector to align over the face.")]
        [SerializeField] private SpriteRenderer mouthRenderer;

        // ------------------------------------------------------------------
        //  Inspector: Mouth Sprites & Cadence
        // ------------------------------------------------------------------
        [Header("Singing Mouth Animation")]
        [Tooltip("4 mouth shape sprites used while singing (e.g., 0=Closed, 1=SlightlyOpen, 2=Open_O, 3=WideOpen_A).")]
        [SerializeField] private Sprite[] mouthSprites = new Sprite[4];

        [Tooltip("Idle closed mouth sprite assigned when not singing or when despawned. If left empty, mouthSprites[0] is used.")]
        [SerializeField] private Sprite idleClosedMouthSprite;

        [Tooltip("Minimum duration (in seconds) between mouth shape changes while singing.")]
        [SerializeField, Range(0.05f, 0.40f)] private float minMouthCadence = 0.10f;

        [Tooltip("Maximum duration (in seconds) between mouth shape changes while singing.")]
        [SerializeField, Range(0.05f, 0.40f)] private float maxMouthCadence = 0.20f;

        // ------------------------------------------------------------------
        //  Inspector: Audio & Hall Reverb Settings
        // ------------------------------------------------------------------
        [Header("Audio & Song Playlist")]
        [Tooltip("3D AudioSource that plays the bard's songs. Configured for tavern spatial acoustics.")]
        [SerializeField] private AudioSource audioSource;

        [Tooltip("List of song clips to play at random. Songs will cycle sequentially without playing the same song twice in a row.")]
        [SerializeField] private AudioClip[] songClips = new AudioClip[0];

        [Tooltip("Optional pause (in seconds) between consecutive songs.")]
        [SerializeField, Range(0f, 3f)] private float pauseBetweenSongs = 0.5f;

        [Header("Hall Effect / Acoustics")]
        [Tooltip("Enable hall acoustic reverb filter on the bard's singing.")]
        [SerializeField] private bool enableHallReverb = true;

        [Tooltip("Acoustic preset for the hall reverb filter. 'Concerthall' or 'Auditorium' delivers authentic tavern hall reverberation.")]
        [SerializeField] private AudioReverbPreset hallReverbPreset = AudioReverbPreset.Concerthall;

        [Tooltip("Reference to the AudioReverbFilter on this GameObject.")]
        [SerializeField] private AudioReverbFilter audioReverbFilter;

        [Tooltip("Walls between the bard and the listener muffle and quiet the singing (adds an AudioOcclusion component).")]
        [SerializeField] private bool enableSoundOcclusion = true;

        // ------------------------------------------------------------------
        //  Inspector: Night Spawn & Persistence
        // ------------------------------------------------------------------
        [Header("Night Spawn Settings")]
        [Tooltip("Whether the bard can appear on Night 1.")]
        [SerializeField] private bool appearOnFirstNight = true;

        [Tooltip("Minimum nights between visits (e.g., 1).")]
        [SerializeField, Range(1, 5)] private int minNightsInterval = 1;

        [Tooltip("Maximum nights between visits (e.g., 3).")]
        [SerializeField, Range(1, 5)] private int maxNightsInterval = 3;

        [Tooltip("If true, the character is spawned immediately on Start() regardless of night index.")]
        [SerializeField] private bool spawnOnStart = false;

        [Tooltip("Automatically listen to HanGameDirector.OnNightStarted events.")]
        [SerializeField] private bool autoSubscribeToGameDirector = true;

        // ------------------------------------------------------------------
        //  Inspector: Player Billboarding
        // ------------------------------------------------------------------
        [Header("Player Tracking & Billboarding")]
        [Tooltip("If true, the body billboard-rotates to face the player/camera.")]
        [SerializeField] private bool enableBillboarding = true;

        [Tooltip("Transform of the Player to face. If unassigned, automatically finds GameObject tagged 'Player' or named 'Player'.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Fallback camera to face if player transform is not found. Defaults to Camera.main.")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("Restricts billboarding to horizontal Y-axis rotation so the sprite stands upright on the floor without tipping backwards/forwards.")]
        [SerializeField] private bool lockPitchAndRollToUpright = true;

        [Tooltip("Invert facing direction if the sprite texture was imported facing the opposite direction.")]
        [SerializeField] private bool invertFacingDirection = false;

        // ------------------------------------------------------------------
        //  Inspector: Singing Sway Movement & Organic Noise
        // ------------------------------------------------------------------
        [Header("Singing Sway Movement & Organic Noise")]
        [Tooltip("Enables rhythmic body sway and bounce while singing.")]
        [SerializeField] private bool enableSingingSway = true;

        [Tooltip("Motion calculation type: HarmonicSine (rhythmic beat), OrganicNoise (non-repeating lifelike motion), or Blended (rhythmic sine with noise layered on top).")]
        [SerializeField] private SwayMotionMode swayMotionMode = SwayMotionMode.Blended;

        [Tooltip("Blend ratio of Perlin noise in Blended mode (0 = 100% sine, 1 = 100% organic noise).")]
        [SerializeField, Range(0f, 1f)] private float noiseInfluence = 0.40f;

        [Tooltip("Speed multiplier for the Perlin noise calculation.")]
        [SerializeField, Range(0.2f, 5.0f)] private float noiseFrequency = 1.35f;

        [Tooltip("Maximum left-right tilt roll angle in degrees (Z-axis tilt in billboard plane).")]
        [SerializeField, Range(0.5f, 15f)] private float maxSwayRollAngle = 4.5f;

        [Tooltip("Maximum subtle left-right head/body turn angle in degrees (Y-axis yaw).")]
        [SerializeField, Range(0f, 10f)] private float maxSwayYawAngle = 3.0f;

        [Tooltip("Enable subtle head nodding (X-axis pitch) driven by Perlin noise.")]
        [SerializeField] private bool enableNoisePitchNod = true;

        [Tooltip("Maximum subtle head nod angle in degrees (pitch).")]
        [SerializeField, Range(0f, 8f)] private float maxNoisePitchAngle = 2.0f;

        [Tooltip("Frequency of the singing sway in cycles per second (Hz). 1.5 - 2.5 Hz fits tavern folklore tempo.")]
        [SerializeField, Range(0.5f, 5.0f)] private float swayFrequency = 2.0f;

        [Tooltip("Subtle vertical breathing/bobbing distance in meters while singing.")]
        [SerializeField, Range(0f, 0.05f)] private float verticalBobAmount = 0.015f;

        [Tooltip("Subtle lateral hip/body shift in meters while swaying side to side.")]
        [SerializeField, Range(0f, 0.05f)] private float lateralSwayAmount = 0.015f;

        [Tooltip("Speed at which the sway motion transitions between idle and active singing.")]
        [SerializeField, Range(1f, 10f)] private float swayTransitionSpeed = 3.5f;

        // ------------------------------------------------------------------
        //  Inspector: Debug
        // ------------------------------------------------------------------
        [Header("Debug Controls")]
        [Tooltip("Enables the 'L' key toggle in Update() to instantly force spawn/despawn for testing.")]
        [SerializeField] private bool enableDebugKeyToggle = true;

        [SerializeField] private KeyCode debugToggleKey = KeyCode.L;

        // ------------------------------------------------------------------
        //  Runtime State
        // ------------------------------------------------------------------
        private bool isSpawned = false;
        private int nextScheduledNight = -1;
        private int lastPlayedSongIndex = -1;
        private int currentMouthIndex = -1;

        private float currentSwayWeight = 0f;
        private float swayTimer = 0f;
        private float noiseTimer = 0f;
        private Vector3 initialBodyLocalPosition = new Vector3(0f, 1.45f, 0f);

        private Coroutine songLoopCoroutine;
        private Coroutine mouthAnimationCoroutine;

        public bool IsSpawned => isSpawned;
        public int NextScheduledNight => nextScheduledNight;
        public AudioClip CurrentSong => audioSource != null ? audioSource.clip : null;
        public bool IsSinging => isSpawned && audioSource != null && audioSource.isPlaying;

        // ------------------------------------------------------------------
        //  Unity Lifecycle
        // ------------------------------------------------------------------
        private void Awake()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    ConfigureDefaultAudioSource(audioSource);
                }
            }

            // Configure Hall Reverb Filter
            ApplyReverbSettings();

            // Walls muffle the bard's singing (geometric sound occlusion)
            if (enableSoundOcclusion && audioSource != null && audioSource.GetComponent<AudioOcclusion>() == null)
            {
                audioSource.gameObject.AddComponent<AudioOcclusion>();
            }

            // Cache initial body local position for bobbing/swaying offsets
            if (bodyTransform != null)
            {
                initialBodyLocalPosition = bodyTransform.localPosition;
            }

            // Ensure idle mouth sprite is set up
            if (idleClosedMouthSprite == null && mouthSprites != null && mouthSprites.Length > 0)
            {
                idleClosedMouthSprite = mouthSprites[0];
            }
        }

        private void OnEnable()
        {
            if (autoSubscribeToGameDirector)
            {
                Han.CheckIn.HanGameDirector.OnNightStarted += OnNewNight;
            }
        }

        private void OnDisable()
        {
            if (autoSubscribeToGameDirector)
            {
                Han.CheckIn.HanGameDirector.OnNightStarted -= OnNewNight;
            }

            StopAllPlayback();
        }

        private void Start()
        {
            ResolveTargetReferences();

            // Default initial state: despawned until triggered by OnNightStarted (after all check-ins finish) or debug key
            if (spawnOnStart && Han.CheckIn.HanGameDirector.Instance == null)
            {
                Spawn(false);
            }
            else
            {
                Despawn();
            }
        }

        private void Update()
        {
            // Debug toggle via 'L' key
            if (enableDebugKeyToggle && CheckDebugKeyPressed())
            {
                ToggleSpawn();
            }
        }

        private void LateUpdate()
        {
            if (!isSpawned || bodyTransform == null) return;

            UpdateBillboardingAndSway();
        }

        private void OnValidate()
        {
            ApplyReverbSettings();
        }

        // ------------------------------------------------------------------
        //  Target & References Resolution
        // ------------------------------------------------------------------
        private void ResolveTargetReferences()
        {
            if (playerTransform == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                {
                    playerTransform = playerObj.transform;
                }
                else
                {
                    var namedPlayer = GameObject.Find("Player");
                    if (namedPlayer != null)
                    {
                        playerTransform = namedPlayer.transform;
                    }
                }
            }

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }
        }

        // ------------------------------------------------------------------
        //  Hall Effect (Reverb Filter)
        // ------------------------------------------------------------------
        /// <summary>
        /// Applies the acoustic hall reverb filter settings to the bard's singing audio.
        /// </summary>
        public void ApplyReverbSettings()
        {
            if (audioReverbFilter == null)
            {
                audioReverbFilter = GetComponent<AudioReverbFilter>();
                if (audioReverbFilter == null && enableHallReverb)
                {
                    audioReverbFilter = gameObject.AddComponent<AudioReverbFilter>();
                }
            }

            if (audioReverbFilter != null)
            {
                audioReverbFilter.enabled = enableHallReverb;
                if (enableHallReverb)
                {
                    audioReverbFilter.reverbPreset = hallReverbPreset;
                }
            }
        }

        // ------------------------------------------------------------------
        //  Billboarding & Rhythmic Singing Sway
        // ------------------------------------------------------------------
        private void UpdateBillboardingAndSway()
        {
            // 1. Determine billboard target position (Player preferred, Camera fallback)
            Vector3 targetPos = Vector3.zero;
            bool hasTarget = false;

            if (playerTransform != null)
            {
                targetPos = playerTransform.position;
                hasTarget = true;
            }
            else if (targetCamera != null)
            {
                targetPos = targetCamera.transform.position;
                hasTarget = true;
            }
            else
            {
                targetCamera = Camera.main;
                if (targetCamera != null)
                {
                    targetPos = targetCamera.transform.position;
                    hasTarget = true;
                }
            }

            // 2. Base Billboard Look Rotation
            Quaternion baseRotation = bodyTransform.rotation;
            if (enableBillboarding && hasTarget)
            {
                Vector3 direction = targetPos - bodyTransform.position;
                if (lockPitchAndRollToUpright)
                {
                    direction.y = 0f; // Keep Florin upright on the tavern floor
                }

                if (direction.sqrMagnitude > 0.001f)
                {
                    // In Unity SpriteRenderer, the visible front faces -Z (local -forward).
                    // Pointing -forward at the player means the front face directly faces the player un-mirrored.
                    Vector3 facingVector = invertFacingDirection ? direction.normalized : -direction.normalized;
                    baseRotation = Quaternion.LookRotation(facingVector, Vector3.up);
                }
            }

            // 3. Singing Sway Motion & Weight Transition
            bool singing = isSpawned && audioSource != null && audioSource.isPlaying;
            float targetWeight = singing && enableSingingSway ? 1f : 0f;
            currentSwayWeight = Mathf.MoveTowards(currentSwayWeight, targetWeight, Time.deltaTime * swayTransitionSpeed);

            if (currentSwayWeight > 0.0001f)
            {
                swayTimer += Time.deltaTime * swayFrequency * Mathf.PI * 2f;
                noiseTimer += Time.deltaTime * noiseFrequency;

                float sinVal = Mathf.Sin(swayTimer);

                // Multi-axis organic Perlin noise samples with unique seeds
                float nRoll = (Mathf.PerlinNoise(noiseTimer, 17.31f) * 2f - 1f);
                float nYaw = (Mathf.PerlinNoise(noiseTimer, 43.73f) * 2f - 1f);
                float nPitch = (Mathf.PerlinNoise(noiseTimer, 81.19f) * 2f - 1f);
                float nBob = (Mathf.PerlinNoise(noiseTimer, 62.45f) * 2f - 1f);
                float nShift = (Mathf.PerlinNoise(noiseTimer, 95.82f) * 2f - 1f);

                float rollFactor;
                float yawFactor;
                float bobFactor;
                float shiftFactor;

                switch (swayMotionMode)
                {
                    case SwayMotionMode.OrganicNoise:
                        rollFactor = nRoll;
                        yawFactor = nYaw;
                        bobFactor = Mathf.Clamp01((nBob + 1f) * 0.5f);
                        shiftFactor = nShift;
                        break;

                    case SwayMotionMode.Blended:
                        rollFactor = Mathf.Lerp(sinVal, nRoll, noiseInfluence);
                        yawFactor = Mathf.Lerp(sinVal, nYaw, noiseInfluence);
                        bobFactor = Mathf.Lerp(Mathf.Abs(sinVal), Mathf.Clamp01((nBob + 1f) * 0.5f), noiseInfluence);
                        shiftFactor = Mathf.Lerp(sinVal, nShift, noiseInfluence);
                        break;

                    case SwayMotionMode.HarmonicSine:
                    default:
                        rollFactor = sinVal;
                        yawFactor = sinVal;
                        bobFactor = Mathf.Abs(sinVal);
                        shiftFactor = sinVal;
                        break;
                }

                // Rhythmic left/right tilt (roll) around the billboard view axis
                float rollAngle = rollFactor * maxSwayRollAngle * currentSwayWeight;

                // Subtle left/right head turn (yaw) around the vertical Y-axis
                float yawAngle = yawFactor * maxSwayYawAngle * currentSwayWeight;

                // Subtle organic head nodding (pitch) driven by Perlin noise
                float pitchAngle = enableNoisePitchNod ? nPitch * maxNoisePitchAngle * currentSwayWeight : 0f;

                // Subtle vertical breathing bob
                float bobY = bobFactor * verticalBobAmount * currentSwayWeight;

                // Subtle lateral shift
                float shiftX = shiftFactor * lateralSwayAmount * currentSwayWeight;

                // Combine base billboard orientation with procedural sway tilt, yaw, and noise pitch
                Quaternion swayRotation = Quaternion.Euler(pitchAngle, yawAngle, rollAngle);
                bodyTransform.rotation = baseRotation * swayRotation;

                // Apply position offset (bob + shift) relative to initial local position
                bodyTransform.localPosition = initialBodyLocalPosition + new Vector3(shiftX, bobY, 0f);
            }
            else
            {
                // Idle rest: upright orientation and neutral position
                bodyTransform.rotation = baseRotation;
                bodyTransform.localPosition = initialBodyLocalPosition;
                swayTimer = 0f;
                noiseTimer = 0f;
            }
        }

        // ------------------------------------------------------------------
        //  Spawn & Night Scheduling Logic
        // ------------------------------------------------------------------
        /// <summary>
        /// Public hook called when a new night starts (e.g. from HanGameDirector or GameLoopManager).
        /// Decides whether the bard visits tonight (every 1 to 3 nights at random).
        /// </summary>
        /// <param name="nightIndex">Current night index (1-based).</param>
        public void OnNewNight(int nightIndex)
        {
            // Initialize schedule if first evaluation
            if (nextScheduledNight == -1)
            {
                if (nightIndex == 1 && appearOnFirstNight)
                {
                    nextScheduledNight = 1;
                }
                else
                {
                    nextScheduledNight = nightIndex + UnityEngine.Random.Range(minNightsInterval, maxNightsInterval + 1);
                }
            }

            if (nightIndex >= nextScheduledNight)
            {
                // Bard appears tonight!
                Spawn();

                // Schedule next appearance 1 to 3 nights after tonight
                int interval = UnityEngine.Random.Range(minNightsInterval, maxNightsInterval + 1);
                nextScheduledNight = nightIndex + interval;

                Debug.Log($"<color=#D4AF37>[Lăutar System]</color> The bard has arrived at the tavern on Night {nightIndex}! Next visit scheduled for Night {nextScheduledNight}.");
            }
            else
            {
                // Bard does not visit tonight
                Despawn();
                Debug.Log($"<color=#A0A0A0>[Lăutar System]</color> The bard is away on Night {nightIndex}. Next scheduled visit is Night {nextScheduledNight}.");
            }
        }

        /// <summary>
        /// Instantly forces the bard to spawn, starting music and mouth animation.
        /// </summary>
        [ContextMenu("Debug: Force Spawn")]
        public void Spawn()
        {
            Spawn(false);
        }

        public void Spawn(bool force)
        {
            // Do not spawn or sing during the evening CheckIn phase unless forced via debug
            if (!force && Han.CheckIn.HanGameDirector.Instance != null &&
                Han.CheckIn.HanGameDirector.Instance.Phase == Han.CheckIn.HanPhase.CheckIn)
            {
                Debug.Log("<color=#E6C280>[Lăutar System]</color> Bard is waiting quietly at the inn — singing will begin once all check-ins are finished and Night starts.");
                Despawn();
                return;
            }

            isSpawned = true;

            if (characterVisualRoot != null)
            {
                characterVisualRoot.SetActive(true);
            }

            ResolveTargetReferences();
            ApplyReverbSettings();

            // Reset mouth to idle before singing starts
            SetMouthIdle();

            // Start audio playlist loop
            if (songLoopCoroutine != null) StopCoroutine(songLoopCoroutine);
            songLoopCoroutine = StartCoroutine(SongLoopRoutine());

            // Start mouth animation loop
            if (mouthAnimationCoroutine != null) StopCoroutine(mouthAnimationCoroutine);
            mouthAnimationCoroutine = StartCoroutine(MouthAnimationRoutine());
        }

        /// <summary>
        /// Instantly forces the bard to despawn, stopping music and mouth animation.
        /// </summary>
        [ContextMenu("Debug: Force Despawn")]
        public void Despawn()
        {
            isSpawned = false;

            StopAllPlayback();

            if (characterVisualRoot != null)
            {
                characterVisualRoot.SetActive(false);
            }
        }

        /// <summary>
        /// Toggles between spawned and despawned states.
        /// </summary>
        [ContextMenu("Debug: Toggle Spawn (L)")]
        public void ToggleSpawn()
        {
            SetSpawned(!isSpawned, force: true);
            Debug.Log($"<color=#E6C280>[Lăutar System]</color> Debug toggled via 'L' key -> {(isSpawned ? "SPAWNED" : "DESPAWNED")}.");
        }

        public void SetSpawned(bool spawn, bool force = false)
        {
            if (spawn) Spawn(force);
            else Despawn();
        }

        private void StopAllPlayback()
        {
            if (songLoopCoroutine != null)
            {
                StopCoroutine(songLoopCoroutine);
                songLoopCoroutine = null;
            }

            if (mouthAnimationCoroutine != null)
            {
                StopCoroutine(mouthAnimationCoroutine);
                mouthAnimationCoroutine = null;
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            SetMouthIdle();
        }

        // ------------------------------------------------------------------
        //  Audio Management & Random Sequential Playlist
        // ------------------------------------------------------------------
        private IEnumerator SongLoopRoutine()
        {
            // Small initial pause upon arrival before striking the first chord
            yield return new WaitForSeconds(0.35f);

            while (isSpawned)
            {
                AudioClip nextClip = PickNextSong();
                if (nextClip == null)
                {
                    yield return new WaitForSeconds(1.0f);
                    continue;
                }

                if (audioSource != null)
                {
                    audioSource.clip = nextClip;
                    audioSource.Play();
                }

                // Wait until song finishes playing
                while (isSpawned && audioSource != null && audioSource.isPlaying)
                {
                    yield return null;
                }

                if (!isSpawned) break;

                // Brief pause between songs
                if (pauseBetweenSongs > 0f)
                {
                    yield return new WaitForSeconds(pauseBetweenSongs);
                }
            }
        }

        /// <summary>
        /// Picks a random song from songClips, guaranteed never to be identical to the previous song.
        /// </summary>
        private AudioClip PickNextSong()
        {
            if (songClips == null || songClips.Length == 0) return null;

            if (songClips.Length == 1)
            {
                lastPlayedSongIndex = 0;
                return songClips[0];
            }

            int nextIndex;
            do
            {
                nextIndex = UnityEngine.Random.Range(0, songClips.Length);
            } while (nextIndex == lastPlayedSongIndex);

            lastPlayedSongIndex = nextIndex;
            return songClips[nextIndex];
        }

        // ------------------------------------------------------------------
        //  Singing Mouth Animation
        // ------------------------------------------------------------------
        private IEnumerator MouthAnimationRoutine()
        {
            while (isSpawned)
            {
                bool isSinging = audioSource != null && audioSource.isPlaying;

                if (isSinging && mouthSprites != null && mouthSprites.Length > 0)
                {
                    // Pick a random mouth sprite, preferring a different frame for dynamic animation
                    int nextIndex;
                    if (mouthSprites.Length > 1)
                    {
                        do
                        {
                            nextIndex = UnityEngine.Random.Range(0, mouthSprites.Length);
                        } while (nextIndex == currentMouthIndex);
                    }
                    else
                    {
                        nextIndex = 0;
                    }

                    currentMouthIndex = nextIndex;

                    if (mouthRenderer != null && mouthSprites[nextIndex] != null)
                    {
                        mouthRenderer.sprite = mouthSprites[nextIndex];
                    }

                    float cadence = UnityEngine.Random.Range(minMouthCadence, maxMouthCadence);
                    yield return new WaitForSeconds(cadence);
                }
                else
                {
                    // Silent / paused: hold idle closed mouth
                    SetMouthIdle();
                    yield return new WaitForSeconds(0.1f);
                }
            }

            SetMouthIdle();
        }

        private void SetMouthIdle()
        {
            currentMouthIndex = -1;

            if (mouthRenderer != null)
            {
                mouthRenderer.sprite = idleClosedMouthSprite != null
                    ? idleClosedMouthSprite
                    : (mouthSprites != null && mouthSprites.Length > 0 ? mouthSprites[0] : null);
            }
        }

        // ------------------------------------------------------------------
        //  Input Helper
        // ------------------------------------------------------------------
        private bool CheckDebugKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
                return true;
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || !ENABLE_INPUT_SYSTEM
            if (Input.GetKeyDown(debugToggleKey))
                return true;
#endif

            return false;
        }

        private static void ConfigureDefaultAudioSource(AudioSource src)
        {
            src.playOnAwake = false;
            src.spatialBlend = 1.0f; // Full 3D spatial sound
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.minDistance = 3.0f;
            src.maxDistance = 28.0f;
            src.dopplerLevel = 0f;
            src.volume = 0.85f;
        }

        // ------------------------------------------------------------------
        //  Editor Setup Helper
        // ------------------------------------------------------------------
#if UNITY_EDITOR
        /// <summary>
        /// Context menu utility that automatically builds the child hierarchy,
        /// loads assets from Sprites & Sounds folders, sets up the Hall Reverb Filter,
        /// and binds serialized references.
        /// </summary>
        [ContextMenu("Auto-Configure Lautar Hierarchy & Assets")]
        public void AutoConfigureHierarchyAndAssets()
        {
            UnityEditor.Undo.RecordObject(gameObject, "Configure Lautar System");

            // 1. AudioSource & Hall Reverb Filter
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            }
            ConfigureDefaultAudioSource(audioSource);

            if (audioReverbFilter == null)
            {
                audioReverbFilter = GetComponent<AudioReverbFilter>();
                if (audioReverbFilter == null) audioReverbFilter = gameObject.AddComponent<AudioReverbFilter>();
            }
            ApplyReverbSettings();

            // 2. Load song clips
            var clipGuids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new string[] { "Assets/Sounds" });
            var loadedClips = new List<AudioClip>();
            foreach (var g in clipGuids)
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (clip != null) loadedClips.Add(clip);
            }
            songClips = loadedClips.ToArray();

            // 3. Child Hierarchy: Lautar_VisualRoot
            Transform rootTr = transform.Find("Lautar_VisualRoot");
            if (rootTr == null)
            {
                var rootGo = new GameObject("Lautar_VisualRoot");
                rootGo.transform.SetParent(transform, false);
                rootTr = rootGo.transform;
            }

            // Compensate for any non-uniform scale on carpet so character is 1:1
            Vector3 parentScale = transform.lossyScale;
            rootTr.localPosition = new Vector3(0f, 0.05f, 0f);
            rootTr.localRotation = Quaternion.identity;
            rootTr.localScale = new Vector3(
                parentScale.x > 0.001f ? 1f / parentScale.x : 1f,
                parentScale.y > 0.001f ? 1f / parentScale.y : 1f,
                parentScale.z > 0.001f ? 1f / parentScale.z : 1f
            );
            characterVisualRoot = rootTr.gameObject;

            // 4. Body (Florin)
            Transform bodyTr = rootTr.Find("Lautar_Body");
            if (bodyTr == null)
            {
                var bodyGo = new GameObject("Lautar_Body");
                bodyGo.transform.SetParent(rootTr, false);
                bodyTr = bodyGo.transform;
            }
            bodyTransform = bodyTr;
            bodyTr.localPosition = new Vector3(0f, 1.45f, 0f);
            bodyTr.localRotation = Quaternion.identity;
            bodyTr.localScale = Vector3.one;
            initialBodyLocalPosition = bodyTr.localPosition;

            bodyRenderer = bodyTr.GetComponent<SpriteRenderer>();
            if (bodyRenderer == null) bodyRenderer = bodyTr.gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Florin.png");
            bodyRenderer.sortingOrder = 0;

            // Optional URP 2D Unlit material
            var spriteShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            if (spriteShader != null && (bodyRenderer.sharedMaterial == null || bodyRenderer.sharedMaterial.shader != spriteShader))
            {
                bodyRenderer.sharedMaterial = new Material(spriteShader);
            }

            // 5. Mouth (Child of Body)
            Transform mouthTr = bodyTr.Find("Lautar_Mouth");
            if (mouthTr == null)
            {
                var mouthGo = new GameObject("Lautar_Mouth");
                mouthGo.transform.SetParent(bodyTr, false);
                mouthTr = mouthGo.transform;
            }
            // Positioned over Florin's face in local space, slightly forward in Z
            mouthTr.localPosition = new Vector3(-0.06f, 0.58f, -0.01f);
            mouthTr.localRotation = Quaternion.identity;
            mouthTr.localScale = Vector3.one;

            mouthRenderer = mouthTr.GetComponent<SpriteRenderer>();
            if (mouthRenderer == null) mouthRenderer = mouthTr.gameObject.AddComponent<SpriteRenderer>();
            mouthRenderer.sortingOrder = 1; // Always in front of body

            if (spriteShader != null && (mouthRenderer.sharedMaterial == null || mouthRenderer.sharedMaterial.shader != spriteShader))
            {
                mouthRenderer.sharedMaterial = new Material(spriteShader);
            }

            // 6. Load Mouth Sprites
            mouthSprites = new Sprite[4];
            mouthSprites[0] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Lautar/Mouth_0_Closed.png");
            mouthSprites[1] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Lautar/Mouth_1_SlightlyOpen.png");
            mouthSprites[2] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Lautar/Mouth_2_Open_O.png");
            mouthSprites[3] = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Lautar/Mouth_3_WideOpen_A.png");

            idleClosedMouthSprite = mouthSprites[0];
            mouthRenderer.sprite = idleClosedMouthSprite;

            // 7. Auto-detect Player transform
            ResolveTargetReferences();

            UnityEditor.EditorUtility.SetDirty(gameObject);
            UnityEditor.EditorUtility.SetDirty(rootTr.gameObject);
            UnityEditor.EditorUtility.SetDirty(bodyTr.gameObject);
            UnityEditor.EditorUtility.SetDirty(mouthTr.gameObject);

            Debug.Log("<color=#40C040>[Lăutar System]</color> Hierarchy, Florin Sprite, 4 Mouth Sprites, 2 Songs, Hall Reverb, and Player Billboarding configured on 'carpet_lautar' successfully!");
        }
#endif
    }
}
