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
    private readonly List<ReactiveMachineAI> guards = new();

    /// <summary>
    /// Fired whenever a registered machine is switched off.
    /// </summary>
    public event Action<FactoryMachine> OnFactoryMachineTurnedOff;
    public event Action<RestingMachine> OnRestingMachineTurnedOff;
    public event Action<SecurityMachine> OnSecurityMachineTurnedOff;

    public void RegisterFactoryMachine(FactoryMachine machine)
    {
        if (machine == null || factoryMachines.Contains(machine))
            return;
        factoryMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachineStateChanged += HandleFactoryMachineStateChanged;
    }

    public void RegisterRestingMachine(RestingMachine machine)
    {
        if (machine == null || restingMachines.Contains(machine))
            return;
        restingMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachineStateChanged += HandleRestingMachineStateChanged;
    }

    public void RegisterSecurityMachine(SecurityMachine machine)
    {
        if (machine == null || securityMachines.Contains(machine))
            return;
        securityMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachineStateChanged += HandleSecurityMachineStateChanged;
    }
    public void RegisterGuard(ReactiveMachineAI guard)
    {
        if (guard == null || guards.Contains(guard))
            return;
        Debug.Log($"Registering guard: {guard.name}");
        guards.Add(guard);
    }

    public void UnregisterGuard(ReactiveMachineAI guard)
    {
        if (guard == null)
            return;
        guards.Remove(guard);
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
            OnRestingMachineTurnedOff?.Invoke(machine);
            DispatchGuardForRestingMachine(machine);
        }
    }

    private void HandleSecurityMachineStateChanged(SecurityMachine machine, bool isOn)
    {
        if (!isOn)
        {
            OnSecurityMachineTurnedOff?.Invoke(machine);
        }
    }

    private void DispatchGuardForFactoryMachine(FactoryMachine machine)
    {
        Debug.Log($"Dispatching guard for machine: {machine.name}");
        if (machine == null || guards.Count == 0)
            return;

        ReactiveMachineAI best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;
        foreach (var guard in guards)
        {
            if (guard == null) continue;
            var controller = guard.GetComponent<EnemyController>();
            if (controller == null || controller.EnemyStatus != EnemyStatus.CheckingSecurity)
                continue;
            float dist = Vector2.Distance(guard.transform.position, pos);
            if (dist < bestDist)
            {
                best = guard;
                bestDist = dist;
            }
        }

        best?.ReactivateFactoryMachine(machine);
    }
    private void DispatchGuardForRestingMachine(RestingMachine machine)
    {
        Debug.Log($"Dispatching guard for machine: {machine.name}");
        if (machine == null || guards.Count == 0)
            return;

        ReactiveMachineAI best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;
        foreach (var guard in guards)
        {
            if (guard == null) continue;
            var controller = guard.GetComponent<EnemyController>();
            if (controller == null || controller.EnemyStatus != EnemyStatus.CheckingSecurity)
                continue;
            float dist = Vector2.Distance(guard.transform.position, pos);
            if (dist < bestDist)
            {
                best = guard;
                bestDist = dist;
            }
        }

        best?.ReactivateRestingMachine(machine);
    }
}

