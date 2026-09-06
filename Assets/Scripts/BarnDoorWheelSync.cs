using UnityEngine;

[ExecuteAlways]
public class BarnDoorWheelSync : MonoBehaviour
{
    [Header("Target References")]
    [Tooltip("The movable transform whose X position determines the wheel rotation. If left empty, auto-detects.")]
    [SerializeField] private Transform movableTransform;

    [Tooltip("First wheel to rotate on Z (Hanger/Wheel.01). If left empty, auto-detects.")]
    [SerializeField] private Transform wheel01;

    [Tooltip("Second wheel to rotate on Z (Hanger/Wheel.02). If left empty, auto-detects.")]
    [SerializeField] private Transform wheel02;

    [Header("Coordinate Settings")]
    [Tooltip("Use localPosition.x (true) or world position.x (false). Default is true.")]
    [SerializeField] private bool useLocalPosition = true;

    [Header("Rotation Settings")]
    [Tooltip("Distance moved on X for degreesPerDistance rotation (default: 0.1).")]
    [SerializeField] private float distancePerRotation = 0.1f;

    [Tooltip("Degrees rotated around Z for each distancePerRotation on X (default: 360).")]
    [SerializeField] private float degreesPerDistance = 360f;

    [Header("State / Calibration")]
    [Tooltip("Reference X position corresponding to 0 rotation offset.")]
    [SerializeField] private float referenceX;
    [SerializeField] private bool hasReference = false;

    [SerializeField] private Vector3 wheel01InitialEuler;
    [SerializeField] private Vector3 wheel02InitialEuler;

    private void Awake()
    {
        InitializeReferences();
    }

    private void OnEnable()
    {
        InitializeReferences();
    }

    private void Start()
    {
        InitializeReferences();
    }

    [ContextMenu("Calibrate Reference Position")]
    public void CalibrateReference()
    {
        if (movableTransform != null)
        {
            referenceX = useLocalPosition ? movableTransform.localPosition.x : movableTransform.position.x;
            hasReference = true;
        }

        if (wheel01 != null) wheel01InitialEuler = wheel01.localEulerAngles;
        if (wheel02 != null) wheel02InitialEuler = wheel02.localEulerAngles;
    }

    public void InitializeReferences()
    {
        // 1. Locate movableTransform
        if (movableTransform == null)
        {
            if (name.Equals("_movable", System.StringComparison.OrdinalIgnoreCase))
            {
                movableTransform = transform;
            }
            else
            {
                Transform found = transform.Find("WholeBarnDoor/_movable");
                if (found == null) found = transform.Find("_movable");
                if (found == null)
                {
                    foreach (var t in GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.Equals("_movable", System.StringComparison.OrdinalIgnoreCase))
                        {
                            found = t;
                            break;
                        }
                    }
                }
                movableTransform = found != null ? found : transform;
            }
        }

        // 2. Locate wheels under movableTransform
        if (movableTransform != null)
        {
            if (wheel01 == null)
            {
                Transform w1 = movableTransform.Find("Hanger/Wheel.01");
                if (w1 == null) w1 = movableTransform.Find("hanger/wheel.01");
                if (w1 == null)
                {
                    foreach (var t in movableTransform.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.Equals("Wheel.01", System.StringComparison.OrdinalIgnoreCase))
                        {
                            w1 = t;
                            break;
                        }
                    }
                }
                wheel01 = w1;
            }

            if (wheel02 == null)
            {
                Transform w2 = movableTransform.Find("Hanger/Wheel.02");
                if (w2 == null) w2 = movableTransform.Find("hanger/wheel.02");
                if (w2 == null)
                {
                    foreach (var t in movableTransform.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name.Equals("Wheel.02", System.StringComparison.OrdinalIgnoreCase))
                        {
                            w2 = t;
                            break;
                        }
                    }
                }
                wheel02 = w2;
            }
        }

        // 3. Initialize reference position & angles if not already calibrated
        if (!hasReference && movableTransform != null)
        {
            referenceX = useLocalPosition ? movableTransform.localPosition.x : movableTransform.position.x;
            hasReference = true;
            if (wheel01 != null) wheel01InitialEuler = wheel01.localEulerAngles;
            if (wheel02 != null) wheel02InitialEuler = wheel02.localEulerAngles;
        }
    }

    private void Update()
    {
        SyncWheels();
    }

    private void LateUpdate()
    {
        SyncWheels();
    }

    public void SyncWheels()
    {
        if (movableTransform == null || distancePerRotation == 0f) return;
        if (wheel01 == null && wheel02 == null) return;

        if (!hasReference)
        {
            CalibrateReference();
        }

        float currentX = useLocalPosition ? movableTransform.localPosition.x : movableTransform.position.x;
        float deltaX = currentX - referenceX;

        // When movable moves on x with +0.1, rotate +360 degrees on Z.
        // For each -0.1 move on x, rotate with -360 degrees.
        float rotationDeltaZ = (deltaX / distancePerRotation) * degreesPerDistance;

        if (wheel01 != null)
        {
            wheel01.localRotation = Quaternion.Euler(
                wheel01InitialEuler.x,
                wheel01InitialEuler.y,
                wheel01InitialEuler.z + rotationDeltaZ
            );
        }

        if (wheel02 != null)
        {
            wheel02.localRotation = Quaternion.Euler(
                wheel02InitialEuler.x,
                wheel02InitialEuler.y,
                wheel02InitialEuler.z + rotationDeltaZ
            );
        }
    }

    private void OnValidate()
    {
        InitializeReferences();
        SyncWheels();
    }
}
