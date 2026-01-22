using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SecurityCamera : MonoBehaviour
{
    [Header("Settings")]
    public Color rayColor = Color.red;
    public float rotationSpeed = 360f;
    public float spriteAngleOffset = 0f;

    [Header("Zone Detection")]
    public PositionTriggerZone playerFollowTriggerZone;
    public PositionTriggerZone detectAggresionZone;

    [Header("References (Auto-assigned)")]
    [SerializeField]
    private Transform cameraHead;
    private Transform player;
    private bool isFollowing;
    private Transform targetToFollow;

    [Header("Room & Player References")]
    public RoomManager roomManager;
    [Header("Minimap Update")]
    [SerializeField] private float minimapRefreshInterval = 2f;
    private Coroutine minimapRoutine;
    private GameUIViewModel minimapView;
    private Vector3 lastPlayerPosition;

    private List<IRobotMemory> enemiesInZone = new List<IRobotMemory>();
    private HashSet<IRobotMemory> alarmedMemories = new HashSet<IRobotMemory>();

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

        if (detectAggresionZone != null)
        {
            detectAggresionZone.onEnter.AddListener(OnSecondaryZoneEnter);
        }

        minimapView = FindFirstObjectByType<GameUIViewModel>();
    }

    private void Update()
    {
        if (isFollowing && targetToFollow != null && cameraHead != null)
            RotateHeadTowardsTarget();
        if (enemiesInZone.Count > 0)
            CheckEnemiesAttackedInZone();
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
        UpdateWantedPlayerPosition();
    }

    private void UpdateWantedPlayerPosition()
    {
        var factoryAlarm = roomManager?.FactoryManager?.factoryAlarmStatus;
        if (factoryAlarm != null && factoryAlarm.CurrentAlarmState != AlarmState.Normal)
        {
            Vector2 playerPos = player.transform.position;
            roomManager.waypointService.UpdateClosestWaypointToPlayer(playerPos);
        }
    }

    private void OnPlayerEnterZone(Collider2D playerCollider)
    {
        if (player == null && roomManager.FactoryManager != null)
            player = roomManager.FactoryManager.playerHeadTransform;

        if (player == null)
            player = roomManager.PlayerHead;

        if (player != null)
        {
            targetToFollow = player;
            isFollowing = true;
            var controller = roomManager.FactoryManager.playerInstance
                .GetComponent<RobotStateController>();
            float morality = controller.Stats.Morality;
            if (morality <= -10f)
            {
                var factoryAlarm = roomManager.FactoryManager.factoryAlarmStatus;
                factoryAlarm.CurrentAlarmState = AlarmState.Wanted;
                factoryAlarm.LastPlayerPosition = player.position;
            }

            Vector2 playerPos = player.transform.position;
            roomManager.waypointService.UpdateClosestWaypointToPlayer(playerPos);
            lastPlayerPosition = targetToFollow.position;
            if (minimapRoutine != null)
                StopCoroutine(minimapRoutine);
            minimapRoutine = StartCoroutine(RefreshMinimapWhilePlayerMoving());
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
        if (minimapRoutine != null)
            StopCoroutine(minimapRoutine);
    }

    private IEnumerator RefreshMinimapWhilePlayerMoving()
    {
        while (isFollowing && targetToFollow != null)
        {
            Vector3 currentPos = targetToFollow.position;
            if ((currentPos - lastPlayerPosition).sqrMagnitude > 0.01f)
            {
                minimapView?.RefreshMinimapTexture();
                UpdateWantedPlayerPosition();
                lastPlayerPosition = currentPos;
            }
            yield return new WaitForSeconds(minimapRefreshInterval);
        }
    }

    private void OnSecondaryZoneEnter(Collider2D enemyCollider)
    {
        var factoryAlarm = roomManager?.FactoryManager?.factoryAlarmStatus;
        if (factoryAlarm != null)
        {
            var brain = enemyCollider.GetComponentInParent<RobotBrain>();
            var mem = brain != null ? brain.Memory as IRobotMemory : enemyCollider.GetComponentInParent<IRobotMemory>();
            if (mem != null && !enemiesInZone.Contains(mem))
                enemiesInZone.Add(mem);
            if (mem != null && mem.WasRecentlyAttacked)
            {
                factoryAlarm.CurrentAlarmState = AlarmState.Wanted;
                factoryAlarm.LastPlayerPosition = mem.LastKnownPlayerPosition;
            }
        }
    }

    private void CheckEnemiesAttackedInZone()
    {
        var factoryAlarm = roomManager?.FactoryManager?.factoryAlarmStatus;
        if (factoryAlarm == null || factoryAlarm.CurrentAlarmState == AlarmState.Wanted) return;

        foreach (var mem in enemiesInZone)
        {
            if (mem.WasRecentlyAttacked && !alarmedMemories.Contains(mem))
            {
                factoryAlarm.CurrentAlarmState = AlarmState.Wanted;
                alarmedMemories.Add(mem);
                break;
            }
        }
    }

    private void OnDestroy()
    {
        if (minimapRoutine != null)
            StopCoroutine(minimapRoutine);
        if (playerFollowTriggerZone != null)
        {
            playerFollowTriggerZone.onEnter.RemoveListener(OnPlayerEnterZone);
            playerFollowTriggerZone.onExit.RemoveListener(OnPlayerExitZone);
        }

        if (detectAggresionZone != null)
        {
            detectAggresionZone.onEnter.RemoveListener(OnSecondaryZoneEnter);
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
