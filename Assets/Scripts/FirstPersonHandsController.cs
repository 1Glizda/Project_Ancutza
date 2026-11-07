using UnityEngine;

namespace Han.CheckIn
{
    public enum FirstPersonHandsState
    {
        LoweredOut,
        RestingOnDesk,
        InspectingBook
    }

    /// <summary>
    /// Controls the first-person hands holding/resting on the check-in register ledger.
    /// Provides:
    /// - Distinct pose states: InspectingBook (gripping ledger edges), RestingOnDesk (relaxed on desk edge), LoweredOut.
    /// - Procedural asymmetric breathing sway & finger-pressure micro-adjustments during book inspection.
    /// - Reactive right-hand finger tracking / tapping motion while the typewriter inks the page.
    /// - Damped spring recoil when photos or stamps slam onto the ledger.
    /// - Smooth grip release and transition out of central view when panning up to the character.
    /// </summary>
    [ExecuteAlways]
    public class FirstPersonHandsController : MonoBehaviour
    {
        [Header("Hand References")]
        [SerializeField] private Transform leftHandTransform;
        [SerializeField] private Transform rightHandTransform;
        [SerializeField] private SpriteRenderer leftHandRenderer;
        [SerializeField] private SpriteRenderer rightHandRenderer;

        [Header("1. Inspecting Book Pose (Gripping Ledger Edges)")]
        [SerializeField] private Vector3 leftInspectPos = new Vector3(-0.236f, -0.078f, -0.016f);
        [SerializeField] private Vector3 rightInspectPos = new Vector3(0.236f, -0.078f, -0.016f);
        [SerializeField] private Vector3 leftInspectEuler = new Vector3(0f, 0f, -5f);
        [SerializeField] private Vector3 rightInspectEuler = new Vector3(0f, 0f, 5f);
        [SerializeField] private Vector3 inspectScale = new Vector3(0.115f, 0.115f, 0.115f);

        [Header("2. Resting on Desk / Pan-Up Pose (Unclasped & Lowered)")]
        [SerializeField] private Vector3 leftDeskRestPos = new Vector3(-0.285f, -0.195f, -0.012f);
        [SerializeField] private Vector3 rightDeskRestPos = new Vector3(0.285f, -0.195f, -0.012f);
        [SerializeField] private Vector3 leftDeskRestEuler = new Vector3(0f, 0f, 18f);
        [SerializeField] private Vector3 rightDeskRestEuler = new Vector3(0f, 0f, -18f);
        [SerializeField] private Vector3 deskRestScale = new Vector3(0.105f, 0.105f, 0.105f);

        [Header("3. Procedural Finger & Grip Micro-Sway")]
        [SerializeField] private bool enableProceduralIdle = true;
        [SerializeField] private float idleSwayFrequency = 1.85f;
        [SerializeField] private float idlePosAmplitude = 0.0045f;
        [SerializeField] private float idleGripRotAmplitude = 2.4f;

        [Header("4. Typewriter Finger-Tracking Motion")]
        [SerializeField] private float typingFingerTapFrequency = 18f;
        [SerializeField] private float typingPosAmplitude = 0.0035f;
        [SerializeField] private float typingRotAmplitude = 3.2f;

        [Header("5. Candlelight Tint")]
        [SerializeField] private Color warmCandlelightTint = new Color(0.96f, 0.90f, 0.82f, 1f);

        // Runtime state
        private FirstPersonHandsState currentState = FirstPersonHandsState.InspectingBook;
        private float currentInspectBlend = 1f; // 1 = InspectingBook, 0 = RestingOnDesk
        private bool isTypingActive = false;
        private float idleTimer = 0f;
        private float recoilOffset = 0f;
        private float recoilVelocity = 0f;

        public FirstPersonHandsState CurrentState => currentState;

        private void Awake()
        {
            EnsureHandReferences();
            ApplyRendererHygiene();
        }

        private void OnEnable()
        {
            EnsureHandReferences();
            ApplyRendererHygiene();
        }

        public void EnsureHandReferences()
        {
            if (leftHandTransform == null)
            {
                Transform t = transform.Find("Hand_Left_2D");
                if (t != null) leftHandTransform = t;
            }
            if (rightHandTransform == null)
            {
                Transform t = transform.Find("Hand_Right_2D");
                if (t != null) rightHandTransform = t;
            }

            if (leftHandTransform != null && leftHandRenderer == null)
                leftHandRenderer = leftHandTransform.GetComponent<SpriteRenderer>();
            if (rightHandTransform != null && rightHandRenderer == null)
                rightHandRenderer = rightHandTransform.GetComponent<SpriteRenderer>();
        }

        private void ApplyRendererHygiene()
        {
            int bookLayer = gameObject.layer;
            if (leftHandTransform != null) leftHandTransform.gameObject.layer = bookLayer;
            if (rightHandTransform != null) rightHandTransform.gameObject.layer = bookLayer;

            if (leftHandRenderer != null)
            {
                leftHandRenderer.sortingOrder = 28;
                leftHandRenderer.color = warmCandlelightTint;
            }
            if (rightHandRenderer != null)
            {
                rightHandRenderer.sortingOrder = 28;
                rightHandRenderer.color = warmCandlelightTint;
            }
        }

        /// <summary>
        /// Sets the blend between RestingOnDesk (0) and InspectingBook (1).
        /// Called automatically by CheckInBookViewController during camera cuts and pans.
        /// </summary>
        public void SetInspectionBlend(float bookFocusFactor01, float openTransition01 = 1f)
        {
            currentInspectBlend = Mathf.Clamp01(bookFocusFactor01);
            if (openTransition01 <= 0.05f)
                currentState = FirstPersonHandsState.LoweredOut;
            else if (currentInspectBlend >= 0.5f)
                currentState = FirstPersonHandsState.InspectingBook;
            else
                currentState = FirstPersonHandsState.RestingOnDesk;

            UpdateHandTransforms(Application.isPlaying ? Time.deltaTime : 0f);
        }

        /// <summary>
        /// Enables or disables the procedural right-hand finger tracking while the ledger is being typed.
        /// </summary>
        public void SetTypingActive(bool active)
        {
            isTypingActive = active;
        }

        /// <summary>
        /// Applies a physical downward/outward recoil impulse when a photo or stamp hits the page.
        /// </summary>
        public void TriggerImpactRecoil(float strength = 1f)
        {
            recoilVelocity -= Mathf.Clamp(strength, 0.1f, 2f) * 0.14f;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            UpdateHandTransforms(Time.deltaTime);
        }

        private void UpdateHandTransforms(float dt)
        {
            if (leftHandTransform == null && rightHandTransform == null) return;

            // Update spring recoil (stiff damped spring)
            if (dt > 0f)
            {
                float springForce = -recoilOffset * 110f - recoilVelocity * 14f;
                recoilVelocity += springForce * dt;
                recoilOffset += recoilVelocity * dt;
                if (Mathf.Abs(recoilOffset) < 0.00005f && Mathf.Abs(recoilVelocity) < 0.0005f)
                {
                    recoilOffset = 0f;
                    recoilVelocity = 0f;
                }
            }

            float smoothBlend = Mathf.SmoothStep(0f, 1f, currentInspectBlend);

            Vector3 baseLeftPos = Vector3.Lerp(leftDeskRestPos, leftInspectPos, smoothBlend);
            Vector3 baseRightPos = Vector3.Lerp(rightDeskRestPos, rightInspectPos, smoothBlend);

            Vector3 baseLeftEuler = Vector3.Lerp(leftDeskRestEuler, leftInspectEuler, smoothBlend);
            Vector3 baseRightEuler = Vector3.Lerp(rightDeskRestEuler, rightInspectEuler, smoothBlend);

            Vector3 targetScale = Vector3.Lerp(deskRestScale, inspectScale, smoothBlend);

            // Procedural idle breathing & finger-grip micro-adjustments
            Vector3 leftOffset = Vector3.zero;
            Vector3 rightOffset = Vector3.zero;
            float leftGripAngle = 0f;
            float rightGripAngle = 0f;

            if (enableProceduralIdle && dt > 0f)
            {
                idleTimer += dt * idleSwayFrequency;

                // Left hand subtle thumb/finger pressure adjustment
                float lSin = Mathf.Sin(idleTimer);
                float lCos = Mathf.Cos(idleTimer * 0.73f);
                leftOffset = new Vector3(lCos * idlePosAmplitude * 0.6f, lSin * idlePosAmplitude, 0f) * smoothBlend;
                leftGripAngle = Mathf.Sin(idleTimer * 1.15f) * idleGripRotAmplitude * smoothBlend;

                // Right hand asymmetric breathing + optional typewriter finger tap
                float rSin = Mathf.Sin(idleTimer + 1.1f);
                float rCos = Mathf.Cos(idleTimer * 0.81f + 0.6f);
                rightOffset = new Vector3(-rCos * idlePosAmplitude * 0.6f, rSin * idlePosAmplitude, 0f) * smoothBlend;
                rightGripAngle = -Mathf.Sin(idleTimer * 1.08f + 0.8f) * idleGripRotAmplitude * smoothBlend;

                if (isTypingActive && smoothBlend > 0.3f)
                {
                    float tapPhase = Time.time * typingFingerTapFrequency;
                    float tapY = Mathf.Abs(Mathf.Sin(tapPhase)) * typingPosAmplitude;
                    float tapX = Mathf.Sin(tapPhase * 0.35f) * typingPosAmplitude * 1.4f;
                    rightOffset += new Vector3(tapX, -tapY, 0f);
                    rightGripAngle += Mathf.Sin(tapPhase) * typingRotAmplitude;
                }
            }

            // Apply recoil impulse (pushes hands slightly down and outward on stamp/photo impact)
            Vector3 leftRecoil = new Vector3(recoilOffset * 0.4f, recoilOffset, 0f);
            Vector3 rightRecoil = new Vector3(-recoilOffset * 0.4f, recoilOffset, 0f);
            float recoilRot = recoilOffset * 180f;

            if (leftHandTransform != null)
            {
                leftHandTransform.localPosition = baseLeftPos + leftOffset + leftRecoil;
                leftHandTransform.localRotation = Quaternion.Euler(baseLeftEuler.x, baseLeftEuler.y, baseLeftEuler.z + leftGripAngle - recoilRot);
                leftHandTransform.localScale = targetScale;
            }

            if (rightHandTransform != null)
            {
                rightHandTransform.localPosition = baseRightPos + rightOffset + rightRecoil;
                rightHandTransform.localRotation = Quaternion.Euler(baseRightEuler.x, baseRightEuler.y, baseRightEuler.z + rightGripAngle + recoilRot);
                rightHandTransform.localScale = targetScale;
            }
        }
    }
}
