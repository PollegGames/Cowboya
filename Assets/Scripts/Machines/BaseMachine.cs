using UnityEngine;
using System;
public enum MachineType
{
    WorkStation,
    RestStation,
    SecurityMachine,
    SpawningMachine
}
public abstract class BaseMachine : MonoBehaviour
{
    [SerializeField] private MachineType machineType;
    [SerializeField] protected bool isOn = true;
    [SerializeField] protected bool isOccupied = false;
    [SerializeField] protected BoxCollider2D trigger;

    public bool IsOn => isOn;
    public bool IsOccupied => isOccupied;

    public event Action<BaseMachine> OnRobotAssigned;
    public event Action<BaseMachine> OnRobotFreed;
    public event Action<BaseMachine> OnPoweredOn;
    public event Action<BaseMachine> OnPoweredOff;

    protected IWaypointService waypointService;
    public MachineType Type => machineType;    protected virtual void Awake()
    {
        if (trigger == null)
            trigger = GetComponentInChildren<BoxCollider2D>();
    }

    public void Initialize(IWaypointService service)
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
    }

    public virtual void PowerOff()
    {
        isOn = false;
        OnPoweredOff?.Invoke(this);
    }

    public virtual void AttachRobot(GameObject robot)
    {
        isOccupied = robot != null;
        if (robot != null)
            OnRobotAssigned?.Invoke(this);
    }

    public virtual void ReleaseRobot()
    {
        isOccupied = false;
        OnRobotFreed?.Invoke(this);
    }
}
