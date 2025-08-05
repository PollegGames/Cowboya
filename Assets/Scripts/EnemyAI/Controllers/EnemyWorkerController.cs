using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(WorkerStateMachine), typeof(RobotMemory))]
public class EnemyWorkerController : AnimatorBaseAgentController, IPooledObject
{
    [SerializeField] public WorkerStateMachine stateMachine;
    [SerializeField] private RobotMemory memoryComponent;

    private IWorkerStateMachine stateMachineInterface;
    public IRobotMemory memory { get; private set; }
    private WaypointPathFollower pathFollower;
    private IWaypointQueries waypointQueries;
    public IWaypointService waypointService;
    private Action stuckHandler;

    [SerializeField] private RobotStateController robotBehaviour;
    [SerializeField] private float arrivalThresholdX = 2f;
    [SerializeField] private float arrivalThresholdY = 2f;
    [SerializeField] private float deadZoneX = 5f;
    [SerializeField] private float deadZoneY = 5f;

    [SerializeField] private LowMoralityPlayerTriggerHandler lowMoralityTriggerHandler;
    [SerializeField] private AllyWorkerController allyWorkerController;
    private FactoryMachine currentMachine;

    public WorkerStatus workerState { get; set; } = WorkerStatus.Idle;

    public bool IsWorkerSpawner { get; private set; }

    [SerializeField] private UpdateLoop updateLoop = UpdateLoop.Update;

    protected override void Awake()
    {
        if (stateMachine == null)
            stateMachine = GetComponent<WorkerStateMachine>();
        stateMachineInterface = stateMachine;

        if (memoryComponent == null)
            memoryComponent = GetComponent<RobotMemory>();
        memory = memoryComponent;

        animator = GetComponentInChildren<Animator>();

        robotBehaviour.OnStateChanged += HandleStateChange;
        if (lowMoralityTriggerHandler != null)
            lowMoralityTriggerHandler.OnLowMoralityPlayerDetected += HandleLowMoralityPlayerDetected;
        if (allyWorkerController != null)
            allyWorkerController.enabled = false;
    }

    public void Initialize(IWaypointQueries waypointQueries, IWaypointService waypointService, IRobotRespawnService respawnService)
    {
        this.waypointQueries = waypointQueries;
        this.waypointService = waypointService;
        if (pathFollower == null)
            SetupPathFollower();
        waypointService.Subscribe(pathFollower);
        memory.SetRespawnService(respawnService);
        stateMachine.ChangeState(new Worker_Idle(this, stateMachine, (IWaypointService)waypointQueries));
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
        allyWorkerController.Initialize(waypointService);
        allyWorkerController.enabled = true;
    }

    protected override void Update()
    {
        base.Update();
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

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(5f);
        ObjectPool.Instance.Release(gameObject);
    }

    private void DisableAnimator()
    {
        if (animator != null)
        {
            animator.enabled = false;
        }
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
        if (pathFollower == null && waypointQueries != null)
        {
            SetupPathFollower();
        }
    }
}
