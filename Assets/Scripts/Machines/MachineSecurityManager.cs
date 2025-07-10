using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Notifies security guards when a factory machine turns off.
/// Guards can subscribe to <see cref="OnMachineTurnedOff"/> to react.
/// </summary>
public class MachineSecurityManager : MonoBehaviour
{
    private readonly List<FactoryMachine> machines = new();

    /// <summary>
    /// Fired whenever a registered machine is switched off.
    /// </summary>
    public event Action<FactoryMachine> OnMachineTurnedOff;

    public void RegisterMachine(FactoryMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;
        machines.Add(machine);
        machine.OnMachineStateChanged += HandleMachineStateChanged;
    }

    private void HandleMachineStateChanged(FactoryMachine machine, bool isOn)
    {
        if (!isOn)
            OnMachineTurnedOff?.Invoke(machine);
    }
}

