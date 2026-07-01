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
    private List<IRobotMemoryNew> enemiesInZone = new List<IRobotMemoryNew>();
    private HashSet<IRobotMemoryNew> alarmedMemories = new HashSet<IRobotMemoryNew>();

    private void Awake()
    {
        if (roomManager == null)
            roomManager = GetComponentInParent<RoomManager>();

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
        if (player == null || roomManager == null)
            return;

        Vector2 playerPos = player.transform.position;
        roomManager.UpdateTrackedPlayerPositionIfAlarmActive(playerPos);
    }

    private void OnPlayerEnterZone(Collider2D playerCollider)
    {
        if (roomManager == null)
            roomManager = GetComponentInParent<RoomManager>();

        if (roomManager == null)
            return;

        if (player == null && roomManager.FactoryManager != null)
            player = roomManager.FactoryManager.playerHeadTransform;

        if (player == null)
            player = roomManager.PlayerHead;

        if (player == null && playerCollider != null)
        {
            var movement = playerCollider.GetComponentInParent<PlayerMovementController>();
            if (movement != null)
                player = movement.HeadTransform;
        }

        if (player != null)
        {
            targetToFollow = player;
            isFollowing = true;
            float morality = 0f;
            var playerInstance = roomManager.FactoryManager != null
                ? roomManager.FactoryManager.playerInstance
                : null;
            var controller = playerInstance != null
                ? playerInstance.GetComponent<RobotStateController>()
                : null;
            if (controller != null && controller.Stats != null)
                morality = controller.Stats.Morality;

            if (morality <= -10f)
            {
                roomManager.RaiseRoomThreat(
                    AlarmState.Wanted,
                    RoomThreatSource.SecurityCamera,
                    player.position);
            }

            Vector2 playerPos = player.transform.position;
            roomManager.UpdateLastKnownPlayerPosition(playerPos);
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

        if (player != null && roomManager != null)
        {
            Vector2 playerPos = player.transform.position;
            roomManager.UpdateLastKnownPlayerPosition(playerPos);
        }
    }

    private void OnSecondaryZoneEnter(Collider2D enemyCollider)
    {
        if (roomManager == null || enemyCollider == null)
            return;

        var brain = enemyCollider.GetComponentInParent<RobotBrainNew>();
        var mem = brain != null ? brain.Memory as IRobotMemoryNew : enemyCollider.GetComponentInParent<IRobotMemoryNew>();
        if (mem != null && !enemiesInZone.Contains(mem))
            enemiesInZone.Add(mem);
        if (mem != null && mem.WasRecentlyAttacked)
        {
            if (mem.LastKnownPlayerPosition != Vector3.zero)
                roomManager.RaiseRoomThreat(AlarmState.Wanted, RoomThreatSource.SecurityCamera, mem.LastKnownPlayerPosition);
            else
                roomManager.RaiseRoomThreat(AlarmState.Wanted, RoomThreatSource.SecurityCamera);
        }
    }

    private void CheckEnemiesAttackedInZone()
    {
        if (roomManager == null || roomManager.CurrentRoomAlarmState == AlarmState.Wanted)
            return;

        foreach (var mem in enemiesInZone)
        {
            if (mem.WasRecentlyAttacked && !alarmedMemories.Contains(mem))
            {
                Vector3 alarmPos = mem.LastKnownPlayerPosition;
                if (alarmPos == Vector3.zero && roomManager != null)
                {
                    var factoryPlayer = roomManager.FactoryManager != null
                        ? roomManager.FactoryManager.playerHeadTransform
                        : null;
                    var fallbackPlayer = factoryPlayer != null ? factoryPlayer : roomManager.PlayerHead;
                    if (fallbackPlayer != null)
                        alarmPos = fallbackPlayer.position;
                }

                if (alarmPos != Vector3.zero)
                {
                    roomManager.RaiseRoomThreat(AlarmState.Wanted, RoomThreatSource.SecurityCamera, alarmPos);
                }
                else
                {
                    roomManager.RaiseRoomThreat(AlarmState.Wanted, RoomThreatSource.SecurityCamera);
                }

                alarmedMemories.Add(mem);
                break;
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

