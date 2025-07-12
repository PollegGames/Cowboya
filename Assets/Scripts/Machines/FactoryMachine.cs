using UnityEngine;
using System;

public enum MachineType
{
    WorkStation,
    RestStation,
    SecurityStation,
}

[RequireComponent(typeof(MeshRenderer))]
public class FactoryMachine : BaseMachine
{
    [SerializeField] private MachineType machineType;
    [SerializeField] private Material materialOn;
    [SerializeField] private Material materialOff;

    private MeshRenderer meshRenderer;
    private EnemyWorkerController currentWorker;
    private IWaypointService waypointService;

    public event Action<FactoryMachine, bool> OnMachineStateChanged;

    public bool IsOn => isOn;
    public bool HasWorker => currentWorker != null;
    public EnemyWorkerController CurrentWorker => currentWorker;
    public MachineType Type => machineType;

    public void Initialize(IWaypointService service)
    {
        waypointService = service;
    }

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
    }

    /// <summary>
    /// Sets the machine's on/off state and updates the material.
    /// </summary>
    public void SetState(bool on)
    {
        if (on)
            PowerOn();
        else
            PowerOff();
    }

    /// <summary>
    /// Toggles the machine state.
    /// </summary>
    public void ToggleState() => SetState(!isOn);

    public override void PowerOn()
    {
        base.PowerOn();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, true);
    }

    public override void PowerOff()
    {
        if (!isOn) return;
        SendCurrentWorkerToRest();
        base.PowerOff();
        ApplyMaterial();
        OnMachineStateChanged?.Invoke(this, false);
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
        else if (machineType == MachineType.WorkStation)
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
        isOccupied = true;
        OnRobotAssigned?.Invoke(this);
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
        worker.stateMachine.ChangeState(
            new Worker_IsWork(worker, worker.stateMachine, worker.waypointService));
    }

    /// <summary>
    /// Sends the currently assigned worker to the rest station and clears the reference.
    /// </summary>
    private void SendCurrentWorkerToRest()
    {
        SendWorkerToRest(currentWorker);
        currentWorker = null;
    }

    public override void ReleaseRobot()
    {
        SendCurrentWorkerToRest();
        isOccupied = false;
        base.ReleaseRobot();
    }
}
