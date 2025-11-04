using UnityEngine;

/// <summary>
/// Issues <see cref="AttackRequest"/>s on behalf of an enemy when the player
/// enters the attack zone.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPunchAttack : MonoBehaviour
{
    [SerializeField] private FollowPlayerTriggerHandler targetToFollow;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float verticalSectorThreshold = 0.75f;
    [SerializeField] private FactoryAlarmStatus alarmStatus;

    private RobotStateController robotBehaviour;
    private RobotStats playerStats;
    private float lastPunchTime;
    private bool playerInAttackZone;

    private void Awake()
    {
        robotBehaviour = GetComponent<RobotStateController>();
        if (alarmStatus == null)
        {
            alarmStatus = FindFirstObjectByType<FactoryManager>()?.factoryAlarmStatus;
        }
    }

    private void OnEnable()
    {
        if (targetToFollow != null)
        {
            targetToFollow.OnPlayerDetectInAttackZoneChanged += HandlePlayerInAttackZoneChange;
        }
    }

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            RobotStateController controller = player.GetComponent<RobotStateController>();
            playerStats = controller?.Stats;
        }
    }

    private void OnDisable()
    {
        if (targetToFollow != null)
        {
            targetToFollow.OnPlayerDetectInAttackZoneChanged -= HandlePlayerInAttackZoneChange;
        }
    }

    private void HandlePlayerInAttackZoneChange(bool isInside)
    {
        playerInAttackZone = isInside;
    }

    public bool TryBuildAttackRequest(out AttackRequest request)
    {
        request = default;

        if (!CanIssueAttack())
        {
            return false;
        }

        Vector3 playerPosition = targetToFollow.PlayerBodyReferencePosition;
        if (playerPosition == Vector3.zero)
        {
            return false;
        }

        if (Time.time < lastPunchTime + attackCooldown)
        {
            return false;
        }

        AttackSector sector = ResolveSector(playerPosition);
        float energyCost = robotBehaviour != null && robotBehaviour.Stats != null
            ? robotBehaviour.Stats.AttackEnergyCost
            : 0f;

        Vector2 targetPosition = new Vector2(playerPosition.x, playerPosition.y);
        request = new AttackRequest(targetPosition, sector, energyCost);
        lastPunchTime = Time.time;
        return true;
    }

    private bool CanIssueAttack()
    {
        if (targetToFollow == null || !playerInAttackZone)
        {
            return false;
        }

        if (playerStats != null &&
            playerStats.Morality > 5f &&
            alarmStatus != null &&
            alarmStatus.CurrentAlarmState != AlarmState.Wanted)
        {
            return false;
        }

        return true;
    }

    private AttackSector ResolveSector(Vector3 playerPosition)
    {
        Vector3 origin = transform.position;
        Vector3 delta = playerPosition - origin;

        if (Mathf.Abs(delta.y) > verticalSectorThreshold)
        {
            return delta.y > 0f ? AttackSector.Up : AttackSector.Down;
        }

        return delta.x >= 0f ? AttackSector.Right : AttackSector.Left;
    }
}
