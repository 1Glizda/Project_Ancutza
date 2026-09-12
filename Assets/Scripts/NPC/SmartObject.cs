using UnityEngine;

public enum SmartObjectType
{
    Chair,
    Bench,
    Bed
}

public class SmartObject : MonoBehaviour
{
    [SerializeField] private SmartObjectType smartObjectType;
    [SerializeField] private Transform interactionAnchor;
    [SerializeField] private float minUseDuration = 10f;
    [SerializeField] private float maxUseDuration = 30f;

    public bool IsOccupied { get; private set; }
    public GameObject OccupyingNPC { get; private set; }
    public SmartObjectType ObjectType => smartObjectType;
    public Transform InteractionAnchor => interactionAnchor;

    private void Awake()
    {
        if (interactionAnchor == null)
        {
            GameObject anchorObj = new GameObject("InteractionAnchor");
            anchorObj.transform.SetParent(transform, false);
            
            Vector3 offset = Vector3.zero;
            switch (smartObjectType)
            {
                case SmartObjectType.Chair:
                case SmartObjectType.Bench:
                    offset = new Vector3(0, 0.5f, 0);
                    break;
                case SmartObjectType.Bed:
                    offset = new Vector3(0, 0.7f, 0);
                    break;
            }
            
            anchorObj.transform.localPosition = offset;
            anchorObj.transform.localRotation = Quaternion.identity;
            interactionAnchor = anchorObj.transform;
        }
    }

    public bool TryReserve(GameObject npc)
    {
        if (IsOccupied)
        {
            return false;
        }
        
        IsOccupied = true;
        OccupyingNPC = npc;
        return true;
    }

    public void Release(GameObject npc)
    {
        if (IsOccupied && OccupyingNPC == npc)
        {
            IsOccupied = false;
            OccupyingNPC = null;
        }
    }

    public float GetRandomUseDuration()
    {
        return Random.Range(minUseDuration, maxUseDuration);
    }

    public static SmartObject FindNearest(Vector3 position, SmartObjectType type, float maxRange)
    {
        SmartObject[] allObjects = FindObjectsByType<SmartObject>(FindObjectsSortMode.None);
        
        SmartObject nearest = null;
        float minDistanceSq = maxRange * maxRange;
        
        foreach (var obj in allObjects)
        {
            if (obj.smartObjectType == type && !obj.IsOccupied)
            {
                float sqrDistance = (obj.transform.position - position).sqrMagnitude;
                if (sqrDistance <= minDistanceSq)
                {
                    minDistanceSq = sqrDistance;
                    nearest = obj;
                }
            }
        }
        
        return nearest;
    }

    private void OnDrawGizmosSelected()
    {
        if (interactionAnchor != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(interactionAnchor.position, 0.2f);
        }
        else
        {
            Gizmos.color = Color.blue;
            Vector3 offset = Vector3.zero;
            switch (smartObjectType)
            {
                case SmartObjectType.Chair:
                case SmartObjectType.Bench:
                    offset = new Vector3(0, 0.5f, 0);
                    break;
                case SmartObjectType.Bed:
                    offset = new Vector3(0, 0.7f, 0);
                    break;
            }
            Gizmos.DrawSphere(transform.TransformPoint(offset), 0.2f);
        }
    }
}
