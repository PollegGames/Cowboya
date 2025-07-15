using UnityEngine;

public class SecurityCamera : MonoBehaviour
{
    [Header("Settings")]
    public Color rayColor = Color.red;
    public float rotationSpeed = 360f;
    public float spriteAngleOffset = 0f;

    [Header("Zone Detection")]
    public PositionTriggerZone playerFollowTriggerZone;
    public PositionTriggerZone rescueEnemyZone;

    [Header("Rescue Settings")]
    public bool reportRescueToFactory = false;

    [Header("References (Auto-assigned)")]
    [SerializeField]
    private Transform cameraHead;

    private Transform player;
    private bool isFollowing;
    private Transform targetToFollow;

    [Header("Room & Player References")]
    public RoomManager roomManager;

    private void Awake()
    {
        if (cameraHead == null && transform.childCount > 0)
            cameraHead = transform.GetChild(0);

        if (cameraHead == null)
            Debug.LogError($"[{name}] SecurityCamera: no cameraHead assigned or found.");
    }

    private void Start()
    {
        if (playerFollowTriggerZone != null)
        {
            playerFollowTriggerZone.onEnter.AddListener(OnPlayerEnterZone);
            playerFollowTriggerZone.onExit.AddListener(OnPlayerExitZone);
        }

        if (rescueEnemyZone != null)
        {
            rescueEnemyZone.onEnter.AddListener(OnSecondaryZoneEnter);
        }
    }

    private void Update()
    {
        if (isFollowing && targetToFollow != null && cameraHead != null)
            RotateHeadTowardsTarget();
    }

    private void RotateHeadTowardsTarget()
    {
        Vector2 dir = (Vector2)targetToFollow.position - (Vector2)cameraHead.position;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + spriteAngleOffset;
        Quaternion goal = Quaternion.Euler(0, 0, angle);

        cameraHead.rotation = Quaternion.RotateTowards(
            cameraHead.rotation,
            goal,
            rotationSpeed * Time.deltaTime
        );
    }

    private void OnPlayerEnterZone(Collider2D playerCollider)
    {
        if (player == null)
            player = roomManager.FactoryManager.playerHeadTransform;

        if (player != null)
        {
            targetToFollow = player;
            isFollowing = true;

            Vector2 playerPos = player.transform.position;
            roomManager.waypointService.UpdateClosestWaypointToPlayer(playerPos);
        }
        else
        {
            Debug.LogWarning($"[{name}] OnPlayerEnterZone fired but player==null.");
        }
    }

    private void OnPlayerExitZone()
    {
        isFollowing = false;
        targetToFollow = null;

        Vector2 playerPos = player.transform.position;
        roomManager.waypointService.UpdateClosestWaypointToPlayer(playerPos);
    }

    private void OnSecondaryZoneEnter(Collider2D enemyCollider)
    {
        // 1) Make sure we have a FactoryAlarmStatus to update
        var factoryAlarm = roomManager?.FactoryManager?.factoryAlarmStatus;
        if (factoryAlarm != null)
        {
            // 2) Try get the EnemyController (and its memory) from this collider
            var ec = enemyCollider.GetComponent<EnemyController>();
            if (ec != null)
            {
                var memory = ec.memory;
                if (memory != null && memory.WasRecentlyAttacked)
                {
                    // 3) If they were recently attacked, raise the alarm
                    factoryAlarm.CurrentAlarmState = AlarmState.Wanted;
                    factoryAlarm.LastPlayerPosition = memory.LastKnownPlayerPosition;
                }
            }

            // 4) (Optional) your existing rescue-report logic
            if (reportRescueToFactory)
            {
                SceneController.instance.RobotSaved();
            }
        }
    }

    private void OnDestroy()
    {
        if (playerFollowTriggerZone != null)
        {
            playerFollowTriggerZone.onEnter.RemoveListener(OnPlayerEnterZone);
            playerFollowTriggerZone.onExit.RemoveListener(OnPlayerExitZone);
        }

        if (rescueEnemyZone != null)
        {
            rescueEnemyZone.onEnter.RemoveListener(OnSecondaryZoneEnter);
        }
    }

    private void OnDrawGizmos()
    {
        if (isFollowing && targetToFollow != null && cameraHead != null)
        {
            Gizmos.color = rayColor;
            Gizmos.DrawLine(cameraHead.position, targetToFollow.position);
        }
    }
}
