namespace EasyDoorSystem
{
    using UnityEngine;
    using UnityEngine.Events;

    [RequireComponent(typeof(AudioSource))]
    public class EasyDoor : MonoBehaviour
    {
        public enum MovementType { Rotation, Position, Both }

        [Header("Door Settings")]
        [SerializeField] private MovementType movementType = MovementType.Rotation;
        [SerializeField] private float movementSpeed = 2f;
        [SerializeField] private float rotationSpeed = 2f;
        [Tooltip("Animation curve used for smooth easing (Ease-In-Out).")]
        [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Automatically close after specified time (0 = no auto-close)")]
        [SerializeField] private float autoCloseDelay = 0f;
        [SerializeField] public bool automaticPlayerDetection = false;
        [SerializeField] private float detectionRange = 3f;

        [Header("Transform Targets")]
        [SerializeField] private Vector3 closedRotation;
        [SerializeField] private Vector3 openedRotation;
        [SerializeField] private Vector3 closedPosition;
        [SerializeField] private Vector3 openedPosition;

        [Header("Audio")]
        [SerializeField] private AudioClip doorOpenSound;
        [SerializeField] private AudioClip doorCloseSound;
        [SerializeField][Range(0, 1)] private float audioVolume = 0.8f;

        [Header("Events")]
        public UnityEvent OnDoorOpening;
        public UnityEvent OnDoorClosed;

        public bool IsOpen { get; private set; }
        public bool IsMoving { get; private set; }

        private AudioSource audioSource;
        private Transform playerTransform;
        private Coroutine movementCoroutine;

        public GameObject Player;
        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound by default
            audioSource.playOnAwake = false;

            if (automaticPlayerDetection)
                FindPlayer();
        }

        private void FindPlayer()
        {
            if (Player) playerTransform = Player.transform;
            //     Player = GameObject.FindGameObjectWithTag("Player"); playerTransform = Player.transform; // ENABLE FI YOU WANT TO FIND PLAYER WITH TAG
        }

        private void Update()
        {
            if (!automaticPlayerDetection || !playerTransform) return;

            float distance = Vector3.Distance(transform.position, playerTransform.position);
            if (distance <= detectionRange && !IsOpen)
            {
                OpenDoor();
            }
            else if (distance > detectionRange && IsOpen)
            {
                CloseDoor();
            }
        }

        public void ToggleDoor()
        {
            if (IsOpen) CloseDoor();
            else OpenDoor();
        }

        public void OpenDoor()
        {
            if (IsOpen || IsMoving) return;

            MoveDoor(openedPosition, openedRotation, true);
            PlaySound(doorOpenSound);
            OnDoorOpening.Invoke();
        }

        public void CloseDoor()
        {
            if (!IsOpen || IsMoving) return;

            MoveDoor(closedPosition, closedRotation, false);
            PlaySound(doorCloseSound);
            OnDoorClosed.Invoke();
        }

        private void MoveDoor(Vector3 targetPosition, Vector3 targetRotation, bool opening)
        {
            if (movementCoroutine != null)
                StopCoroutine(movementCoroutine);

            movementCoroutine = StartCoroutine(AnimateDoor(
                movementType != MovementType.Rotation ? targetPosition : transform.localPosition,
                movementType != MovementType.Position ? targetRotation : transform.localEulerAngles,
                opening
            ));
        }

        private System.Collections.IEnumerator AnimateDoor(Vector3 targetPos, Vector3 targetRot, bool opening)
        {
            IsMoving = true;
            Quaternion startRot = transform.localRotation;
            Vector3 startPos = transform.localPosition;
            Quaternion targetQuaternion = Quaternion.Euler(targetRot);

            float speed = movementSpeed;
            if (movementType == MovementType.Rotation)
                speed = rotationSpeed;
            else if (movementType == MovementType.Both)
                speed = Mathf.Max(movementSpeed, rotationSpeed);

            float duration = Mathf.Max(0.05f, 1f / Mathf.Max(0.01f, speed));

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                // Smooth ease-in-out evaluation (custom curve or high-quality smootherstep)
                float smoothProgress = (movementCurve != null && movementCurve.length >= 2)
                    ? movementCurve.Evaluate(progress)
                    : (progress * progress * progress * (progress * (progress * 6f - 15f) + 10f));

                if (movementType != MovementType.Position)
                {
                    transform.localRotation = Quaternion.Slerp(startRot, targetQuaternion, smoothProgress);
                }

                if (movementType != MovementType.Rotation)
                {
                    transform.localPosition = Vector3.Lerp(startPos, targetPos, smoothProgress);
                }

                yield return null;
            }

            // Ensure final positions are exact
            if (movementType != MovementType.Position)
                transform.localRotation = targetQuaternion;

            if (movementType != MovementType.Rotation)
                transform.localPosition = targetPos;

            IsOpen = opening;
            IsMoving = false;

            if (autoCloseDelay > 0 && IsOpen)
                Invoke(nameof(CloseDoor), autoCloseDelay);
        }

        private void PlaySound(AudioClip clip)
        {
            if (!clip || !audioSource) return;

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.volume = audioVolume;
            audioSource.Play();
        }

        public void SaveCurrentState(bool saveAsOpen)
        {
            if (saveAsOpen)
            {
                openedRotation = transform.localEulerAngles;
                openedPosition = transform.localPosition;
            }
            else
            {
                closedRotation = transform.localEulerAngles;
                closedPosition = transform.localPosition;
            }
        }

        // Gizmos for detection visualization
        private void OnDrawGizmosSelected()
        {
            if (automaticPlayerDetection)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(transform.position, detectionRange);
            }
        }
    }
}