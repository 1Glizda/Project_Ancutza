using UnityEngine;
using UnityEngine.InputSystem;

namespace AZE.AdvancedFirstPerson
{
    public class PlayerCameraController : MonoBehaviour
    {
        [Header("Settings")]
        [Range(0f, 100f)] public float SensitivityX = 20f;
        [Range(0f, 100f)] public float SensitivityY = 15f;
        public float TopClamp = -90f;
        public float BottomClamp = 75f;

        [Header("References")]
        public Transform CameraTransform;
        [SerializeField] private PlayerInputHandler inputHandler;

        private float _cameraPitch = 0f;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void LateUpdate()
        {
            HandleRotation();
        }

        private void HandleRotation()
        {
            Vector2 lookInput = inputHandler.LookInput;
            if (lookInput.sqrMagnitude < 0.0001f) return;

            var device = inputHandler.GetLookDevice;

            float multiplier = 1.0f;

            if (device is Mouse)
            {
                multiplier = 0.01f;
            }
            else if (device is Gamepad)
            {
                multiplier = Time.deltaTime;
            }
            else
            {
                multiplier = Time.deltaTime;
            }

            float yaw = lookInput.x * SensitivityX * multiplier;
            transform.Rotate(Vector3.up * yaw);

            float pitch = lookInput.y * SensitivityY * multiplier;
            _cameraPitch -= pitch;
            _cameraPitch = Mathf.Clamp(_cameraPitch, TopClamp, BottomClamp);

            CameraTransform.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        }
    }
}