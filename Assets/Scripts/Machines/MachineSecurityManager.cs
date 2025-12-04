using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Notifies security guards when a factory machine turns off.
/// Guards can subscribe to <see cref="OnFactoryMachineTurnedOff"/> to react.
/// Also raises <see cref="OnAllMachinesOff"/> once EVERY registered machine is OFF.
/// </summary>
public class MachineSecurityManager : MonoBehaviour
{
    [SerializeField] private StationReservationService reservationService;

    private readonly List<FactoryMachine> factoryMachines = new();
    private readonly List<RestingMachine> restingMachines = new();
    private readonly List<SecurityMachine> securityMachines = new();

    // Tracks *currently ON* machines (any type)
    private readonly HashSet<MonoBehaviour> machinesOn = new();

    private readonly List<RobotBrain> guards = new();

    public event Action OnAllMachinesOff;

    /// <summary> Fired whenever a registered machine is switched off. </summary>
    public event Action<FactoryMachine> OnFactoryMachineTurnedOff;
    public event Action<RestingMachine> OnRestingMachineTurnedOff;
    public event Action<SecurityMachine> OnSecurityMachineTurnedOff;

    public void RegisterFactoryMachine(FactoryMachine machine)
    {
        if (machine == null || factoryMachines.Contains(machine)) return;

        factoryMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachineStateChanged += HandleFactoryMachineStateChanged;

        // seed initial state if available
        if (IsOn(machine)) machinesOn.Add(machine); else machinesOn.Remove(machine);
    }

    public void RegisterRestingMachine(RestingMachine machine)
    {
        if (machine == null || restingMachines.Contains(machine)) return;

        restingMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachineStateChanged += HandleRestingMachineStateChanged;

        if (IsOn(machine)) machinesOn.Add(machine); else machinesOn.Remove(machine);
    }

    public void RegisterSecurityMachine(SecurityMachine machine)
    {
        if (machine == null || securityMachines.Contains(machine)) return;

        securityMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachineStateChanged += HandleSecurityMachineStateChanged;

        if (IsOn(machine)) machinesOn.Add(machine); else machinesOn.Remove(machine);
    }

    public void RegisterGuard(RobotBrain guard)
    {
        if (guard == null || guards.Contains(guard)) return;
        Debug.Log($"Registering guard: {guard.name}");
        guards.Add(guard);
    }

    public void UnregisterGuard(RobotBrain guard)
    {
        if (guard == null) return;
        guards.Remove(guard);
    }

    private void HandleFactoryMachineStateChanged(FactoryMachine machine, bool isOn)
    {
        UpdateOnSet(machine, isOn);

        if (!isOn)
        {
            OnFactoryMachineTurnedOff?.Invoke(machine);
            DispatchGuardForFactoryMachine(machine);
        }

        CheckAllMachinesOff();
    }

    private void HandleRestingMachineStateChanged(RestingMachine machine, bool isOn)
    {
        UpdateOnSet(machine, isOn);

        if (!isOn)
        {
            OnRestingMachineTurnedOff?.Invoke(machine);
            DispatchGuardForRestingMachine(machine);
        }

        CheckAllMachinesOff();
    }

    private void HandleSecurityMachineStateChanged(SecurityMachine machine, bool isOn)
    {
        UpdateOnSet(machine, isOn);

        if (!isOn)
        {
            OnSecurityMachineTurnedOff?.Invoke(machine);
        }

        CheckAllMachinesOff();
    }

    private void UpdateOnSet(MonoBehaviour machine, bool isOn)
    {
        if (machine == null) return;
        if (isOn) machinesOn.Add(machine);
        else machinesOn.Remove(machine);

        // cleanup destroyed refs
        machinesOn.RemoveWhere(m => m == null);
    }

    private void DispatchGuardForFactoryMachine(FactoryMachine machine)
    {
        Debug.Log($"Dispatching guard for machine: {machine.name}");
        if (machine == null || guards.Count == 0) return;

        RobotBrain best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;

        foreach (var guard in guards)
        {
            if (guard == null) continue;
            if (guard == null)
                continue;

            float dist = Vector2.Distance(guard.transform.position, pos);
            if (dist < bestDist)
            {
                best = guard;
                bestDist = dist;
            }
        }

        if (best == null)
            return;

        PushBrainIntent(best, RobotTaskType.ReactivateMachine, machine, false);
    }

    private void DispatchGuardForRestingMachine(RestingMachine machine)
    {
        Debug.Log($"Dispatching guard for machine: {machine.name}");
        if (machine == null || guards.Count == 0) return;

        RobotBrain best = null;
        float bestDist = float.MaxValue;
        var pos = machine.transform.position;

        foreach (var guard in guards)
        {
            if (guard == null) continue;
            if (guard == null)
                continue;
            float dist = Vector2.Distance(guard.transform.position, pos);
            if (dist < bestDist)
            {
                best = guard;
                bestDist = dist;
            }
        }

        if (best == null)
            return;

        PushBrainIntent(best, RobotTaskType.ReactivateMachine, machine, false);
    }

    /// <summary>
    /// Call this to (re)count states from scratch (e.g., after all registrations).
    /// </summary>
    public void RecountAndMaybeTrigger()
    {
        machinesOn.Clear();

        foreach (var m in factoryMachines) if (IsOn(m)) machinesOn.Add(m);
        foreach (var m in restingMachines) if (IsOn(m)) machinesOn.Add(m);
        foreach (var m in securityMachines) if (IsOn(m)) machinesOn.Add(m);

        CheckAllMachinesOff();
    }

    private void CheckAllMachinesOff()
    {
        // consider 'all off' only if at least one machine is registered
        int registeredCount = factoryMachines.Count + restingMachines.Count + securityMachines.Count;
        if (registeredCount == 0) return;

        // purge any destroyed references to be safe
        machinesOn.RemoveWhere(m => m == null);

        if (machinesOn.Count == 0)
        {
            // avoid duplicate raises
            if (!allOffRaised)
            {
                allOffRaised = true;
                TriggerAllMachinesOff();
            }
        }
        else
        {
            // reset latch when any machine turns back on
            allOffRaised = false;
        }
    }

    // Call this method when all machines are off
    public void TriggerAllMachinesOff()
    {
        Debug.Log("[MachineSecurityManager] All machines are OFF -> broadcasting OnAllMachinesOff");
        OnAllMachinesOff?.Invoke();
    }

    private bool allOffRaised = false;

    private static bool IsOn(MonoBehaviour machine)
    {
        if (machine == null) return false;

        // Prefer a common base type if your machines inherit from it
        // (uncomment if you have such a type)
        // if (machine is BaseMachine bm) return bm.IsOn; // or bm.isOn if public

        // Fallback to reflection to support either a public property 'IsOn'
        // or a field named 'isOn' (protected/private). Keeps this class decoupled.
        var t = machine.GetType();
        var prop = t.GetProperty("IsOn");
        if (prop != null && prop.PropertyType == typeof(bool))
        {
            try { return (bool)prop.GetValue(machine); } catch { }
        }
        var field = t.GetField("isOn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
        if (field != null && field.FieldType == typeof(bool))
        {
            try { return (bool)field.GetValue(machine); } catch { }
        }
        return false;
    }

    private static void PushBrainIntent(RobotBrain guard, RobotTaskType taskType, object payload, bool isOn)
    {
        if (guard == null)
            return;
        guard.OnMachineStateChanged(payload, isOn);
    }
}
