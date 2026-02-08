using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages which machines are available for workers. Raises events when a
/// machine is powered on, off or freed so robots can reserve stations.
/// </summary>
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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        foreach (RobotRole role in Enum.GetValues(typeof(RobotRole)))
            available[role] = new List<BaseMachine>();
    }

    /// <summary>
    /// Registers a machine with the service so robots can reserve it.
    /// </summary>
    /// <param name="machine">The machine to register.</param>
    /// <param name="role">The role allowed to use the machine.</param>
    public void RegisterMachine(BaseMachine machine, RobotRole role)
    {
        if (machine == null || machines.Contains(machine)) return;
        machines.Add(machine);
        machineRoles[machine] = role;
        machine.OnRobotFreed += HandleMachineFreed;
        machine.OnRobotAssigned += HandleMachineOccupied;
        machine.OnPoweredOff += HandleMachinePoweredOff;
        machine.OnPoweredOn += HandleMachinePoweredOn;
        if (!available.ContainsKey(role))
            available[role] = new List<BaseMachine>();
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
        NotifyPowerChanged(machine, false);
    }

    private void HandleMachinePoweredOn(BaseMachine machine)
    {
        NotifyPowerChanged(machine, true);
    }

    /// <summary>
    /// Updates machine availability when its power state changes.
    /// </summary>
    /// <param name="machine">Machine whose power changed.</param>
    /// <param name="isOn">True if the machine is now powered on.</param>
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

    /// <summary>
    /// Reserves and returns an available machine for the specified role.
    /// </summary>
    /// <param name="role">The role requesting a machine.</param>
    /// <returns>The reserved machine or <c>null</c> if none are available.</returns>
    public BaseMachine ReserveStation(RobotRole role)
    {
        var list = available[role];
        if (list.Count == 0) return null;
        var machine = list[0];
        list.RemoveAt(0);
        return machine;
    }

    /// <summary>
    /// Reserves and returns the closest available machine for the specified role.
    /// </summary>
    /// <param name="role">The role requesting a machine.</param>
    /// <param name="position">World position used to find the closest machine.</param>
    /// <param name="typeFilter">Optional machine type filter.</param>
    /// <returns>The reserved machine or <c>null</c> if none are available.</returns>
    public BaseMachine ReserveClosestStation(RobotRole role, Vector3 position, MachineType? typeFilter = null)
    {
        var list = available[role];
        if (list.Count == 0) return null;

        BaseMachine best = null;
        float bestDist = float.MaxValue;

        foreach (var machine in list)
        {
            if (machine == null) continue;
            if (typeFilter.HasValue && machine.Type != typeFilter.Value) continue;

            float dist = Vector2.Distance(position, machine.transform.position);
            if (dist < bestDist)
            {
                best = machine;
                bestDist = dist;
            }
        }

        if (best == null)
            return null;

        list.Remove(best);
        return best;
    }

    /// <summary>
    /// Attempts to reserve a specific machine for the specified role.
    /// </summary>
    /// <param name="machine">Machine to reserve.</param>
    /// <param name="role">Role requesting the machine.</param>
    /// <returns><c>true</c> if the machine was reserved.</returns>
    public bool TryReserveStation(BaseMachine machine, RobotRole role)
    {
        if (machine == null)
            return false;

        if (!available.TryGetValue(role, out var list))
            return false;

        if (!list.Contains(machine))
            return false;

        list.Remove(machine);
        return true;
    }

    /// <summary>
    /// Releases a previously reserved machine, making it available again.
    /// </summary>
    /// <param name="machine">The machine to release.</param>
    public void ReleaseStation(BaseMachine machine)
    {
        if (machine == null || !machineRoles.ContainsKey(machine)) return;
        var role = machineRoles[machine];
        if (machine.IsOn && !available[role].Contains(machine))
            available[role].Add(machine);
    }
}
