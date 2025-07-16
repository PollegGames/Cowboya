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
    private readonly List<SecurityGuardAI> guards = new();

    /// <summary>
    /// Fired whenever a registered machine is switched off.
    /// </summary>
    public event Action<FactoryMachine> OnFactoryMachineTurnedOff;
    public event Action<RestingMachine> OnRestingMachineTurnedOff;

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
    public void RegisterGuard(SecurityGuardAI guard)
    {
        if (guard == null || guards.Contains(guard))
            return;
        Debug.Log($"Registering guard: {guard.name}");
        guards.Add(guard);
    }

    public void UnregisterGuard(SecurityGuardAI guard)
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

    private void DispatchGuardForFactoryMachine(FactoryMachine machine)
    {
        Debug.Log($"Dispatching guard for machine: {machine.name}");
        if (machine == null || guards.Count == 0)
            return;

        SecurityGuardAI best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;
        foreach (var guard in guards)
        {
            if (guard == null) continue;
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

        SecurityGuardAI best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;
        foreach (var guard in guards)
        {
            if (guard == null) continue;
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

