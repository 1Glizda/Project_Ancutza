using UnityEngine;
using UnityEngine.AI;
using EasyDoorSystem;

[RequireComponent(typeof(EasyDoor))]
public class DoorNavMeshObstacle : MonoBehaviour
{
    [SerializeField] private NavMeshObstacle obstacle;
    [SerializeField] private Vector3 obstacleSize = new Vector3(1.6f, 3.0f, 0.4f);
    [SerializeField] private Vector3 obstacleCenter = Vector3.zero;

    private EasyDoor door;

    private void Awake()
    {
        door = GetComponent<EasyDoor>();
        EnsureObstacle();
        UpdateObstacleState();
    }

    private void OnEnable()
    {
        UpdateObstacleState();
    }

    private void Start()
    {
        UpdateObstacleState();
    }

    private void Update()
    {
        UpdateObstacleState();
    }

    public void EnsureObstacle()
    {
        if (obstacle == null)
        {
            obstacle = GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = gameObject.AddComponent<NavMeshObstacle>();
            }
        }

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;

        // If obstacle already has properly configured size, keep it
        if (obstacle.size != Vector3.one && obstacle.size.y > 1.5f)
        {
            return;
        }

        Transform barnWood = transform.Find("Barn wood");
        BoxCollider col = barnWood != null ? barnWood.GetComponent<BoxCollider>() : (GetComponent<BoxCollider>() ?? GetComponentInChildren<BoxCollider>());
        if (col != null)
        {
            Vector3 localCenter = transform.InverseTransformPoint(col.bounds.center);
            obstacle.center = localCenter;
            obstacle.size = new Vector3(Mathf.Max(col.bounds.size.x, 0.6f), col.bounds.size.y, Mathf.Max(col.bounds.size.z, 0.6f));
        }
        else
        {
            obstacle.center = obstacleCenter;
            obstacle.size = obstacleSize;
        }
    }

    private void UpdateObstacleState()
    {
        if (door == null || obstacle == null) return;

        // When door is moving or open, allow passage (disable obstacle)
        // When door is closed, block passage (enable obstacle)
        bool shouldBlock = !door.IsOpen && !door.IsMoving;

        if (obstacle.enabled != shouldBlock)
        {
            obstacle.enabled = shouldBlock;
        }
    }
}
