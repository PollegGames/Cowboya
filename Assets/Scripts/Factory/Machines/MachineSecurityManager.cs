using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Notifies security guards when a factory machine turns off.
/// Guards can subscribe to <see cref="OnFactoryMachineTurnedOff"/> to react.
/// </summary>
public class MachineSecurityManager : MonoBehaviour
{
    [SerializeField] private StationReservationService reservationService;

    private readonly List<FactoryMachine> factoryMachines = new();
    private readonly List<RestingMachine> restingMachines = new();
    private readonly List<SecurityMachine> securityMachines = new();

    private readonly List<RobotBrainNew> guards = new();

    /// <summary> Fired whenever a registered machine is switched off. </summary>
    public event Action<FactoryMachine> OnFactoryMachineTurnedOff;

    private void OnEnable()
    {
        if (reservationService != null)
        {
            reservationService.OnMachineFreed += HandleMachineFreed;
            reservationService.OnMachinePoweredOn += HandleMachinePoweredOn;
        }
    }

    private void OnDisable()
    {
        if (reservationService != null)
        {
            reservationService.OnMachineFreed -= HandleMachineFreed;
            reservationService.OnMachinePoweredOn -= HandleMachinePoweredOn;
        }
    }

    public void RegisterFactoryMachine(FactoryMachine machine)
    {
        if (machine == null || factoryMachines.Contains(machine)) return;

        factoryMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachinePowerChanged += HandleMachinePowerChanged;
    }

    public void RegisterRestingMachine(RestingMachine machine)
    {
        if (machine == null || restingMachines.Contains(machine)) return;

        restingMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachinePowerChanged += HandleMachinePowerChanged;
    }

    public void RegisterSecurityMachine(SecurityMachine machine)
    {
        if (machine == null || securityMachines.Contains(machine)) return;

        securityMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachinePowerChanged += HandleMachinePowerChanged;
        machine.OnMachineTurnedOff += HandleMachineTurnedOff;
    }

    private void OnDestroy()
    {
        foreach (var machine in factoryMachines)
        {
            if (machine == null)
                continue;
            machine.OnMachinePowerChanged -= HandleMachinePowerChanged;
        }

        foreach (var machine in restingMachines)
        {
            if (machine == null)
                continue;
            machine.OnMachinePowerChanged -= HandleMachinePowerChanged;
        }

        foreach (var machine in securityMachines)
        {
            if (machine == null)
                continue;
            machine.OnMachinePowerChanged -= HandleMachinePowerChanged;
            machine.OnMachineTurnedOff -= HandleMachineTurnedOff;
        }
    }

    public void RegisterGuard(RobotBrainNew guard)
    {
        if (guard == null || guards.Contains(guard)) return;
        Debug.Log($"Registering guard: {guard.name}");
        guards.Add(guard);
        RequestGuardPost(guard);
    }

    public void UnregisterGuard(RobotBrainNew guard)
    {
        if (guard == null) return;
        guards.Remove(guard);
    }

    private void HandleSecurityMachineTurningOff(SecurityMachine machine, RobotBrainNew currentGuard)
    {
        // Dispatch a different guard to reactivate; fall back to the current guard if it's the only eligible one.
        if (machine == null)
            return;

        DispatchGuardForSecurityMachine(machine, currentGuard);
    }

    private void HandleMachinePowerChanged(MachinePowerChangedEvent evt)
    {
        if (evt.Machine is FactoryMachine factoryMachine)
        {
            HandleFactoryMachineStateChanged(factoryMachine, evt.IsOn);
            return;
        }

        if (evt.Machine is RestingMachine restingMachine)
        {
            HandleRestingMachineStateChanged(restingMachine, evt.IsOn);
            return;
        }

        if (evt.Machine is SecurityMachine securityMachine)
            HandleSecurityMachineStateChanged(securityMachine, evt.IsOn);
    }

    private void HandleMachineTurnedOff(MachineTurnedOffEvent evt)
    {
        if (evt.Machine is not SecurityMachine securityMachine)
            return;

        var currentGuard = TryResolveRobotBrain(evt.PreviousRobot);
        HandleSecurityMachineTurningOff(securityMachine, currentGuard);
    }

    private void HandleFactoryMachineStateChanged(FactoryMachine machine, bool isOn)
    {
        if (!isOn)
        {
            OnFactoryMachineTurnedOff?.Invoke(machine);
            DispatchGuardForFactoryMachine(machine);
        }
    }

    private void HandleRestingMachineStateChanged(RestingMachine machine, bool isOn)
    {
        if (!isOn)
        {
            DispatchGuardForRestingMachine(machine);
        }
    }

    private void HandleSecurityMachineStateChanged(SecurityMachine machine, bool isOn)
    {
        if (!isOn)
        {
        }
        else if (reservationService == null)
        {
            TryAssignClosestRestingGuard(machine);
        }
    }

    private void DispatchGuardForFactoryMachine(FactoryMachine machine)
    {
        Debug.Log($"Dispatching guard for machine: {machine.name}");
        if (machine == null || guards.Count == 0) return;

        RobotBrainNew best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;

        foreach (var guard in guards)
        {
            if (guard == null) continue;
            if (!IsGuardStationedAtSecurityMachine(guard)) continue;

            float dist = Vector2.Distance(guard.transform.position, pos);
            if (dist < bestDist)
            {
                best = guard;
                bestDist = dist;
            }
        }

        if (best == null)
            return;

        PublishGuardDispatch(best, machine, false);
    }

    private void DispatchGuardForRestingMachine(RestingMachine machine)
    {
        Debug.Log($"Dispatching guard for machine: {machine.name}");
        if (machine == null || guards.Count == 0) return;

        RobotBrainNew best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;

        foreach (var guard in guards)
        {
            if (guard == null) continue;
            if (!IsGuardStationedAtSecurityMachine(guard)) continue;
            float dist = Vector2.Distance(guard.transform.position, pos);
            if (dist < bestDist)
            {
                best = guard;
                bestDist = dist;
            }
        }

        if (best == null)
            return;

        PublishGuardDispatch(best, machine, false);
    }

    private bool DispatchGuardForSecurityMachine(SecurityMachine machine, RobotBrainNew skipGuard)
    {
        Debug.Log($"Dispatching guard for security machine: {machine.name}");
        if (machine == null || guards.Count == 0) return false;

        RobotBrainNew best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;
        bool skippedEligible = false;

        foreach (var guard in guards)
        {
            if (guard == null) continue;
            if (!IsGuardStationedAtSecurityMachine(guard)) continue;
            if (skipGuard != null && ReferenceEquals(guard, skipGuard))
            {
                skippedEligible = true;
                continue;
            }

            float dist = Vector2.Distance(guard.transform.position, pos);
            if (dist < bestDist)
            {
                best = guard;
                bestDist = dist;
            }
        }

        if (best == null && skippedEligible)
            best = skipGuard;

        if (best == null)
            return false;

        PublishGuardDispatch(best, machine, false);
        return true;
    }

    public bool RequestGuardPost(RobotBrainNew guard)
    {
        if (guard == null)
            return false;

        if (TryAssignGuardToSecurityMachine(guard))
            return true;

        PublishGuardDispatch(guard, null, false);
        return true;
    }

    private bool TryAssignGuardToSecurityMachine(RobotBrainNew guard)
    {
        if (guard == null)
            return false;

        SecurityMachine target = null;
        if (reservationService != null)
        {
            target = reservationService.ReserveClosestStation(
                RobotRole.SecurityGuard,
                guard.transform.position,
                MachineType.SecurityMachine) as SecurityMachine;
        }

        if (target == null)
        {
            target = FindClosestAvailableSecurityMachine(guard.transform.position);
        }

        if (target == null)
            return false;

        PublishGuardDispatch(guard, target, target != null && target.IsOn);
        return true;
    }

    private SecurityMachine FindClosestAvailableSecurityMachine(Vector3 position)
    {
        SecurityMachine best = null;
        float bestDist = float.MaxValue;

        foreach (var machine in securityMachines)
        {
            if (machine == null)
                continue;
            if (!machine.IsOn || machine.IsOccupied)
                continue;

            float dist = Vector2.Distance(position, machine.transform.position);
            if (dist < bestDist)
            {
                best = machine;
                bestDist = dist;
            }
        }

        return best;
    }

    private void HandleMachineFreed(BaseMachine machine)
    {
        if (machine is SecurityMachine security)
            TryAssignClosestRestingGuard(security);
    }

    private void HandleMachinePoweredOn(BaseMachine machine)
    {
        if (machine is SecurityMachine security)
            TryAssignClosestRestingGuard(security);
    }

    private bool TryAssignClosestRestingGuard(SecurityMachine machine)
    {
        if (machine == null || !machine.IsOn || machine.IsOccupied)
            return false;

        bool reserved = false;
        if (reservationService != null)
        {
            reserved = reservationService.TryReserveStation(machine, RobotRole.SecurityGuard);
            if (!reserved)
                return false;
        }

        RobotBrainNew best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;

        foreach (var guard in guards)
        {
            if (guard == null) continue;
            if (IsGuardStationedAtSecurityMachine(guard)) continue;
            float dist = Vector2.Distance(guard.transform.position, pos);
            if (dist < bestDist)
            {
                best = guard;
                bestDist = dist;
            }
        }

        if (best == null)
        {
            if (reserved)
                reservationService.ReleaseStation(machine);
            return false;
        }

        PublishGuardDispatch(best, machine, machine.IsOn);
        return true;
    }

    private static bool IsGuardStationedAtSecurityMachine(RobotBrainNew guard)
    {
        if (guard == null)
            return false;
        return false;
    }

    private static void PublishGuardDispatch(RobotBrainNew guard, object payload, bool isOn)
    {
        if (guard == null)
            return;

        RobotDomainEventBus.PublishSecurityDispatch(guard, payload);
        RobotDomainEventBus.PublishMachineStateDispatch(guard, payload, isOn);
    }

    private static RobotBrainNew TryResolveRobotBrain(GameObject robot)
    {
        if (robot == null)
            return null;

        var brain = robot.GetComponent<RobotBrainNew>();
        if (brain != null)
            return brain;

        return robot.GetComponentInParent<RobotBrainNew>();
    }
}

