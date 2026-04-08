using System;
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
    private RobotBrain currentWorker;
    private RobotStateController currentWorkerState;

    private const string SpawnMethodName = nameof(SpawnCubeIfPossible);

    public event Action<FactoryMachine, bool> OnMachineStateChanged;
    public event Action<FactoryMachine, RobotBrain> OnMachineTurningOff;

    public bool HasWorker => currentWorker != null;
    public RobotBrain CurrentWorker => currentWorker;

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
        OnMachineStateChanged?.Invoke(this, true);

        TryScheduleSpawn();
    }

    public override void PowerOff()
    {
        if (!isOn) return;

        OnMachineTurningOff?.Invoke(this, currentWorker);

        if (isOccupied)
            base.ReleaseRobot();

        base.PowerOff();

        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, false);

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

        var newWorker = robot.GetComponent<RobotBrain>();
        if (newWorker == null) return;

        if (!CanAcceptWorker(newWorker)) return;
        if (ReferenceEquals(newWorker, currentWorker)) return;

        TrackWorker(newWorker);

        waypointService?.ReleaseMachine(this);
        base.AttachRobot(robot);

        TryScheduleSpawn();
    }

    public override void ReleaseRobot()
    {
        UnsubscribeFromWorkerState();
        base.ReleaseRobot();
        ResetWorkerTracking();
        StopConveyorAndCancelSpawn();
    }

    public void ReleaseWorker(RobotBrain worker)
    {
        if (worker == null) return;
        if (!ReferenceEquals(worker, currentWorker)) return;

        ReleaseRobot();
    }

    public bool CanAcceptWorker(RobotBrain worker)
    {
        if (worker == null || !isOn)
            return false;

        if (waypointService != null
            && waypointService.IsMachineReserved(this)
            && !waypointService.IsMachineReservedFor(this, worker)
            && !ReferenceEquals(worker, currentWorker))
        {
            return false;
        }

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

    private void TrackWorker(RobotBrain worker)
    {
        currentWorker = worker;
        SubscribeToWorkerState(worker);
    }

    private void ResetWorkerTracking()
    {
        UnsubscribeFromWorkerState();
        currentWorker = null;
        currentWorkerState = null;
    }

    private void SubscribeToWorkerState(RobotBrain worker)
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
