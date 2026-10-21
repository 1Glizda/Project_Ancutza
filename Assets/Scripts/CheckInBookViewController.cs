using System;
using UnityEngine;
using UnityEngine.Events;

namespace Han.CheckIn
{
    /// <summary>
    /// CheckInBookViewController
    /// Controls raising and lowering the 3D check-in register book viewmodel when the player looks down.
    /// Supports:
    /// - Pitch detection with angle hysteresis (avoids boundary chatter).
    /// - Manual toggle key (Tab / B) for quick testing and accessibility.
    /// - Smooth 3D Viewmodel Transform interpolation AND/OR UI RectTransform.
    /// - Procedural 3D micro-sway / breathing bob (Doom 64 / Ultrakill feel).
    /// - Diegetic inspection light fading (so pages remain legible in deep Transylvanian midnight).
    /// </summary>
    public class CheckInBookViewController : MonoBehaviour
    {
        [Header("Camera & Tracking")]
        [Tooltip("The camera transform to read pitch from. If null, auto-finds Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        [Header("Hysteresis Angle Thresholds")]
        [Tooltip("Downward pitch angle (degrees) to raise book. Unity pitch: > 0 is looking down.")]
        [Range(25f, 75f)]
        [SerializeField] private float enterPitchAngle = 45f;

        [Tooltip("Downward pitch angle (degrees) below which book is lowered. Must be lower than enter angle.")]
        [Range(10f, 50f)]
        [SerializeField] private float exitPitchAngle = 30f;

        [Header("Manual Key Toggle (Convenience / Testing)")]
        [Tooltip("Optional keyboard key to toggle the book regardless of pitch angle.")]
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        [Header("3D Viewmodel Transform Targets")]
        [Tooltip("Root Transform of the 3D Book Viewmodel.")]
        [SerializeField] private Transform bookViewmodelTransform;

        [Tooltip("Local position relative to camera when fully raised in reading view (framed so entire book is visible).")]
        [SerializeField] private Vector3 raisedLocalPosition = new Vector3(0f, -0.015f, 0.48f);

        [Tooltip("Local position relative to camera when lowered out of view.")]
        [SerializeField] private Vector3 loweredLocalPosition = new Vector3(0f, -0.90f, 0.38f);

        [Tooltip("Local rotation (Euler angles) relative to camera when raised.")]
        [SerializeField] private Vector3 raisedLocalEuler = new Vector3(12f, 0f, 0f);

        [Tooltip("Local rotation (Euler angles) relative to camera when lowered.")]
        [SerializeField] private Vector3 loweredLocalEuler = new Vector3(60f, 0f, 0f);

        [Header("Player Movement & Look Containment")]
        [Tooltip("Disables player walking, sprinting, and jumping when reading the book.")]
        [SerializeField] private bool disableMovementWhileReading = true;
        [SerializeField] private movement playerMovement;

        [Header("Contained Screen Inspection Clamps")]
        [Tooltip("Allows subtle mouse look while reading but keeps the screen strictly contained on the book.")]
        [SerializeField] private bool containScreenLook = true;
        [SerializeField] private float maxInspectionYawDelta = 14f;
        [SerializeField] private float minInspectionPitch = 38f;
        [SerializeField] private float maxInspectionPitch = 65f;

        [Header("2D UI Fallback (Optional)")]
        [Tooltip("Optional 2D RectTransform if using screen-space UI mode.")]
        [SerializeField] private RectTransform bookRectTransform;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Vector2 uiRaisedAnchoredPos = new Vector2(0f, -40f);
        [SerializeField] private Vector2 uiLoweredAnchoredPos = new Vector2(0f, -800f);

        [Header("Inspection Light (Diegetic Illumination)")]
        [Tooltip("Subtle warm point light attached to the viewmodel to illuminate pages in pitch darkness.")]
        [SerializeField] private Light inspectionLight;
        [SerializeField] private float maxInspectionLightIntensity = 0.95f;

        [Header("Animation & Transitions")]
        [Tooltip("Duration in seconds for the raising/lowering transition.")]
        [Range(0.1f, 1.2f)]
        [SerializeField] private float transitionDuration = 0.30f;

        [Tooltip("Animation curve for raising/lowering interpolation.")]
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Procedural 3D Micro-Sway (Doom / Retro Juiciness)")]
        [Tooltip("Enable subtle breathing / idle bobbing when holding the book.")]
        [SerializeField] private bool enableProceduralSway = true;
        [SerializeField] private float swayFrequency = 1.7f;
        [SerializeField] private float swayPosAmplitude = 0.005f;
        [SerializeField] private float swayRotAmplitude = 1.2f;


        [Header("Audio Feedback")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip bookRaiseSfx;
        [SerializeField] private AudioClip bookLowerSfx;

        [Header("Data & Page Navigation Link")]
        [SerializeField] private GuestBookUI guestBookUI;

        [Header("Events")]
        public UnityEvent onBookOpened;
        public UnityEvent onBookClosed;
        public Action<bool> OnBookStateChanged;

        // Runtime State
        private bool isBookOpen = false;
        private bool isKeyToggled = false;
        private float currentTransitionProgress = 0f; // 0 = lowered, 1 = raised
        private float currentPitch = 0f;
        private float swayTimer = 0f;

        public bool IsBookOpen => isBookOpen;
        public float CurrentPitch => currentPitch;
        public float TransitionProgress => currentTransitionProgress;

        private void Awake()
        {
            FindCameraReference();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            if (bookViewmodelTransform == null && bookRectTransform == null)
                bookViewmodelTransform = transform;

            if (guestBookUI == null)
                guestBookUI = GetComponentInChildren<GuestBookUI>();

            Canvas canvas = GetComponentInChildren<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                Camera overlay = transform.parent != null ? transform.parent.Find("BookOverlayCamera")?.GetComponent<Camera>() : null;
                if (overlay == null && Camera.main != null)
                    overlay = Camera.main.transform.Find("BookOverlayCamera")?.GetComponent<Camera>();

                if (overlay != null)
                    canvas.worldCamera = overlay;
            }

            if (inspectionLight == null)
                inspectionLight = GetComponentInChildren<Light>();

            if (exitPitchAngle >= enterPitchAngle)
                exitPitchAngle = enterPitchAngle - 10f;
        }

        private void FindCameraReference()
        {
            if (cameraTransform == null)
            {
                if (transform.parent != null && transform.parent.GetComponent<Camera>() != null)
                    cameraTransform = transform.parent;
                else if (Camera.main != null)
                    cameraTransform = Camera.main.transform;
            }
        }

        private void Start()
        {
            FindCameraReference();
            currentTransitionProgress = 0f;
            ApplyPose(0f);
        }

        private void Update()
        {
            UpdateCameraPitch();
            HandleManualToggle();
            EvaluatePitchActivation();
            UpdateTransition();
            HandlePageInputs();
        }

        /// <summary>
        /// Reads camera pitch and converts Unity's [0, 360) Euler representation to signed [-180, +180].
        /// > 0 = Looking Down (towards ground)
        /// < 0 = Looking Up (towards sky)
        /// </summary>
        private void UpdateCameraPitch()
        {
            if (cameraTransform == null)
            {
                FindCameraReference();
                if (cameraTransform == null) return;
            }

            float rawPitch = cameraTransform.localEulerAngles.x;

            if (rawPitch > 180f)
                rawPitch -= 360f;

            currentPitch = rawPitch;
        }

        private void HandleManualToggle()
        {
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
            {
                isKeyToggled = !isKeyToggled;
                SetBookState(isKeyToggled);
            }
        }

        /// <summary>
        /// Evaluates hysteresis thresholds to switch open/closed states based on look-down angle.
        /// </summary>
        private void EvaluatePitchActivation()
        {
            if (isKeyToggled) return; // Manual toggle overrides pitch

            if (!isBookOpen)
            {
                if (currentPitch >= enterPitchAngle)
                {
                    SetBookState(true);
                }
            }
            else
            {
                if (currentPitch <= exitPitchAngle)
                {
                    SetBookState(false);
                }
            }
        }

        public void SetBookState(bool open)
        {
            if (isBookOpen == open) return;

            isBookOpen = open;

            // Handle player movement and contained inspection look
            if (playerMovement == null)
            {
                if (cameraTransform != null)
                {
                    playerMovement = cameraTransform.GetComponentInParent<movement>();
                }
                if (playerMovement == null)
                {
                    playerMovement = UnityEngine.Object.FindFirstObjectByType<movement>();
                }
            }

            if (playerMovement != null)
            {
                if (disableMovementWhileReading)
                {
                    playerMovement.canMove = !open;
                }

                if (containScreenLook)
                {
                    playerMovement.isInspectionMode = open;
                    if (open)
                    {
                        playerMovement.inspectionCenterYaw = playerMovement.transform.eulerAngles.y;
                        playerMovement.minInspectionPitch = minInspectionPitch;
                        playerMovement.maxInspectionPitch = maxInspectionPitch;
                        playerMovement.maxInspectionYawDelta = maxInspectionYawDelta;
                    }
                }
            }
            movement.MovementLocked = disableMovementWhileReading && open;

            if (isBookOpen)
            {
                Debug.Log($"[CheckInBook] 📖 Book Raised into view! Movement disabled, screen contained. (Pitch: {currentPitch:F1}°)");
                PlaySound(bookRaiseSfx);
                onBookOpened?.Invoke();
            }
            else
            {
                Debug.Log($"[CheckInBook] 📕 Book Lowered out of view! Movement restored. (Pitch: {currentPitch:F1}°)");
                isKeyToggled = false;
                PlaySound(bookLowerSfx);
                onBookClosed?.Invoke();
            }

            OnBookStateChanged?.Invoke(isBookOpen);
        }

        private void OnDisable()
        {
            if (isBookOpen)
            {
                SetBookState(false);
            }
        }

        private void UpdateTransition()
        {
            float targetProgress = isBookOpen ? 1f : 0f;
            float step = Time.deltaTime / Mathf.Max(0.01f, transitionDuration);

            currentTransitionProgress = Mathf.MoveTowards(currentTransitionProgress, targetProgress, step);

            float curveT = transitionCurve.Evaluate(currentTransitionProgress);
            ApplyPose(curveT);
        }

        /// <summary>
        /// Applies interpolated position, rotation, sway, and lighting based on progress t.
        /// </summary>
        private void ApplyPose(float t)
        {
            // Calculate procedural micro-sway
            Vector3 swayPosOffset = Vector3.zero;
            Vector3 swayRotOffset = Vector3.zero;

            if (enableProceduralSway && t > 0.05f)
            {
                swayTimer += Time.deltaTime * swayFrequency;
                float sinVal = Mathf.Sin(swayTimer);
                float cosVal = Mathf.Cos(swayTimer * 0.8f);

                swayPosOffset = new Vector3(cosVal * swayPosAmplitude * 0.4f, sinVal * swayPosAmplitude, 0f) * t;
                swayRotOffset = new Vector3(sinVal * swayRotAmplitude * 0.6f, 0f, cosVal * swayRotAmplitude) * t;
            }

            // 1. Apply to 3D Viewmodel Transform
            if (bookViewmodelTransform != null)
            {
                Vector3 targetPos = Vector3.Lerp(loweredLocalPosition, raisedLocalPosition, t) + swayPosOffset;
                Vector3 targetRotEuler = Vector3.Lerp(loweredLocalEuler, raisedLocalEuler, t) + swayRotOffset;

                bookViewmodelTransform.localPosition = targetPos;
                bookViewmodelTransform.localRotation = Quaternion.Euler(targetRotEuler);
            }

            // 2. Apply to 2D UI RectTransform (if assigned)
            if (bookRectTransform != null)
            {
                Vector2 baseUiPos = Vector2.Lerp(uiLoweredAnchoredPos, uiRaisedAnchoredPos, t);
                baseUiPos.y += swayPosOffset.y * 100f;
                bookRectTransform.anchoredPosition = baseUiPos;
                bookRectTransform.localRotation = Quaternion.Euler(0f, 0f, swayRotOffset.z);

                if (canvasGroup != null)
                {
                    canvasGroup.alpha = Mathf.Clamp01(t * 1.5f);
                    canvasGroup.interactable = isBookOpen && t > 0.85f;
                    canvasGroup.blocksRaycasts = isBookOpen && t > 0.85f;
                }
            }

            // 3. Fade inspection light
            if (inspectionLight != null)
            {
                inspectionLight.intensity = maxInspectionLightIntensity * t;
                inspectionLight.enabled = t > 0.01f;
            }
        }

        private void HandlePageInputs()
        {
            if (!isBookOpen || guestBookUI == null || currentTransitionProgress < 0.8f)
                return;

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0.05f || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                guestBookUI.PreviousPage();
            }
            else if (scroll < -0.05f || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                guestBookUI.NextPage();
            }
        }

        private void PlaySound(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.94f, 1.06f);
                audioSource.PlayOneShot(clip);
            }
        }

        public float ApplyInspectionPitchClamp(float currentCameraPitch)
        {
            if (!isBookOpen || !containScreenLook) return currentCameraPitch;
            return Mathf.Clamp(currentCameraPitch, minInspectionPitch, maxInspectionPitch);
        }
    }
}
