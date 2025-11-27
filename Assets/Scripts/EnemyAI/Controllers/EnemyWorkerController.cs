using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controls enemy worker navigation using waypoint-based pathing.
/// </summary>
[RequireComponent(typeof(WorkerStateMachine), typeof(RobotMemory))]
public class EnemyWorkerController : AnimatorBaseAgentController, IRobotDecisionProvider
{
    [SerializeField] public WorkerStateMachine stateMachine;
    [SerializeField] private RobotMemory memoryComponent;
    [SerializeField] private RobotStateController robotBehaviour;

    private IWorkerStateMachine stateMachineInterface;
    public IRobotMemory memory { get; protected set; }
    private WaypointPathFollower pathFollower;
    private IWaypointQueries waypointQueries;
    public IWaypointService waypointService;
    private Action stuckHandler;


    [SerializeField] private float arrivalThresholdX = 2f;
    [SerializeField] private float arrivalThresholdY = 2f;
    [SerializeField] private float deadZoneX = 5f;
    [SerializeField] private float deadZoneY = 5f;
    [SerializeField] private UpdateLoop updateLoop = UpdateLoop.Update;
    [SerializeField] private Inventory inventory;
    private BatteryPickup initialBattery;
    private BatterySpawner batterySpawner;
    private Transform dropContainer;


    [SerializeField] private LowMoralityPlayerTriggerHandler lowMoralityTriggerHandler;
    [SerializeField] private AllyWorkerController allyWorkerController;
    private FactoryMachine currentMachine;

    public WorkerStatus workerState { get; set; } = WorkerStatus.Idle;

    public bool IsWorkerSpawner { get; private set; }

    protected override void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        base.Awake();

        if (stateMachine == null)
            stateMachine = GetComponent<WorkerStateMachine>();
        if (memoryComponent == null)
            memoryComponent = GetComponent<RobotMemory>();
        memory = memoryComponent;

        if (robotBehaviour == null)
            robotBehaviour = GetComponent<RobotStateController>();
        if (lowMoralityTriggerHandler == null)
            lowMoralityTriggerHandler = GetComponent<LowMoralityPlayerTriggerHandler>();
        if (allyWorkerController == null)
            allyWorkerController = GetComponent<AllyWorkerController>();

        if (robotBehaviour != null)
            robotBehaviour.OnStateChanged += HandleStateChange;
        if (lowMoralityTriggerHandler != null)
            lowMoralityTriggerHandler.OnLowMoralityPlayerDetected += HandleLowMoralityPlayerDetected;
        if (allyWorkerController != null)
            allyWorkerController.enabled = false;
    }

    public void Initialize(IWaypointQueries waypointQueries, IWaypointService waypointService,
        IRobotRespawnService respawnService,
        Transform dropContainer,
        BatterySpawner batterySpawner = null,
        bool spawnInitialPickups = true)
    {
        this.waypointQueries = waypointQueries;
        this.waypointService = waypointService;
        this.dropContainer = dropContainer;
        this.batterySpawner = batterySpawner;

        if (pathFollower == null)
            SetupPathFollower();

        waypointService.Subscribe(pathFollower);
        memory.SetRespawnService(respawnService);
        stateMachine.ChangeState(new Worker_Idle(this, stateMachine, (IWaypointService)waypointQueries));

        if (spawnInitialPickups && this.batterySpawner != null && initialBattery == null)
        {
            initialBattery = this.batterySpawner.SpawnBattery(bodyReference);

            if (initialBattery != null && inventory != null)
            {
                initialBattery.AssignInventory(inventory);
                inventory.SetItem(PickupType.Battery, initialBattery);

                if (robotBehaviour != null)
                    robotBehaviour.Stats.UpdateHealth(10f);
            }
        }
    }

    private void Update()
    {
        if (updateLoop == UpdateLoop.Update)
            pathFollower?.Update(Time.deltaTime);
    }

    protected override void FixedUpdate()
    {
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
        request = default;
        return false;
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
        memory.OnStuck(this);
    }

    public void SetWorkerSpawnerState()
    {
        IsWorkerSpawner = true;
    }

    /// <summary>
    /// Sets a waypoint destination for the enemy worker.
    /// </summary>
    public virtual void SetDestination(RoomWaypoint target, bool includeUnavailable = false) =>
        pathFollower?.SetDestination(target, includeUnavailable);



    public void OnBatteryStolen(GameObject player, float healthGain)
    {
        Debug.Log($"{name} battery stolen by {player.name}");
        robotBehaviour.Stats.UpdateEnergy(robotBehaviour.Stats.CurrentEnergy);
        robotBehaviour.Stats.UpdateHealth(-healthGain);
    }

    /// <summary>
    /// Removes the stolen battery from the inventory and clears the initial reference.
    /// </summary>
    public void HandleBatteryStolen()
    {
        if (inventory != null)
            inventory.RemoveItem(PickupType.Battery);

        initialBattery = null;
    }

    private void DropBossLoot()
    {
        if (initialBattery != null)
        {
            if (inventory != null && (object)inventory.GetItem(PickupType.Battery) == initialBattery)
            {
                initialBattery.OnRelease(Vector2.zero);
                if (dropContainer != null)
                    initialBattery.transform.SetParent(dropContainer, true);
            }
            initialBattery = null;
        }
    }

    public virtual bool HasArrivedAtDestination() => pathFollower.HasArrived;

    public void OnPathObsoleted(RoomWaypoint blockedWaypoint) =>
        pathFollower.OnPathObsoleted(blockedWaypoint);

    public RoomWaypoint GetClosestWaypoint(RoomWaypoint exclude = null) =>
        pathFollower.GetClosestWaypoint(exclude);

    public void SetCurrentMachine(FactoryMachine machine) => currentMachine = machine;

    public void ClearCurrentMachine() => currentMachine = null;

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
        var jointBreaker = GetComponent<JointBreaker>();
        jointBreaker?.BreakAll();
        SceneController.instance.RobotKilled();

        DropBossLoot();
        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(5f);
        ObjectPool.Instance.Release(gameObject);
    }

    /// <summary>
    /// Converts this worker from an enemy into an ally.
    /// </summary>
    public void ConvertToAlly()
    {
        enabled = false;
        if (stateMachine != null)
            stateMachine.enabled = false;
        if (lowMoralityTriggerHandler != null)
            lowMoralityTriggerHandler.enabled = false;
        if (robotBehaviour != null)
            robotBehaviour.enabled = false;
        if (memoryComponent != null)
            memoryComponent.enabled = false;
        var punchAttack = GetComponent<EnemyPunchAttack>();
        if (punchAttack != null)
            punchAttack.enabled = false;
        var followHandler = GetComponent<FollowPlayerTriggerHandler>();
        if (followHandler != null)
            followHandler.enabled = false;
    }

    private void UpdateBalance(bool enabledBalance)
    {
        var bodyBalance = GetComponent<BodyBalance>();
        if (bodyBalance != null)
        {
            bodyBalance.UpdateBalance(enabledBalance);
        }
    }

    private void HandleLowMoralityPlayerDetected(Transform player)
    {
        var previousState = stateMachine.enemyState;
        var machine = currentMachine;
        stateMachine.ChangeState(new Worker_FleePlayer(this, stateMachine, waypointService, previousState, player, machine));
        currentMachine = null;
    }

    private void OnDrawGizmos()
    {
        pathFollower?.DrawGizmos();
    }

    /// <summary>
    /// Cleans up listeners and references when returned to the pool.
    /// </summary>
    public void OnReleaseToPool()
    {
        if (pathFollower != null)
        {
            pathFollower.OnStuck -= stuckHandler;
            waypointService?.Unsubscribe(pathFollower);
        }
        pathFollower = null;
        stuckHandler = null;
    }

    /// <summary>
    /// Reinitializes state after the worker is pulled from the pool.
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

