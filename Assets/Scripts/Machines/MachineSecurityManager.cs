using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Notifies security guards when a factory machine turns off.
/// Guards can subscribe to <see cref="OnMachineTurnedOff"/> to react.
/// </summary>
public class MachineSecurityManager : MonoBehaviour
{
    [SerializeField] private StationReservationService reservationService;
    private readonly List<FactoryMachine> machines = new();
    private readonly List<SecurityGuardAI> guards = new();

    /// <summary>
    /// Fired whenever a registered machine is switched off.
    /// </summary>
    public event Action<FactoryMachine> OnMachineTurnedOff;

    public void RegisterMachine(FactoryMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;
        machines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachineStateChanged += HandleMachineStateChanged;
    }

    public void RegisterGuard(SecurityGuardAI guard)
    {
        if (guard == null || guards.Contains(guard))
            return;
        guards.Add(guard);
    }

    public void UnregisterGuard(SecurityGuardAI guard)
    {
        if (guard == null)
            return;
        guards.Remove(guard);
    }

    private void HandleMachineStateChanged(FactoryMachine machine, bool isOn)
    {
        if (!isOn)
        {
            OnMachineTurnedOff?.Invoke(machine);
            DispatchGuard(machine);
        }
    }

    private void DispatchGuard(FactoryMachine machine)
    {
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

        best?.ReactivateMachine(machine);
    }
}

