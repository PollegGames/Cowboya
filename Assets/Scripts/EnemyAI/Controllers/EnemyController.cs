using UnityEngine;
using System.Collections;

[RequireComponent(typeof(EnemyStateMachine), typeof(RobotMemory))]
/// <summary>
/// Controls enemy behaviour and state transitions. Initializes path following
/// and badge spawning services and provides APIs to change state or assign
/// destinations.
/// </summary>
public class EnemyController : PhysicsBaseAgentController
{
    [SerializeField] private EnemyStateMachine stateMachine;
    [SerializeField] private RobotMemory memoryComponent;

    private IEnemyStateMachine stateMachineInterface;
    public IRobotMemory memory { get; private set; }
    private WaypointPathFollower pathFollower;
    private IWaypointQueries waypointQueries;
    private IWaypointNotifier waypointNotifier;

    [SerializeField] private RobotStateController robotBehaviour;
    [SerializeField] private float arrivalThresholdX = 2f;
    [SerializeField] private float arrivalThresholdY = 2f;
    [SerializeField] private float deadZoneX = 5f;
    [SerializeField] private float deadZoneY = 5f;

    [SerializeField] private Transform bodyReference;
    public Transform BodyReference => bodyReference;

    [SerializeField] private EnemyPunchAttack punchAttack;

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
        stateMachine.ChangeState(new Enemy_Idle(this, stateMachine, null));

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
        pathFollower = new WaypointPathFollower(bodyReference, this, waypointQueries,
            arrivalThresholdX, arrivalThresholdY, deadZoneX, deadZoneY);
        pathFollower.OnStuck += () => memory.OnBossStuck(this);
        waypointNotifier.Subscribe(pathFollower);
        memory.SetRespawnService(respawnService);
        this.dropContainer = dropContainer;

        if (securityBadgeSpawner)
        {
            if (initialBadge != null)
                Destroy(initialBadge.gameObject);
            initialBadge = securityBadgeSpawner.SpawnBadge(bodyReference);
        }
    }

    public void SetSecurityGuardState()
    {
        stateMachine.ChangeState(new Enemy_Idle(this, stateMachine, (IWaypointService)waypointQueries));
    }

    public void SetFollowerState(FactoryAlarmStatus factoryAlarmStatus)
    {
        stateMachine.ChangeState(new Enemy_Follower(this, stateMachine, (IWaypointService)waypointQueries, factoryAlarmStatus));
    }

    private void Update()
    {
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
            initialBadge.transform.SetParent(dropContainer, true);
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
}
