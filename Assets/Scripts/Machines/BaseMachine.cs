using UnityEngine;
using System;

public abstract class BaseMachine : MonoBehaviour
{
    [SerializeField] protected bool isOn = true;
    [SerializeField] protected bool isOccupied = false;
    [SerializeField] protected BoxCollider2D trigger;

    public bool IsOn => isOn;
    public bool IsOccupied => isOccupied;

    public event Action<BaseMachine> OnRobotAssigned;
    public event Action<BaseMachine> OnFreed;
    public event Action<BaseMachine> OnPoweredOff;

    protected virtual void Awake()
    {
        if (trigger == null)
            trigger = GetComponentInChildren<BoxCollider2D>();
    }

    public virtual void PowerOn()
    {
        isOn = true;
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
        OnFreed?.Invoke(this);
    }
}
