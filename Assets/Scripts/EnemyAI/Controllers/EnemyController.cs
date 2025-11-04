using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(EnemyStateMachine), typeof(RobotMemory), typeof(Inventory))]
/// <summary>
/// Controls enemy behaviour and state transitions. Initializes path following
/// and badge spawning services and provides APIs to change state or assign
/// destinations.
/// </summary>
public class EnemyController : AnimatorBaseAgentController, IPooledObject, IRobotDecisionProvider
{
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private RobotMemory memoryComponent;
    // Uses bodyReference from AnimatorBaseAgentController
    [SerializeField] private BodyJointLimiter bodyJointLimiter;

    private IEnemyStateMachine stateMachineInterface;
    public IRobotMemory memory { get; private set; }
    private WaypointPathFollower pathFollower;
    private IWaypointQueries waypointQueries;
    private IWaypointNotifier waypointNotifier;
    private Action stuckHandler;

    [SerializeField] private RobotStateController robotBehaviour;
    [SerializeField] private float arrivalThresholdX = 2f;
    [SerializeField] private float arrivalThresholdY = 2f;
    [SerializeField] private float deadZoneX = 5f;
    [SerializeField] private float deadZoneY = 5f;

    public Transform BodyReference => bodyReference;

    [SerializeField] private EnemyPunchAttack punchAttack;
    [SerializeField] private AttackRequestController attackRequestController;
    [SerializeField] private Inventory inventory;

    private FactoryAlarmStatus alarmStatus;

    [SerializeField] private UpdateLoop updateLoop = UpdateLoop.Update;
    public EnemyStatus EnemyStatus { get; set; } = EnemyStatus.Idle;
    [field: SerializeField] public bool IsBoss { get; private set; }

    private SecurityBadgePickup initialBadge;
    private SecurityBadgeSpawner securityBadgeSpawner;

    private Transform dropContainer;

    protected override void Awake()
    {
        // Ensure animator is assigned for the animator-driven movement controller
        animator = GetComponentInChildren<Animator>();
        if (bodyJointLimiter == null)
            bodyJointLimiter = GetComponent<BodyJointLimiter>();
        base.Awake();
        if (stateMachine == null)
            stateMachine = GetComponent<EnemyStateMachine>();
        stateMachineInterface = stateMachine;

        if (memoryComponent == null)
            memoryComponent = GetComponent<RobotMemory>();
        memory = memoryComponent;

        if (robotBehaviour == null)
            robotBehaviour = GetComponent<RobotStateController>();

        if (punchAttack == null)
            punchAttack = GetComponent<EnemyPunchAttack>();

        robotBehaviour.OnStateChanged += HandleStateChange;

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (attackRequestController == null)
        {
            attackRequestController = GetComponent<AttackRequestController>();
            if (attackRequestController == null)
                attackRequestController = GetComponentInChildren<AttackRequestController>();
        }
    }

    public void Initialize(
        IWaypointQueries waypointQueries,
        IWaypointNotifier waypointNotifier,
        IRobotRespawnService respawnService,
        Transform dropContainer,
        SecurityBadgeSpawner securityBadgeSpawner,
        bool spawnInitialPickups = true)
    {
        this.waypointQueries = waypointQueries;
        this.waypointNotifier = waypointNotifier;
        if (pathFollower == null)
            SetupPathFollower();
        if (this.waypointNotifier != null && pathFollower != null)
            this.waypointNotifier.Subscribe(pathFollower);

        if (memory == null)
        {
            if (memoryComponent == null)
                memoryComponent = GetComponent<RobotMemory>();
            memory = memoryComponent != null ? memoryComponent : GetComponent<RobotMemory>();
        }

        if (memory != null && respawnService != null)
            memory.SetRespawnService(respawnService);

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        this.dropContainer = dropContainer;
        this.securityBadgeSpawner = securityBadgeSpawner;

        if (spawnInitialPickups)
        {
            if (securityBadgeSpawner && initialBadge == null)
            {
                var badgeParent = bodyReference != null ? bodyReference : transform;
                initialBadge = securityBadgeSpawner.SpawnBadge(badgeParent);
                if (inventory != null && initialBadge != null)
                {
                    initialBadge.AssignInventory(inventory);
                    inventory.SetItem(PickupType.SecurityBadge, initialBadge);
                }
            }
        }
    }

    private void SetupPathFollower()
    {
        pathFollower = new WaypointPathFollower(bodyReference, this, waypointQueries,
            arrivalThresholdX, arrivalThresholdY, deadZoneX, deadZoneY);
        stuckHandler = HandlePathFollowerStuck;
        pathFollower.OnStuck += stuckHandler;
    }

    private void HandlePathFollowerStuck()
    {
        memory.OnBossStuck(this);
    }

    public void SetSecurityGuardState()
    {
        IsBoss = false;
        stateMachine.ChangeState(new Enemy_ReturnToSecurityPost(this, stateMachine, (IWaypointService)waypointQueries, null));
    }

    public void SetBossState()
    {
        IsBoss = true;
        stateMachine.ChangeState(new Enemy_Idle(this, stateMachine, (IWaypointService)waypointQueries));
    }

    public void SetFollowerState(FactoryAlarmStatus factoryAlarmStatus)
    {
        IsBoss = false;
        alarmStatus = factoryAlarmStatus;
        stateMachine.ChangeState(new Enemy_Follower(this, stateMachine, (IWaypointService)waypointQueries, alarmStatus));
    }

    private void Update()
    {
        TryFlip(direction);
        if (updateLoop == UpdateLoop.Update)
            pathFollower?.Update(Time.deltaTime);

        ProcessAttackDecisions();
    }

    protected override void FixedUpdate()
    {
        // Drive animator-based locomotion from the base controller
        base.FixedUpdate();
        if (updateLoop == UpdateLoop.FixedUpdate)
            pathFollower?.Update(Time.fixedDeltaTime);
    }

    /// <inheritdoc />
    public Vector2 Movement => new Vector2(direction, verticalDirection);

    /// <inheritdoc />
    public Vector2 DesiredFacing => LookDirection;

    /// <inheritdoc />
    public bool TryBuildAttackRequest(out AttackRequest request)
    {
        if (punchAttack != null && punchAttack.TryBuildAttackRequest(out request))
            return true;

        request = default;
        return false;
    }

    private void ProcessAttackDecisions()
    {
        if (attackRequestController == null)
            return;

        if (TryBuildAttackRequest(out AttackRequest request))
        {
            bool accepted = attackRequestController.TryHandleAttack(request);
            if (accepted)
            {
                punchAttack?.HandleAttackAccepted(request);
            }
        }
    }


    public void SetDestination(RoomWaypoint target, bool includeUnavailable = false) =>
        pathFollower.SetDestination(target, includeUnavailable);

    public bool HasArrivedAtDestination() => pathFollower.HasArrived;

    public void OnPathObsoleted(RoomWaypoint blockedWaypoint) =>
        pathFollower.OnPathObsoleted(blockedWaypoint);

    public RoomWaypoint GetClosestWaypoint(RoomWaypoint exclude = null) =>
        pathFollower.GetClosestWaypoint(exclude);

    private void HandleStateChange(RobotState newState)
    {
        switch (newState)
        {
            case RobotState.Faint:
                UpdateBalance(false);
                break;
            case RobotState.Dead:
                Die();
                break;
            case RobotState.Alive:
                UpdateBalance(true);
                break;
        }
    }

    public void Faint()
    {
        robotBehaviour.UpdateState(RobotState.Faint);
    }

    public void Die()
    {
        if (bodyJointLimiter != null)
            bodyJointLimiter.enabled = false;
        var jointBreaker = GetComponent<JointBreaker>();
        jointBreaker?.BreakAll();
        SceneController.instance.RobotKilled();
        DropBossLoot();
        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(10f);
        ObjectPool.Instance.Release(gameObject);
    }

    private void DropBossLoot()
    {
        if (initialBadge != null)
        {
            if (inventory != null && (object)inventory.GetItem(PickupType.SecurityBadge) == initialBadge)
            {
                initialBadge.OnRelease(Vector2.zero);
                if (dropContainer != null)
                    initialBadge.transform.SetParent(dropContainer, true);
            }
            initialBadge = null;
        }


        if (IsBoss)
        {
            var spawnParent = transform;
            if (securityBadgeSpawner != null)
            {
                var badge = securityBadgeSpawner.SpawnBadge(spawnParent);
                badge?.OnRelease(Vector2.zero);
                if (dropContainer != null && badge != null)
                    badge.transform.SetParent(dropContainer, true);
            }

        }
    }

    public void OnBadgeStolen(GameObject player)
    {
        var thiefName = player != null ? player.name : "unknown";
        Debug.Log($"{name} badge stolen by {thiefName}");

        if (EnemyStatus != EnemyStatus.Following)
        {
            if (alarmStatus != null && waypointQueries != null)
            {
                SetFollowerState(alarmStatus);
            }
            else
            {
                EnemyStatus = EnemyStatus.Following;
            }
        }
    }

    /// <summary>
    /// Handles cleanup when this enemy's security badge has been stolen.
    /// Removes the badge from the inventory and clears the reference so a new
    /// badge will be spawned on the next initialization.
    /// </summary>
    public void HandleBadgeStolen()
    {
        if (inventory != null)
            inventory.RemoveItem(PickupType.SecurityBadge);

        initialBadge = null;
    }



    private void UpdateBalance(bool enabledBalance)
    {
        var bodyBalance = GetComponent<BodyBalance>();
        if (bodyBalance != null)
        {
            bodyBalance.UpdateBalance(enabledBalance);
        }
    }

    private void OnDrawGizmos()
    {
        pathFollower?.DrawGizmos();
    }

    /// <summary>
    /// Cleans up listeners and releases references when returned to the pool.
    /// </summary>
    public void OnReleaseToPool()
    {
        if (pathFollower != null)
        {
            pathFollower.OnStuck -= stuckHandler;
            waypointNotifier?.Unsubscribe(pathFollower);
        }
        pathFollower = null;
        stuckHandler = null;
        IsBoss = false;
    }

    /// <summary>
    /// Reinitializes required fields after being pulled from the pool.
    /// </summary>
    public void OnAcquireFromPool()
    {
        var jointBreaker = GetComponent<JointBreaker>();
        jointBreaker?.RestoreAll();

        if (pathFollower == null && waypointQueries != null)
        {
            SetupPathFollower();
        }
    }
}
