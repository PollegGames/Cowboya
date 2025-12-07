using UnityEngine;

/// <summary>
/// Issues basic <see cref="AttackRequest"/>s on behalf of an enemy when the player enters the attack zone.
/// Hitbox activation is intentionally omitted for the simplified combat flow.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyPunchAttack : MonoBehaviour
{
    [SerializeField] private FollowPlayerTriggerHandler targetToFollow;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float verticalSectorThreshold = 0.75f;
    [SerializeField] private FactoryAlarmStatus alarmStatus;

    [Header("Hostility")]
    [SerializeField] private float hostilityThreshold = -2f;
    [SerializeField] private float followerHostilityThreshold = 100f;

    private RobotStateController robotBehaviour;
    private RobotStats playerStats;
    private RobotMemory memory;
    private RobotBrain brain;
    private float lastPunchTime;
    private bool playerInAttackZone;

    private void Awake()
    {
        robotBehaviour = GetComponent<RobotStateController>();
        memory = GetComponent<RobotMemory>();
        brain = GetComponent<RobotBrain>();
        if (alarmStatus == null)
            alarmStatus = FindFirstObjectByType<FactoryManager>()?.factoryAlarmStatus;
        ConfigureThresholdByRole();
    }

    private void OnEnable()
    {
        if (targetToFollow != null)
            targetToFollow.OnPlayerDetectInAttackZoneChanged += HandlePlayerInAttackZoneChange;
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
            targetToFollow.OnPlayerDetectInAttackZoneChanged -= HandlePlayerInAttackZoneChange;
    }

    private void HandlePlayerInAttackZoneChange(bool isInside)
    {
        playerInAttackZone = isInside;
        if (brain == null)
            return;

        if (isInside)
        {
            Transform player = targetToFollow != null ? targetToFollow.PlayerTransform : null;
            brain.RequestAttackTarget(player);
        }
    }

    public bool TryBuildAttackRequest(out AttackRequest request)
    {
        request = default;

        if (!CanIssueAttack())
            return false;

        Vector3 playerPosition = targetToFollow.PlayerBodyReferencePosition;
        if (playerPosition == Vector3.zero)
            return false;

        if (Time.time < lastPunchTime + attackCooldown)
            return false;

        AttackSector sector = ResolveSector(playerPosition);
        float energyCost = robotBehaviour != null && robotBehaviour.Stats != null
            ? robotBehaviour.Stats.AttackEnergyCost
            : 0f;

        Vector2 targetPosition = new Vector2(playerPosition.x, playerPosition.y);
        request = new AttackRequest(targetPosition, sector, energyCost);
        return true;
    }

    /// <summary>
    /// Tracks cooldown when an attack is accepted. Hitbox wiring will be added with the new combat flow.
    /// </summary>
    public void HandleAttackAccepted(AttackRequest request)
    {
        lastPunchTime = Time.time;
    }

    private bool CanIssueAttack()
    {
        if (targetToFollow == null || !playerInAttackZone)
            return false;

        if (alarmStatus != null && alarmStatus.CurrentAlarmState == AlarmState.Wanted)
            return true;

        if (memory != null && memory.WasRecentlyAttacked)
            return true;

        if (playerStats == null)
            return true;

        return playerStats.Morality <= hostilityThreshold;
    }

    private AttackSector ResolveSector(Vector3 playerPosition)
    {
        Vector3 origin = transform.position;
        Vector3 delta = playerPosition - origin;

        if (Mathf.Abs(delta.y) > verticalSectorThreshold)
            return delta.y > 0f ? AttackSector.Up : AttackSector.Down;

        return delta.x >= 0f ? AttackSector.Right : AttackSector.Left;
    }

    private void ConfigureThresholdByRole()
    {
        var role = brain != null && brain.Config != null ? brain.Config.Role : RobotRole.SecurityGuard;
        if (role == RobotRole.Follower)
        {
            hostilityThreshold = followerHostilityThreshold;
        }
        else if (role == RobotRole.SecurityGuard)
        {
            hostilityThreshold = -2f;
        }
    }
}
