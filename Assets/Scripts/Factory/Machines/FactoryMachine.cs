using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public sealed class FactoryMachine : BaseMachine
{
    [Header("Visuals")]
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    [Header("Conveyor Settings")]
    [SerializeField] private CubeConveyorController cubeConveyorController;
    [SerializeField] private FactoryAlarmStatus factoryAlarmStatus;
    [Min(0f)]
    [SerializeField] private float spawnCooldown = 1f;

    private MeshRenderer meshRenderer;
    private float lastSpawnTime = -Mathf.Infinity;
    private bool cubeActive;
    private RobotBrainNew currentWorker;
    private RobotStateController currentWorkerState;

    private const string SpawnMethodName = nameof(SpawnCubeIfPossible);

    public bool HasWorker => currentWorker != null;
    public RobotBrainNew CurrentWorker => currentWorker;

    protected override void Awake()
    {
        base.Awake();

        meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
        SubscribeCoreEvents();
        SubscribeConveyorEvents();
    }

    private void OnEnable() => SubscribeAlarmEvents();

    private void OnDisable()
    {
        UnsubscribeAlarmEvents();
        StopConveyorAndCancelSpawn();
    }

    private void OnDestroy()
    {
        UnsubscribeAlarmEvents();
        UnsubscribeCoreEvents();
        UnsubscribeConveyorEvents();
        UnsubscribeFromWorkerState();
    }

    public override void PowerOn()
    {
        base.PowerOn();
        ApplyMaterial();

        TryScheduleSpawn();
    }

    public override void PowerOff()
    {
        if (!isOn) return;

        if (isOccupied)
            ReleaseRobot();

        base.PowerOff();

        ApplyMaterial();

        ResetWorkerTracking();
        StopConveyorAndCancelSpawn();
    }

    /// <summary>
    /// Called when a worker arrives at this machine.
    /// The machine only validates/updates occupancy and emits machine events.
    /// </summary>
    public override void AttachRobot(GameObject robot)
    {
        if (robot == null) return;

        var newWorker = robot.GetComponent<RobotBrainNew>();
        if (newWorker == null) return;

        if (!CanAcceptWorker(newWorker)) return;
        if (ReferenceEquals(newWorker, currentWorker)) return;

        TrackWorker(newWorker);
        base.AttachRobot(robot);

        TryScheduleSpawn();
    }

    public override void ReleaseRobot()
    {
        if (!isOccupied && currentWorker == null)
            return;

        UnsubscribeFromWorkerState();
        base.ReleaseRobot();
        ResetWorkerTracking();
        StopConveyorAndCancelSpawn();
    }

    public void ReleaseWorker(RobotBrainNew worker)
    {
        if (worker == null) return;
        if (!ReferenceEquals(worker, currentWorker)) return;

        ReleaseRobot();
    }

    public override bool TryAttachWorker(RobotBrainNew worker, string reason)
    {
        _ = reason;
        if (worker == null || !isOn)
            return false;
        if (ReferenceEquals(currentWorker, worker))
            return false;
        if (currentWorker != null)
            return false;
        if (!CanAcceptWorker(worker))
            return false;

        AttachRobot(worker.gameObject);
        NotifyWorkerAttached(worker, this);
        return true;
    }

    public override bool TryReplaceWorker(RobotBrainNew incoming, string reason)
    {
        if (incoming == null || !isOn)
            return false;
        if (ReferenceEquals(currentWorker, incoming))
            return false;
        if (currentWorker == null)
            return TryAttachWorker(incoming, reason);

        var previous = currentWorker;
        ReplaceWorkerInPlace(incoming);
        NotifyWorkerAttached(incoming, this);
        NotifyWorkerReleased(previous, this, "replaced");
        return true;
    }

    public override bool TryReleaseWorker(RobotBrainNew worker, string reason)
    {
        if (worker == null || !ReferenceEquals(currentWorker, worker))
            return false;

        ReleaseWorker(worker);
        NotifyWorkerReleased(worker, this, reason);
        return true;
    }

    public bool CanAcceptWorker(RobotBrainNew worker)
    {
        if (worker == null || !isOn)
            return false;

        if (currentWorker != null && !ReferenceEquals(currentWorker, worker))
            return false;

        return true;
    }

    private void OnValidate()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
    }

    private void ApplyMaterial()
    {
        if (meshRenderer == null) return;
        if (materialOn == null || materialOff == null) return;

        meshRenderer.material = isOn ? materialOn : materialOff;
    }

    private void SubscribeCoreEvents()
    {
        OnPoweredOn += HandlePoweredOn;
        OnPoweredOff += HandlePoweredOff;

        OnRobotAssigned += HandleRobotAssigned;
        OnRobotFreed += HandleRobotFreed;
    }

    private void UnsubscribeCoreEvents()
    {
        OnPoweredOn -= HandlePoweredOn;
        OnPoweredOff -= HandlePoweredOff;

        OnRobotAssigned -= HandleRobotAssigned;
        OnRobotFreed -= HandleRobotFreed;
    }

    private void SubscribeConveyorEvents()
    {
        if (cubeConveyorController != null)
            cubeConveyorController.OnCubeProcessed += HandleCubeProcessed;
    }

    private void UnsubscribeConveyorEvents()
    {
        if (cubeConveyorController != null)
            cubeConveyorController.OnCubeProcessed -= HandleCubeProcessed;
    }

    private void SubscribeAlarmEvents()
    {
        if (factoryAlarmStatus != null)
            factoryAlarmStatus.OnAlarmStateChanged += HandleAlarmChanged;
    }

    private void UnsubscribeAlarmEvents()
    {
        if (factoryAlarmStatus != null)
            factoryAlarmStatus.OnAlarmStateChanged -= HandleAlarmChanged;
    }

    private void HandlePoweredOn(BaseMachine _)
    {
        ApplyMaterial();
        TryScheduleSpawn();
    }

    private void HandlePoweredOff(BaseMachine _)
    {
        StopConveyorAndCancelSpawn();
        ResetWorkerTracking();
    }

    private void HandleAlarmChanged(AlarmState state)
    {
        if (!isOn)
        {
            StopConveyorAndCancelSpawn();
            return;
        }

        if (state == AlarmState.Wanted)
            TryScheduleSpawn();
        else
            StopConveyorAndCancelSpawn();
    }

    private void HandleRobotAssigned(BaseMachine m)
    {
        if (!ReferenceEquals(m, this)) return;
        TryScheduleSpawn();
    }

    private void HandleRobotFreed(BaseMachine m)
    {
        if (!ReferenceEquals(m, this)) return;
        StopConveyorAndCancelSpawn();
    }

    private void HandleCubeProcessed()
    {
        cubeActive = false;
        TryScheduleSpawn();
    }

    private void TryScheduleSpawn()
    {
        if (!CanSpawnNow())
            return;

        var timeSinceLast = Time.time - lastSpawnTime;
        var delay = Mathf.Max(0f, spawnCooldown - timeSinceLast);
        CancelSpawn();
        Invoke(SpawnMethodName, delay);
    }

    private void SpawnCubeIfPossible()
    {
        if (!CanSpawnNow())
            return;

        cubeActive = true;
        lastSpawnTime = Time.time;
        cubeConveyorController.BeginConveyor();
    }

    private bool CanSpawnNow()
    {
        if (!isOn) return false;
        if (!isOccupied) return false;
        if (cubeConveyorController == null) return false;
        if (cubeActive) return false;
        if (!HasAliveWorker()) return false;
        return true;
    }

    private void CancelSpawn() => CancelInvoke(SpawnMethodName);

    private void StopConveyorAndCancelSpawn()
    {
        cubeConveyorController?.DetachCube();
        CancelSpawn();
        cubeActive = false;
    }

    private bool HasAliveWorker()
    {
        return currentWorker != null
            && currentWorkerState != null
            && currentWorkerState.CurrentState == RobotState.Alive;
    }

    private void TrackWorker(RobotBrainNew worker)
    {
        currentWorker = worker;
        SubscribeToWorkerState(worker);
    }

    private void ReplaceWorkerInPlace(RobotBrainNew incoming)
    {
        if (incoming == null)
            return;

        TrackWorker(incoming);
        base.AttachRobot(incoming.gameObject);
        TryScheduleSpawn();
    }

    private void ResetWorkerTracking()
    {
        UnsubscribeFromWorkerState();
        currentWorker = null;
        currentWorkerState = null;
    }

    private void SubscribeToWorkerState(RobotBrainNew worker)
    {
        UnsubscribeFromWorkerState();
        if (worker == null) return;

        currentWorkerState = worker.GetComponent<RobotStateController>();
        if (currentWorkerState != null)
            currentWorkerState.OnStateChanged += HandleWorkerStateChanged;
    }

    private void UnsubscribeFromWorkerState()
    {
        if (currentWorkerState != null)
            currentWorkerState.OnStateChanged -= HandleWorkerStateChanged;

        currentWorkerState = null;
    }

    private void HandleWorkerStateChanged(RobotState newState)
    {
        if (newState != RobotState.Dead)
            return;

        StopConveyorAndCancelSpawn();
        ResetWorkerTracking();

        // Frees the slot and notifies listeners.
        base.ReleaseRobot();
    }
}



