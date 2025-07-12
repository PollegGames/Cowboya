using System;
using System.Collections.Generic;
using UnityEngine;

public class StationReservationService : MonoBehaviour
{
    public static StationReservationService Instance { get; private set; }

    private readonly List<BaseMachine> machines = new();
    private readonly Dictionary<BaseMachine, RobotRole> machineRoles = new();
    private readonly Dictionary<RobotRole, List<BaseMachine>> available = new();

    public event Action<BaseMachine> OnMachineFreed;
    public event Action<BaseMachine> OnMachinePoweredOff;
    public event Action<BaseMachine> OnMachinePoweredOn;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        foreach (RobotRole role in Enum.GetValues(typeof(RobotRole)))
            available[role] = new List<BaseMachine>();
    }

    public void RegisterMachine(BaseMachine machine, RobotRole role)
    {
        if (machine == null || machines.Contains(machine)) return;
        machines.Add(machine);
        machineRoles[machine] = role;
        machine.OnFreed += HandleMachineFreed;
        machine.OnRobotAssigned += HandleMachineOccupied;
        machine.OnPoweredOff += HandleMachinePoweredOff;
        if (machine.IsOn && !machine.IsOccupied)
            available[role].Add(machine);
    }

    private void HandleMachineFreed(BaseMachine machine)
    {
        var role = machineRoles[machine];
        if (!available[role].Contains(machine) && machine.IsOn)
            available[role].Add(machine);
        OnMachineFreed?.Invoke(machine);
    }

    private void HandleMachineOccupied(BaseMachine machine)
    {
        var role = machineRoles[machine];
        available[role].Remove(machine);
    }

    private void HandleMachinePoweredOff(BaseMachine machine)
    {
        var role = machineRoles[machine];
        available[role].Remove(machine);
        OnMachinePoweredOff?.Invoke(machine);
    }

    public void NotifyPowerChanged(BaseMachine machine, bool isOn)
    {
        var role = machineRoles[machine];
        if (isOn)
        {
            if (!machine.IsOccupied && !available[role].Contains(machine))
                available[role].Add(machine);
            OnMachinePoweredOn?.Invoke(machine);
        }
        else
        {
            available[role].Remove(machine);
            OnMachinePoweredOff?.Invoke(machine);
        }
    }

    public BaseMachine ReserveStation(RobotRole role)
    {
        var list = available[role];
        if (list.Count == 0) return null;
        var machine = list[0];
        list.RemoveAt(0);
        return machine;
    }

    public void ReleaseStation(BaseMachine machine)
    {
        if (machine == null || !machineRoles.ContainsKey(machine)) return;
        var role = machineRoles[machine];
        if (machine.IsOn && !available[role].Contains(machine))
            available[role].Add(machine);
    }
}
