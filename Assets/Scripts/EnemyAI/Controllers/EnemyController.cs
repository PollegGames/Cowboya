using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(EnemyStateMachine), typeof(RobotMemory))]
/// <summary>
/// Controls enemy behaviour and state transitions. Initializes path following
/// and badge spawning services and provides APIs to change state or assign
/// destinations.
/// </summary>
public class EnemyController : PhysicsBaseAgentController, IPooledObject
{
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private RobotMemory memoryComponent;
    [SerializeField] private Transform bodyReference;

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

    private FactoryAlarmStatus alarmStatus;

    [SerializeField] private UpdateLoop updateLoop = UpdateLoop.Update;
    public EnemyStatus EnemyStatus { get; set; } = EnemyStatus.Idle;

    private SecurityBadgePickup initialBadge;

    private Transform dropContainer;

    protected override void Awake()
    {
        base.Awake();
        if (stateMachine == null)
            stateMachine = GetComponent<EnemyStateMachine>();
        stateMachineInterface = stateMachine;

        if (memoryComponent == null)
            memoryComponent = GetComponent<RobotMemory>();
        memory = memoryComponent;

        if (robotBehaviour == null)
            robotBehaviour = GetComponent<RobotStateController>();

        robotBehaviour.OnStateChanged += HandleStateChange;
    }

    public void Initialize(
        IWaypointQueries waypointQueries,
        IWaypointNotifier waypointNotifier,
        IRobotRespawnService respawnService,
        Transform dropContainer,
        SecurityBadgeSpawner securityBadgeSpawner)
    {
        this.waypointQueries = waypointQueries;
        this.waypointNotifier = waypointNotifier;
        if (pathFollower == null)
            SetupPathFollower();
        waypointNotifier.Subscribe(pathFollower);
        memory.SetRespawnService(respawnService);
        this.dropContainer = dropContainer;

        if (securityBadgeSpawner && initialBadge == null)
        {
            initialBadge = securityBadgeSpawner.SpawnBadge(bodyReference);
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
        stateMachine.ChangeState(new Enemy_ReturnToSecurityPost(this, stateMachine, (IWaypointService)waypointQueries, null));
    }

    public void SetBossState()
    {
        stateMachine.ChangeState(new Enemy_Idle(this, stateMachine, (IWaypointService)waypointQueries));
    }

    public void SetFollowerState(FactoryAlarmStatus factoryAlarmStatus)
    {
        alarmStatus = factoryAlarmStatus;
        stateMachine.ChangeState(new Enemy_Follower(this, stateMachine, (IWaypointService)waypointQueries, alarmStatus));
    }

    private void Update()
    {
        TryFlip(direction);
        if (updateLoop == UpdateLoop.Update)
            pathFollower?.Update(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (updateLoop == UpdateLoop.FixedUpdate)
            pathFollower?.Update(Time.fixedDeltaTime);
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
                Faint();
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
        UpdateBalance(false);
    }

    public void Die()
    {
        if (bodyJointLimiter != null)
            bodyJointLimiter.enabled = false;
        var jointBreaker = GetComponent<JointBreaker>();
        jointBreaker?.BreakAll();
        SceneController.instance.RobotKilled();
        DetachHeldBadges();
        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(10f);
        ObjectPool.Instance.Release(gameObject);
    }

    private void DetachHeldBadges()
    {
        if (initialBadge == null) return;

        initialBadge.OnRelease(Vector2.zero);

        if (dropContainer != null)
        {
            initialBadge.transform.SetParent(dropContainer, true);
        }
        initialBadge = null;
    }

    public void OnBadgeStolen(GameObject player)
    {
        Debug.Log($"{name} badge stolen by {player.name}");
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
    }

    /// <summary>
    /// Reinitializes required fields after being pulled from the pool.
    /// </summary>
    public void OnAcquireFromPool()
    {
        if (pathFollower == null && waypointQueries != null)
        {
            SetupPathFollower();
        }
    }
}
