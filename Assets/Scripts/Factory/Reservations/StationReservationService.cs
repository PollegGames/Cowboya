using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages which machines are available for workers. Raises events when a
/// machine is powered on, off or freed so robots can reserve stations.
/// </summary>
public class StationReservationService : MonoBehaviour
{
    public readonly struct StationReservation
    {
        public StationReservation(BaseMachine machine, RobotBrainNew owner, string purpose)
        {
            Machine = machine;
            Owner = owner;
            Purpose = purpose;
        }

        public BaseMachine Machine { get; }
        public RobotBrainNew Owner { get; }
        public string Purpose { get; }
    }

    public static StationReservationService Instance { get; private set; }

    private readonly List<BaseMachine> machines = new();
    private readonly Dictionary<BaseMachine, HashSet<RobotRole>> machineRoles = new();
    private readonly Dictionary<RobotRole, List<BaseMachine>> available = new();
    private readonly Dictionary<BaseMachine, StationReservation> reservations = new();

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
        if (machine == null) return;
        if (!machineRoles.TryGetValue(machine, out HashSet<RobotRole> roles))
        {
            roles = new HashSet<RobotRole>();
            machineRoles[machine] = roles;
        }
        if (!roles.Add(role)) return;

        if (!machines.Contains(machine))
        {
            machines.Add(machine);
            machine.OnRobotFreed += HandleMachineFreed;
            machine.OnRobotAssigned += HandleMachineOccupied;
            machine.OnPoweredOff += HandleMachinePoweredOff;
            machine.OnPoweredOn += HandleMachinePoweredOn;
        }
        if (!available.ContainsKey(role))
            available[role] = new List<BaseMachine>();
        if (machine.IsOn && !machine.IsOccupied && !reservations.ContainsKey(machine))
            available[role].Add(machine);
    }

    private void HandleMachineFreed(BaseMachine machine)
    {
        foreach (RobotRole role in machineRoles[machine])
        {
            if (!available[role].Contains(machine) && machine.IsOn && !reservations.ContainsKey(machine))
                available[role].Add(machine);
        }
        OnMachineFreed?.Invoke(machine);
    }

    private void HandleMachineOccupied(BaseMachine machine)
    {
        foreach (RobotRole role in machineRoles[machine])
            available[role].Remove(machine);
        reservations.Remove(machine);
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
        if (isOn)
        {
            foreach (RobotRole role in machineRoles[machine])
            {
                if (!machine.IsOccupied && !reservations.ContainsKey(machine) && !available[role].Contains(machine))
                    available[role].Add(machine);
            }
            OnMachinePoweredOn?.Invoke(machine);
        }
        else
        {
            foreach (RobotRole role in machineRoles[machine])
                available[role].Remove(machine);
            reservations.Remove(machine);
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
        return ReserveStation(role, null, "legacy");
    }

    /// <summary>Reserves an available station for a specific robot and purpose.</summary>
    public BaseMachine ReserveStation(RobotRole role, RobotBrainNew owner, string purpose)
    {
        var list = available[role];
        if (list.Count == 0) return null;
        var machine = list[0];
        RemoveFromAllAvailability(machine);
        reservations[machine] = new StationReservation(machine, owner, purpose);
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
        return ReserveClosestStation(role, position, typeFilter, null, "legacy");
    }

    /// <summary>Reserves the closest station for a specific robot and purpose.</summary>
    public BaseMachine ReserveClosestStation(
        RobotRole role,
        Vector3 position,
        MachineType? typeFilter,
        RobotBrainNew owner,
        string purpose)
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

        RemoveFromAllAvailability(best);
        reservations[best] = new StationReservation(best, owner, purpose);
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
        return TryReserveStation(machine, role, null, "legacy");
    }

    /// <summary>Attempts to reserve a specific station for a robot and purpose.</summary>
    public bool TryReserveStation(BaseMachine machine, RobotRole role, RobotBrainNew owner, string purpose)
    {
        if (machine == null)
            return false;

        if (!available.TryGetValue(role, out var list))
            return false;

        if (!list.Contains(machine))
            return false;

        RemoveFromAllAvailability(machine);
        reservations[machine] = new StationReservation(machine, owner, purpose);
        return true;
    }

    /// <summary>
    /// Releases a previously reserved machine, making it available again.
    /// </summary>
    /// <param name="machine">The machine to release.</param>
    public void ReleaseStation(BaseMachine machine)
    {
        ReleaseStation(machine, null);
    }

    /// <summary>Releases a reservation when the caller owns it or uses the legacy ownerless API.</summary>
    public bool ReleaseStation(BaseMachine machine, RobotBrainNew owner)
    {
        if (machine == null || !machineRoles.ContainsKey(machine)) return false;
        if (owner != null
            && reservations.TryGetValue(machine, out StationReservation reservation)
            && reservation.Owner != null
            && !ReferenceEquals(reservation.Owner, owner))
        {
            return false;
        }

        reservations.Remove(machine);
        foreach (RobotRole role in machineRoles[machine])
        {
            if (machine.IsOn && !machine.IsOccupied && !available[role].Contains(machine))
                available[role].Add(machine);
        }
        return true;
    }

    /// <summary>Returns the current reservation record for a machine.</summary>
    public bool TryGetReservation(BaseMachine machine, out StationReservation reservation)
    {
        return reservations.TryGetValue(machine, out reservation);
    }

    private void RemoveFromAllAvailability(BaseMachine machine)
    {
        if (machine == null || !machineRoles.TryGetValue(machine, out HashSet<RobotRole> roles))
            return;

        foreach (RobotRole role in roles)
            available[role].Remove(machine);
    }

    private void OnDestroy()
    {
        foreach (BaseMachine machine in machines)
        {
            if (machine == null)
                continue;
            machine.OnRobotFreed -= HandleMachineFreed;
            machine.OnRobotAssigned -= HandleMachineOccupied;
            machine.OnPoweredOff -= HandleMachinePoweredOff;
            machine.OnPoweredOn -= HandleMachinePoweredOn;
        }

        if (Instance == this)
            Instance = null;
    }
}
