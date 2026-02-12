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
    public event Action<FactoryMachine, RobotBrain> OnMachineTurningOff;
    private RobotBrain currentWorker;
    private RobotStateController currentWorkerState;

    public bool HasWorker => currentWorker != null;
    public RobotBrain CurrentWorker => currentWorker;

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
        UnsubscribeFromWorkerState();
        currentWorker = null;
        currentWorkerState = null;
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
        if (!isOccupied || cubeConveyorController == null || cubeActive || !HasAliveWorker())
            return;

        float timeSinceLast = Time.time - lastSpawnTime;
        float delay = Mathf.Max(0f, spawnCooldown - timeSinceLast);
        CancelInvoke(nameof(BeginConveyorInternal));
        Invoke(nameof(BeginConveyorInternal), delay);
    }


    private void BeginConveyorInternal()
    {
        if (!isOccupied || cubeConveyorController == null || cubeActive || !HasAliveWorker())
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
        var newWorker = robot.GetComponent<RobotBrain>();
        if (newWorker == null) return;
        if (newWorker == currentWorker && isOn && Type == MachineType.WorkStation)
            return;
        // If the machine is off, always send the incoming worker to rest.
        if (!isOn)
        {
            SendWorkerToRest(currentWorker);
            UnsubscribeFromWorkerState();
            currentWorker = null;
            SendWorkerToRest(newWorker);
            return;
        }

        if (Type == MachineType.WorkStation)
        {
            // Capture previous worker, then assign and activate the new one.
            var previousWorker = currentWorker;
            currentWorker = newWorker;
            SubscribeToWorkerState(currentWorker);

            SetWorkerToWork(currentWorker);

            if (previousWorker != null && previousWorker != currentWorker)
            {
                UnsubscribeFromWorkerState(previousWorker);
                SendWorkerToRest(previousWorker);
            }
        }
        else
        {
            // Machine is rest
            SendWorkerToWork(currentWorker);
            SendWorkerToRest(newWorker);
            currentWorker = newWorker;
            SubscribeToWorkerState(currentWorker);
        }

        waypointService?.ReleaseMachine(this);
        base.AttachRobot(robot);
    }

    /// <summary>
    /// Helper to send a worker to the rest station state.
    /// </summary>
    private void SendWorkerToRest(RobotBrain worker)
    {
        if (worker == null) return;
        object payload = null;
        if (waypointService != null)
        {
            payload = waypointService.GetFirstRestPoint();
            if (payload == null)
                payload = waypointService.GetStartPoint();
        }
        if (payload == null)
            payload = transform.position;
        Debug.Log($"[WorkerRestDebug][FactoryMachine.SendWorkerToRest] machine={name} isOn={isOn} worker={worker.name} payloadType={(payload!=null?payload.GetType().Name:"null")} payload={payload}");
        worker.OnMachineStateChanged(payload, false);
    }

    private void SendWorkerToWork(RobotBrain worker)
    {
        if (worker == null) return;
        Debug.Log($"[WorkerRestDebug][FactoryMachine.SendWorkerToWork] machine={name} isOn={isOn} worker={worker.name} payloadType={this.GetType().Name} payload={this}");
        worker.OnMachineStateChanged(this, true);
    }

    private void SetWorkerToWork(RobotBrain worker)
    {
        if (worker == null) return;
        Debug.Log($"[WorkerRestDebug][FactoryMachine.SetWorkerToWork] machine={name} isOn={isOn} worker={worker.name}");
        worker.OnMachineStateChanged(this, true);
    }

    public void SendWorkerBackToWork(RobotBrain worker)
    {
        if (worker == null) return;
        worker.OnMachineStateChanged(this, true);
    }

    private void SubscribeToWorkerState(RobotBrain worker)
    {
        UnsubscribeFromWorkerState();
        if (worker == null)
            return;

        currentWorkerState = worker.GetComponent<RobotStateController>();
        if (currentWorkerState != null)
            currentWorkerState.OnStateChanged += HandleWorkerStateChanged;
    }

    private void UnsubscribeFromWorkerState(RobotBrain worker = null)
    {
        var state = worker != null ? worker.GetComponent<RobotStateController>() : currentWorkerState;
        if (state != null)
            state.OnStateChanged -= HandleWorkerStateChanged;
        if (worker == null)
            currentWorkerState = null;
    }

    private void HandleWorkerStateChanged(RobotState newState)
    {
        if (newState != RobotState.Dead)
            return;

        cubeConveyorController?.DetachCube();
        CancelInvoke(nameof(BeginConveyorInternal));
        cubeActive = false;

        UnsubscribeFromWorkerState();
        currentWorker = null;
        currentWorkerState = null;
        base.ReleaseRobot(); // frees the slot and notifies listeners
    }

    private bool HasAliveWorker()
    {
        return currentWorker != null && currentWorkerState != null && currentWorkerState.CurrentState == RobotState.Alive;
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
