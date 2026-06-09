using UnityEngine;
using Unity.Cinemachine;

namespace AZE.AdvancedFirstPerson
{
    public class CameraEffects : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMovementStateMachine playerMovement;
        [SerializeField] private CinemachineCamera cinemachineCamera;

        [Header("Bob Settings")]
        [SerializeField] private bool useHeadBob = true;
        [Range(0f, 5f)] [SerializeField] private float moveAmplitude = 1.5f;
        [Range(0f, 10f)] [SerializeField] private float moveFrequency = 2.5f;
        [Range(0.1f, 20f)] [SerializeField] private float bobSmoothing = 5f;
        private float idleAmplitude;
        private float idleFrequency;

        [Header("Tilt Settings")]
        [SerializeField] private bool useTilt = true;
        [Range(0f, 10f)] [SerializeField] private float maxTiltAngle = 1.5f;
        [Range(0.1f, 20f)] [SerializeField] private float tiltSmoothing = 8f;

        [Header("FOV Settings")]
        [SerializeField] private bool useFovKick = true;
        [Range(0f, 20f)] [SerializeField] private float fovBoostAmount = 10f;
        [Range(0.1f, 20f)] [SerializeField] private float fovSmoothing = 4f;

        private CinemachineBasicMultiChannelPerlin _cameraNoise;
        private float _baseFOV;

        private void Awake()
        {
            if (cinemachineCamera == null)
                cinemachineCamera = GetComponent<CinemachineCamera>();

            if (cinemachineCamera != null)
            {
                _cameraNoise = cinemachineCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
                idleAmplitude = _cameraNoise.AmplitudeGain;
                idleFrequency = _cameraNoise.FrequencyGain;
                _baseFOV = cinemachineCamera.Lens.FieldOfView;
            }

            if (cinemachineCamera != null)
            {
                playerMovement = GetComponentInParent<PlayerMovementStateMachine>();
            }

            if (playerMovement == null)
            {
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (playerMovement == null) return;

            float speedPercent = playerMovement.CurrentSpeedPercentage;
            float inputX = playerMovement.InputHandler.MoveInput.x;

            HandleBob(speedPercent);
            HandleTilt(speedPercent, inputX);
            HandleFOV(speedPercent);
        }

        private void HandleBob(float speedPercent)
        {
            if (!useHeadBob) return;
            if (_cameraNoise == null) return;

            float targetAmp = Mathf.Lerp(idleAmplitude, moveAmplitude, speedPercent);
            float targetFreq = Mathf.Lerp(idleFrequency, moveFrequency, speedPercent);

            _cameraNoise.AmplitudeGain = Mathf.Lerp(_cameraNoise.AmplitudeGain, targetAmp, bobSmoothing * Time.deltaTime);
            _cameraNoise.FrequencyGain = Mathf.Lerp(_cameraNoise.FrequencyGain, targetFreq, bobSmoothing * Time.deltaTime);
        }

        private void HandleTilt(float speedPercent, float inputX)
        {
            if (!useTilt) return;
            if (cinemachineCamera == null) return;

            float targetTilt = -inputX * maxTiltAngle * speedPercent;

            var lens = cinemachineCamera.Lens;

            lens.Dutch = Mathf.Lerp(lens.Dutch, targetTilt, tiltSmoothing * Time.deltaTime);

            cinemachineCamera.Lens = lens;
        }

        private void HandleFOV(float speedPercent)
        {
            if (!useFovKick) return;
            if (cinemachineCamera == null) return;

            float targetFOV = _baseFOV + (fovBoostAmount * speedPercent);

            var lens = cinemachineCamera.Lens;

            lens.FieldOfView = Mathf.Lerp(lens.FieldOfView, targetFOV, fovSmoothing * Time.deltaTime);

            cinemachineCamera.Lens = lens;
        }
    }
}