using UnityEngine;
using System;

[RequireComponent(typeof(MeshRenderer))]
public class FactoryMachine : BaseMachine
{
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;

    [Header("Conveyor Settings")]
    [SerializeField] private CubeConveyorController cubeConveyorController;
    [SerializeField] private FactoryAlarmStatus factoryAlarmStatus;
    [SerializeField] private float spawnCooldown = 1f;

    private float lastSpawnTime = -Mathf.Infinity;
    private bool cubeActive = false;

    public event Action<FactoryMachine, bool> OnMachineStateChanged;
    public event Action<FactoryMachine, EnemyWorkerController> OnMachineTurningOff;
    private EnemyWorkerController currentWorker;

    public bool HasWorker => currentWorker != null;
    public EnemyWorkerController CurrentWorker => currentWorker;

    protected override void Awake()
    {
        base.Awake();
        meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
        OnPoweredOn += HandlePoweredOn;
        OnPoweredOff += HandlePoweredOff;

        OnRobotAssigned += HandleRobotAssigned;
        OnRobotFreed += HandleRobotFreed;

        if (cubeConveyorController != null)
            cubeConveyorController.OnCubeProcessed += HandleCubeProcessed;
    }

    private void OnDestroy()
    {
        OnPoweredOn -= HandlePoweredOn;
        OnPoweredOff -= HandlePoweredOff;

        OnRobotAssigned -= HandleRobotAssigned;
        OnRobotFreed -= HandleRobotFreed;
        if (cubeConveyorController != null)
            cubeConveyorController.OnCubeProcessed -= HandleCubeProcessed;
    }

    private void OnEnable()
    {
        if (factoryAlarmStatus != null)
            factoryAlarmStatus.OnAlarmStateChanged += HandleAlarmChanged;
    }

    private void OnDisable()
    {
        if (factoryAlarmStatus != null)
            factoryAlarmStatus.OnAlarmStateChanged -= HandleAlarmChanged;
        CancelInvoke(nameof(BeginConveyorInternal));
    }

    public override void PowerOn()
    {
        base.PowerOn();
        ApplyMaterial();
        SendWorkerToWork(currentWorker);
        OnMachineStateChanged?.Invoke(this, true);
    }

    public override void PowerOff()
    {
        if (!isOn) return;
        SendCurrentWorkerToRest();
        OnMachineTurningOff?.Invoke(this, currentWorker);
        base.PowerOff();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, false);
        currentWorker = null;
    }

    private void ApplyMaterial()
    {
        if (meshRenderer == null) return;
        if (materialOn == null || materialOff == null) return;
        meshRenderer.material = isOn ? materialOn : materialOff;
    }

    private void OnValidate()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
    }

    private void HandlePoweredOn(BaseMachine machine)
    {
        ScheduleSpawn();
    }

    private void HandlePoweredOff(BaseMachine machine)
    {
        cubeConveyorController?.DetachCube();
        CancelInvoke(nameof(BeginConveyorInternal));
    }

    private void HandleAlarmChanged(AlarmState state)
    {
        if (state == AlarmState.Wanted && isOn)
        {
            ScheduleSpawn();
        }
        else
        {
            cubeConveyorController?.DetachCube();
            CancelInvoke(nameof(BeginConveyorInternal));
        }
    }
    private void HandleRobotAssigned(BaseMachine m)
    {
        if (m != this) return;
        ScheduleSpawn();
    }

    private void HandleRobotFreed(BaseMachine m)
    {
        if (m != this) return;
        cubeConveyorController?.DetachCube();        // drop any cube currently on the conveyor
        CancelInvoke(nameof(BeginConveyorInternal)); // stop future spawns
    }

    private void HandleCubeProcessed()
    {
        cubeActive = false;
        ScheduleSpawn();
    }

    private void ScheduleSpawn()
    {
        if (!isOccupied || cubeConveyorController == null || cubeActive)
            return;

        float timeSinceLast = Time.time - lastSpawnTime;
        float delay = Mathf.Max(0f, spawnCooldown - timeSinceLast);
        CancelInvoke(nameof(BeginConveyorInternal));
        Invoke(nameof(BeginConveyorInternal), delay);
    }


    private void BeginConveyorInternal()
    {
        if (!isOccupied || cubeConveyorController == null || cubeActive)
            return;

        cubeActive = true;
        lastSpawnTime = Time.time;
        cubeConveyorController.BeginConveyor();
    }

    /// <summary>
    /// Called when a worker arrives at this machine.
    /// Sends workers to the appropriate state based on machine status and type.
    /// </summary>
    public override void AttachRobot(GameObject robot)
    {
        var newWorker = robot.GetComponent<EnemyWorkerController>();
        if (newWorker == null) return;
        // If the machine is off, send any worker to rest
        if (!isOn)
        {
            SendWorkerToRest(currentWorker);
            currentWorker = null;
            SendWorkerToRest(newWorker);
        }
        else if (Type == MachineType.WorkStation)
        {
            // Send existing worker to rest before assigning new
            SendWorkerToRest(currentWorker);
            // Assign the new worker to work
            SetWorkerToWork(newWorker);
            currentWorker = newWorker;
        }
        else
        {
            // Machine is rest
            // Send existing worker to work before assigning new
            SendWorkerToWork(currentWorker);
            // Assign the new worker to rest
            SendWorkerToRest(newWorker);
            currentWorker = newWorker;
        }

        waypointService?.ReleaseMachine(this);
        base.AttachRobot(robot);
    }

    /// <summary>
    /// Helper to send a worker to the rest station state.
    /// </summary>
    private void SendWorkerToRest(EnemyWorkerController worker)
    {
        if (worker == null) return;
        worker.stateMachine.ChangeState(
            new Worker_GoingToRestStation(worker, worker.stateMachine, worker.waypointService));
    }

    private void SendWorkerToWork(EnemyWorkerController worker)
    {
        if (worker == null) return;
        worker.stateMachine.ChangeState(
            new Worker_GoingToLeastWorkedStation(worker, worker.stateMachine, worker.waypointService));
    }

    private void SetWorkerToWork(EnemyWorkerController worker)
    {
        if (worker == null) return;
        worker.SetCurrentMachine(this);
        worker.stateMachine.ChangeState(
            new Worker_IsWork(worker, worker.stateMachine, worker.waypointService));
    }

    public void SendWorkerBackToWork(EnemyWorkerController worker)
    {
        if (worker == null) return;
        worker.stateMachine.ChangeState(
            new Worker_GoingToMachine(worker, worker.stateMachine, worker.waypointService, this));
    }

    /// <summary>
    /// Sends the currently assigned worker to the rest station and clears the reference.
    /// </summary>
    private void SendCurrentWorkerToRest()
    {
        SendWorkerToRest(currentWorker);
    }

    public override void ReleaseRobot()
    {
        SendCurrentWorkerToRest();
        isOccupied = false;
        base.ReleaseRobot();
        currentWorker = null;
    }
}
