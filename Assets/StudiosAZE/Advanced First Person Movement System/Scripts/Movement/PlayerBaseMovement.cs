using UnityEngine;

namespace AZE.AdvancedFirstPerson
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerMovementStateMachine : MonoBehaviour
    {
        [Header("Speed Settings")]
        [Range(1f, 10f)] public float WalkSpeed = 4.5f;
        [Range(5f, 20f)] public float RunSpeed = 7f;
        [Range(1f, 5f)] public float CrouchSpeed = 2.5f;
        [Range(0f, 30f)] public float MovementSmoothing = 15f;

        [Header("Physics Settings")]
        public float Gravity = -15f;
        [HideInInspector] public float InitialFallVelocity = -3f;

        [Header("Jump Settings")]
        public bool useJump = true;
        [Range(1f, 10f)] public float JumpForce = 5f;
        [Range(0.01f, 0.5f)] public float CoyoteTime = 0.2f;
        [Range(0.01f, 0.5f)] public float JumpBufferTime = 0.2f;

        [Header("Crouch Settings")]
        public bool useCrouch = true;
        [Range(0f, 20f)] public float CrouchTransitionSpeed = 10f;
        public float CameraOffset = 0.15f;

        [Header("Dodge Settings")]
        public bool useDodge = true;
        [Range(10f, 30f)] public float DodgeSpeed = 15f;
        [Range(0f, 1f)] public float DodgeDuration = 0.3f;
        [Range(0f, 5f)] public float DodgeCooldown = 3f;

        [Header("References")]
        public Transform CameraTransform;


        public PlayerInputHandler InputHandler { get; private set; }
        public CharacterController Controller { get; private set; }

        public float CoyoteTimeCounter { get; set; }
        public float JumpBufferCounter { get; set; }


        private PlayerBaseState _currentState;
        private PlayerStateFactory _states;


        [HideInInspector] public Vector3 CurrentMoveVelocity;
        [HideInInspector] public float VerticalVelocity;

        public bool IsGrounded { get; private set; }
        public float LastDodgeTime { get; set; } = -10f;


        private float _standingHeight;
        private float _crouchHeight;
        public float TargetHeight { get; set; }

        public float CurrentSpeedPercentage
        {
            get
            {
                if (Controller == null) return 0f;
                Vector3 horizontalVel = new Vector3(Controller.velocity.x, 0, Controller.velocity.z);
                return Mathf.Clamp01(horizontalVel.magnitude / RunSpeed);
            }
        }

        private void Awake()
        {
            InputHandler = GetComponent<PlayerInputHandler>();
            Controller = GetComponent<CharacterController>();
            _states = new PlayerStateFactory(this);

            _standingHeight = Controller.height;
            _crouchHeight = _standingHeight / 2f;
            TargetHeight = _standingHeight;

            _currentState = _states.Idle;
            _currentState.EnterState();
        }

        private void Update()
        {
            IsGrounded = Controller.isGrounded;

            if (InputHandler.JumpTriggered)
            {
                JumpBufferCounter = JumpBufferTime;
            }
            else
            {
                JumpBufferCounter -= Time.deltaTime;
            }

            if (IsGrounded)
            {
                CoyoteTimeCounter = CoyoteTime;
            }
            else
            {
                CoyoteTimeCounter -= Time.deltaTime;
            }

            _currentState.UpdateState();

            ApplyFinalMovement();
            HandleHeightInterpolation();
        }

        public void SwitchState(PlayerBaseState newState)
        {
            _currentState.ExitState();
            _currentState = newState;
            _currentState.EnterState();
        }

        public void HandleGravity()
        {
            if (IsGrounded && VerticalVelocity < 0)
            {
                VerticalVelocity = InitialFallVelocity;
            }
            VerticalVelocity += Gravity * Time.deltaTime;
        }

        public void HandleMovement(float speed)
        {
            Vector2 input = InputHandler.MoveInput;
            Vector3 moveDir = (transform.right * input.x + transform.forward * input.y).normalized;

            float targetSpeed = (input.magnitude < 0.1f) ? 0f : speed;

            Vector3 targetVel = moveDir * targetSpeed;
            CurrentMoveVelocity = Vector3.Lerp(CurrentMoveVelocity, targetVel, MovementSmoothing * Time.deltaTime);
        }

        private void ApplyFinalMovement()
        {
            Vector3 finalMove = CurrentMoveVelocity;
            finalMove.y = VerticalVelocity;

            Controller.Move(finalMove * Time.deltaTime);
        }

        private void HandleHeightInterpolation()
        {
            float currentH = Controller.height;
            if (Mathf.Abs(currentH - TargetHeight) < 0.05f)
            {
                if (currentH != TargetHeight)
                {
                    Controller.height = TargetHeight;
                    Controller.center = Vector3.up * (TargetHeight * 0.5f);
                    Vector3 camPos = CameraTransform.localPosition;
                    camPos.y = TargetHeight - CameraOffset;
                    CameraTransform.localPosition = camPos;
                }
                return;
            }

            float newH = Mathf.Lerp(currentH, TargetHeight, CrouchTransitionSpeed * Time.deltaTime);
            Controller.height = newH;
            Controller.center = Vector3.up * (newH * 0.5f);

            Vector3 targetCamPos = CameraTransform.localPosition;
            targetCamPos.y = TargetHeight - CameraOffset;
            CameraTransform.localPosition = Vector3.Lerp(CameraTransform.localPosition, targetCamPos, CrouchTransitionSpeed * Time.deltaTime);
        }

        public bool CanDodge()
        {
            if (Time.time >= LastDodgeTime + DodgeCooldown && IsGrounded && !InputHandler.CrouchTriggered)
            {
                return true;
            }
            else
            {
                InputHandler.UseDodge();
                return false;
            }
        }

        public bool CanStandUp()
        {
            float radius = Controller.radius * 0.7f;
            Vector3 castOrigin = transform.position + Vector3.up * (_crouchHeight - radius + 0.05f);
            float distanceToCheck = _standingHeight - _crouchHeight;
            bool hitCeiling = Physics.SphereCast(castOrigin, radius, Vector3.up, out RaycastHit hit, distanceToCheck);
            return !hitCeiling;
        }

        public float GetStandingHeight() => _standingHeight;
        public float GetCrouchHeight() => _crouchHeight;
    }
}