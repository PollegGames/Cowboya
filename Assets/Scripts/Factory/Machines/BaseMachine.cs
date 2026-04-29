using UnityEngine;
using System;

public enum MachineType
{
    WorkStation,
    RestStation,
    SecurityMachine,
    SpawningMachine
}

public readonly struct MachinePowerChangedEvent
{
    public MachinePowerChangedEvent(BaseMachine machine, bool isOn)
    {
        Machine = machine;
        IsOn = isOn;
    }

    public BaseMachine Machine { get; }
    public bool IsOn { get; }
}

public readonly struct MachineOccupancyChangedEvent
{
    public MachineOccupancyChangedEvent(BaseMachine machine, GameObject robot, bool isOccupied)
    {
        Machine = machine;
        Robot = robot;
        IsOccupied = isOccupied;
    }

    public BaseMachine Machine { get; }
    public GameObject Robot { get; }
    public bool IsOccupied { get; }
}

public readonly struct MachineTurnedOffEvent
{
    public MachineTurnedOffEvent(BaseMachine machine, GameObject previousRobot)
    {
        Machine = machine;
        PreviousRobot = previousRobot;
    }

    public BaseMachine Machine { get; }
    public GameObject PreviousRobot { get; }
}

public abstract class BaseMachine : MonoBehaviour
{
    [SerializeField] private MachineType machineType;
    [SerializeField] protected bool isOn = true;
    [SerializeField] protected bool isOccupied = false;
    [SerializeField] protected BoxCollider2D trigger;

    private GameObject attachedRobot;

    public bool IsOn => isOn;
    public bool IsOccupied => isOccupied;
    public GameObject AttachedRobot => attachedRobot;

    public event Action<BaseMachine> OnRobotAssigned;
    public event Action<BaseMachine> OnRobotFreed;
    public event Action<BaseMachine> OnPoweredOn;
    public event Action<BaseMachine> OnPoweredOff;
    public event Action<MachinePowerChangedEvent> OnMachinePowerChanged;
    public event Action<MachineOccupancyChangedEvent> OnMachineOccupancyChanged;
    public event Action<MachineTurnedOffEvent> OnMachineTurnedOff;

    protected IWaypointService waypointService;
    public IWaypointService WaypointService => waypointService;
    public MachineType Type => machineType;

    protected virtual void Awake()
    {
        if (trigger == null)
            trigger = GetComponentInChildren<BoxCollider2D>();
    }

    public void InitializeWaypointService(IWaypointService service)
    {
        waypointService = service;
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
    public virtual void PowerOn()
    {
        isOn = true;
        OnPoweredOn?.Invoke(this);
        OnMachinePowerChanged?.Invoke(new MachinePowerChangedEvent(this, true));
    }

    public virtual void PowerOff()
    {
        OnMachineTurnedOff?.Invoke(new MachineTurnedOffEvent(this, attachedRobot));
        isOn = false;
        OnPoweredOff?.Invoke(this);
        OnMachinePowerChanged?.Invoke(new MachinePowerChangedEvent(this, false));
    }

    public virtual void AttachRobot(GameObject robot)
    {
        attachedRobot = robot;
        isOccupied = robot != null;
        if (robot != null)
            OnRobotAssigned?.Invoke(this);
        OnMachineOccupancyChanged?.Invoke(new MachineOccupancyChangedEvent(this, robot, isOccupied));
    }

    public virtual void ReleaseRobot()
    {
        var previousRobot = attachedRobot;
        attachedRobot = null;
        isOccupied = false;
        OnRobotFreed?.Invoke(this);
        OnMachineOccupancyChanged?.Invoke(new MachineOccupancyChangedEvent(this, previousRobot, false));
    }

    public virtual bool TryAttachWorker(RobotBrainNew worker, string reason)
    {
        _ = worker;
        _ = reason;
        return false;
    }

    public virtual bool TryReplaceWorker(RobotBrainNew incoming, string reason)
    {
        _ = incoming;
        _ = reason;
        return false;
    }

    public virtual bool TryReleaseWorker(RobotBrainNew worker, string reason)
    {
        _ = worker;
        _ = reason;
        return false;
    }

    protected static RoomWaypoint ResolveMachineWaypoint(BaseMachine machine)
    {
        if (machine == null)
            return null;

        return machine.GetComponent<RoomWaypoint>() ?? machine.GetComponentInParent<RoomWaypoint>();
    }

    protected static MachineType? ResolveNextDesiredMachineType(BaseMachine machine)
    {
        if (machine == null)
            return null;

        switch (machine.Type)
        {
            case MachineType.WorkStation:
                return MachineType.RestStation;
            case MachineType.RestStation:
                return MachineType.WorkStation;
            default:
                return null;
        }
    }

    protected static void NotifyWorkerAttached(RobotBrainNew worker, BaseMachine machine)
    {
        if (worker == null || worker.Memory == null)
            return;

        worker.Memory.NotifyMachineSlotAttached(ResolveMachineWaypoint(machine));
    }

    protected static void NotifyWorkerReleased(RobotBrainNew worker, BaseMachine machine, string reason)
    {
        if (worker == null || worker.Memory == null)
            return;

        MachineType? nextMachineType = ResolveNextDesiredMachineType(machine);
        if (nextMachineType.HasValue)
            worker.Memory.SetDesiredMachineType(nextMachineType.Value);

        worker.Memory.NotifyMachineSlotReleasedTransient();

        if (RobotNewPipelineRuntime.IsWorkerCycleValidationEnabled)
        {
            RobotEcosystemProbe.RecordWorkerCycleTransition(
                worker,
                worker.Heart != null ? worker.Heart.CurrentTask : null,
                new RobotTask(RobotTaskType.GoToMachine),
                reason);
        }
    }
}
