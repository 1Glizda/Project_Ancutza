using UnityEngine;
using UnityEngine.Events;

public enum BillboardMode
{
    Single,
    Directional4,
    Directional8
}

public class BillboardSprite : MonoBehaviour
{
    [SerializeField] private float verticalOffset = 0f;
    [SerializeField] private Vector3 pivotOffset = Vector3.zero;
    [SerializeField] private float lyingVerticalOffset = 0.05f;
    [SerializeField] private BillboardMode billboardMode = BillboardMode.Single;
    [SerializeField] private UnityEvent<int> onDirectionChanged;

    private Camera mainCamera;
    private bool isSitting = false;
    private float sitOffsetValue = 0f;
    private bool isLying = false;
    private Transform myTransform;
    private int lastDirectionIndex = -1;
    private Vector3 initialLocalPosition;

    public bool IsLying => isLying;

    private void Awake()
    {
        if (myTransform == null)
            myTransform = transform;
        if (myTransform.localPosition != Vector3.zero && myTransform.localPosition.y > 0.5f)
            initialLocalPosition = myTransform.localPosition;
        else
            initialLocalPosition = new Vector3(0f, 1.5f, 0f);

        if (!isLying)
        {
            myTransform.localPosition = initialLocalPosition + pivotOffset + new Vector3(0f, verticalOffset, 0f);
            myTransform.localRotation = Quaternion.identity;
        }
    }

    private void OnValidate()
    {
        if (!isLying && (transform.localRotation == Quaternion.Euler(90f, 180f, 0f) || transform.localPosition.y < 0.5f))
        {
            transform.localRotation = Quaternion.identity;
            transform.localPosition = new Vector3(0f, 1.5f, 0f);
        }
    }

    private void OnEnable()
    {
        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (isLying)
        {
            // When lying down on a bed, the sprite lays flat horizontally on the mattress (XZ plane),
            // with head toward the headboard and feet toward the footboard, aligned with the bed.
            myTransform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            myTransform.localPosition = new Vector3(0f, lyingVerticalOffset, 0f);
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
                return;
        }

        Vector3 camPos = mainCamera.transform.position;
        Vector3 myPos = myTransform.position;

        Vector3 directionToCamera = camPos - myPos;
        directionToCamera.y = 0f;

        if (directionToCamera.sqrMagnitude > 0.001f)
        {
            directionToCamera.Normalize();
            myTransform.rotation = Quaternion.LookRotation(directionToCamera);
        }

        float currentVerticalOffset = verticalOffset;
        if (isSitting)
        {
            currentVerticalOffset += sitOffsetValue;
        }

        myTransform.localPosition = initialLocalPosition + pivotOffset + new Vector3(0f, currentVerticalOffset, 0f);

        UpdateDirectionalSprite();
    }

    private void UpdateDirectionalSprite()
    {
        if (billboardMode == BillboardMode.Single)
            return;

        // Logical forward should be the parent's forward. If no parent, assume world forward.
        Vector3 logicalForward = myTransform.parent != null ? myTransform.parent.forward : Vector3.forward;
        logicalForward.y = 0f;
        if (logicalForward.sqrMagnitude < 0.001f)
            logicalForward = Vector3.forward;
        else
            logicalForward.Normalize();

        Vector3 directionToCamera = mainCamera.transform.position - myTransform.position;
        directionToCamera.y = 0f;
        if (directionToCamera.sqrMagnitude < 0.001f)
            directionToCamera = Vector3.forward;
        else
            directionToCamera.Normalize();

        float angle = Vector3.SignedAngle(logicalForward, directionToCamera, Vector3.up);
        if (angle < 0f)
            angle += 360f;

        int index = 0;
        if (billboardMode == BillboardMode.Directional4)
        {
            float step = 90f;
            float halfStep = step / 2f;
            index = (int)(((angle + halfStep) % 360f) / step);
        }
        else if (billboardMode == BillboardMode.Directional8)
        {
            float step = 45f;
            float halfStep = step / 2f;
            index = (int)(((angle + halfStep) % 360f) / step);
        }

        if (index != lastDirectionIndex)
        {
            lastDirectionIndex = index;
            onDirectionChanged?.Invoke(index);
        }
    }

    public void SetSitMode(bool sitting, float sitOffset)
    {
        isSitting = sitting;
        sitOffsetValue = sitOffset;
    }

    public void SetLyingMode(bool lying)
    {
        isLying = lying;
        if (myTransform == null)
            myTransform = transform;

        if (initialLocalPosition == Vector3.zero || initialLocalPosition.y < 0.5f)
        {
            if (myTransform.localPosition != Vector3.zero && myTransform.localPosition.y > 0.5f)
                initialLocalPosition = myTransform.localPosition;
            else
                initialLocalPosition = new Vector3(0f, 1.5f, 0f);
        }

        if (isLying)
        {
            myTransform.localRotation = Quaternion.Euler(90f, 180f, 0f);
            myTransform.localPosition = new Vector3(0f, lyingVerticalOffset, 0f);
        }
        else
        {
            myTransform.localPosition = initialLocalPosition + pivotOffset + new Vector3(0f, verticalOffset, 0f);
            myTransform.localRotation = Quaternion.identity;
        }
    }
}
