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
    private readonly List<BaseMachine> pendingOffMachines = new();

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
        machine.InitializeSecurityManager(this);
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
            QueuePendingOffMachine(machine, "factory_power_off");
            TryDispatchPendingOffMachine("factory_power_off");
        }
        else
        {
            RemovePendingOffMachine(machine, "factory_power_on");
        }
    }

    private void HandleRestingMachineStateChanged(RestingMachine machine, bool isOn)
    {
        if (!isOn)
        {
            QueuePendingOffMachine(machine, "rest_power_off");
            TryDispatchPendingOffMachine("rest_power_off");
        }
        else
        {
            RemovePendingOffMachine(machine, "rest_power_on");
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

    /// <summary>
    /// Checks queued powered-off machines when a guard reaches and attaches to a security machine.
    /// </summary>
    public void HandleGuardConnectedToSecurityMachine(SecurityMachine securityMachine, RobotBrainNew guard)
    {
        if (securityMachine == null || guard == null)
            return;

        PruneResolvedPendingOffMachines("security_connect_prune");
        Debug.Log(
            $"[MachineSecurityManager] Guard connected to security machine guard={guard.name} securityMachine={securityMachine.name} pendingCount={pendingOffMachines.Count}",
            securityMachine);

        RobotEcosystemProbe.RecordBrainCall(
            guard,
            "MachineSecurityManager.SecurityMachineConnected",
            "securityPost=" + DescribeMachine(securityMachine)
            + " pendingCount=" + pendingOffMachines.Count
            + " currentTask=" + DescribeTask(guard.Heart != null ? guard.Heart.CurrentTask : null));

        if (TryDispatchPendingOffMachineToGuard(guard, "pending_power_off_security_connect"))
            return;

        Debug.Log(
            $"[MachineSecurityManager] No pending powered-off machine for connected guard={guard.name} securityMachine={securityMachine.name}",
            securityMachine);
    }

    private void QueuePendingOffMachine(BaseMachine machine, string reason)
    {
        if (machine == null || machine.IsOn)
            return;

        if (!pendingOffMachines.Contains(machine))
            pendingOffMachines.Add(machine);

        Debug.Log(
            $"[MachineSecurityManager] Pending powered-off machine queued reason={reason} machine={machine.name} pendingCount={pendingOffMachines.Count}",
            machine);
    }

    private void RemovePendingOffMachine(BaseMachine machine, string reason)
    {
        if (machine == null)
            return;

        if (!pendingOffMachines.Remove(machine))
            return;

        Debug.Log(
            $"[MachineSecurityManager] Pending powered-off machine removed reason={reason} machine={machine.name} pendingCount={pendingOffMachines.Count}",
            machine);
    }

    private void PruneResolvedPendingOffMachines(string reason)
    {
        for (int i = pendingOffMachines.Count - 1; i >= 0; i--)
        {
            var pending = pendingOffMachines[i];
            if (pending != null && !pending.IsOn)
                continue;

            pendingOffMachines.RemoveAt(i);
            Debug.Log(
                $"[MachineSecurityManager] Pending powered-off machine pruned reason={reason} machine={DescribeMachine(pending)} pendingCount={pendingOffMachines.Count}",
                pending);
        }
    }

    private bool TryDispatchPendingOffMachine(string reason)
    {
        PruneResolvedPendingOffMachines(reason + "_prune");
        BaseMachine machine = FindClosestPendingOffMachine(null);
        if (machine == null)
            return false;

        Debug.Log($"Dispatching guard for pending machine: {machine.name}");

        RobotBrainNew best = FindClosestStationedGuard(machine.transform.position, reason, machine);
        if (best == null)
        {
            Debug.Log(
                $"[MachineSecurityManager] No stationed guard available for pending machine={machine.name} reason={reason} pendingCount={pendingOffMachines.Count}",
                machine);
            return false;
        }

        pendingOffMachines.Remove(machine);
        DispatchGuardToReactivateMachine(best, machine, reason);
        return true;
    }

    private bool TryDispatchPendingOffMachineToGuard(RobotBrainNew guard, string reason)
    {
        if (guard == null)
            return false;

        PruneResolvedPendingOffMachines(reason + "_prune");
        BaseMachine machine = FindClosestPendingOffMachine(guard.transform.position);
        if (machine == null)
            return false;

        pendingOffMachines.Remove(machine);
        Debug.Log(
            $"[MachineSecurityManager] Dispatching connected guard to pending machine guard={guard.name} reason={reason} machine={machine.name} pendingCount={pendingOffMachines.Count}",
            machine);
        DispatchGuardToReactivateMachine(guard, machine, reason);
        return true;
    }

    private BaseMachine FindClosestPendingOffMachine(Vector3? position)
    {
        BaseMachine best = null;
        float bestDist = float.MaxValue;

        foreach (var pending in pendingOffMachines)
        {
            if (pending == null || pending.IsOn)
                continue;

            if (!position.HasValue)
                return pending;

            float dist = Vector2.Distance(position.Value, pending.transform.position);
            if (dist < bestDist)
            {
                best = pending;
                bestDist = dist;
            }
        }

        return best;
    }

    private bool DispatchGuardForSecurityMachine(SecurityMachine machine, RobotBrainNew skipGuard)
    {
        if (machine == null || guards.Count == 0) return false;
        Debug.Log($"Dispatching guard for security machine: {machine.name}");

        RobotBrainNew best = null;

        foreach (var guard in guards)
        {
            if (guard == null) continue;
            if (!IsGuardStationedAtSecurityMachine(guard)) continue;
            if (skipGuard != null && ReferenceEquals(guard, skipGuard))
            {
                continue;
            }

            best = SelectCloserGuard(best, guard, machine.transform.position);
        }

        if (best == null && skipGuard != null)
            best = skipGuard;

        if (best == null)
            return false;

        DispatchGuardToReactivateMachine(best, machine, "security_power_off");
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

    private RobotBrainNew FindClosestStationedGuard(Vector3 position, string reason, BaseMachine targetMachine)
    {
        RobotBrainNew best = null;

        foreach (var guard in guards)
        {
            if (guard == null)
                continue;

            bool stationed = IsGuardStationedAtSecurityMachine(guard);
            RobotEcosystemProbe.RecordBrainCall(
                guard,
                "MachineSecurityManager.EvaluateGuardDispatch",
                "reason=" + reason
                + " target=" + DescribeMachine(targetMachine)
                + " stationed=" + stationed
                + " currentTask=" + DescribeTask(guard.Heart != null ? guard.Heart.CurrentTask : null)
                + " lastVisited=" + DescribeWaypoint(guard.Memory != null ? guard.Memory.LastVisitedPoint : null)
                + " securityPost=" + DescribeMachine(FindSecurityMachineForGuard(guard)));

            if (!stationed)
                continue;

            best = SelectCloserGuard(best, guard, position);
        }

        return best;
    }

    private static RobotBrainNew SelectCloserGuard(RobotBrainNew currentBest, RobotBrainNew candidate, Vector3 position)
    {
        if (candidate == null)
            return currentBest;

        if (currentBest == null)
            return candidate;

        float candidateDistance = Vector2.Distance(candidate.transform.position, position);
        float bestDistance = Vector2.Distance(currentBest.transform.position, position);
        return candidateDistance < bestDistance ? candidate : currentBest;
    }

    private void DispatchGuardToReactivateMachine(RobotBrainNew guard, BaseMachine machine, string reason)
    {
        if (guard == null || machine == null)
            return;

        RoomWaypoint waypoint = MachineWaypointResolver.Resolve(machine);
        SecurityMachine securityPost = FindSecurityMachineForGuard(guard);
        if (securityPost != null)
            securityPost.VacateGuard(guard);

        RobotTask reactivateTask = new RobotTask(RobotTaskType.ReactivateMachine, machine);
        RobotEcosystemProbe.RecordBrainCall(
            guard,
            "MachineSecurityManager.DispatchGuardToReactivateMachine",
            "reason=" + reason
            + " machine=" + DescribeMachine(machine)
            + " waypoint=" + DescribeWaypoint(waypoint)
            + " vacatedSecurityPost=" + DescribeMachine(securityPost)
            + " queuedTask=" + DescribeTask(reactivateTask));

        RobotNewTrace.Log(
            guard,
            eventSource: "MachineSecurityManager.DispatchGuard",
            memoryDelta: "ReactivationAssigned",
            brainOptions: guard.CurrentOptions,
            plannedTask: reactivateTask,
            heartCurrentTask: guard.Heart != null ? guard.Heart.CurrentTask : null,
            taskSignal: "security_dispatch:" + reason);

        if (guard.Memory == null)
        {
            Debug.LogWarning(
                $"[MachineSecurityManager] Cannot dispatch reactivation through Memory -> Brain -> Heart -> Task because guard={guard.name} has no {nameof(RobotMemoryNew)}.",
                guard);
            return;
        }

        guard.Memory.AssignReactivationMachine(machine);
    }

    private bool IsGuardStationedAtSecurityMachine(RobotBrainNew guard)
    {
        if (guard == null)
            return false;

        if (FindSecurityMachineForGuard(guard) != null)
            return true;

        RobotTask currentTask = guard.Heart != null ? guard.Heart.CurrentTask : null;
        if (currentTask == null || currentTask.Type != RobotTaskType.GuardPost)
            return false;

        RoomWaypoint lastVisited = guard.Memory != null ? guard.Memory.LastVisitedPoint : null;
        if (lastVisited != null && lastVisited.type == WaypointType.Security)
            return true;

        RoomWaypoint bodyTarget = guard.Body != null ? guard.Body.CurrentTarget : null;
        return bodyTarget != null && bodyTarget.type == WaypointType.Security;
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

    private SecurityMachine FindSecurityMachineForGuard(RobotBrainNew guard)
    {
        if (guard == null)
            return null;

        foreach (var machine in securityMachines)
        {
            if (machine == null)
                continue;
            if (ReferenceEquals(machine.CurrentGuard, guard))
                return machine;
        }

        return null;
    }

    private static string DescribeMachine(BaseMachine machine)
    {
        if (machine == null)
            return "none";

        return machine.name + ":" + machine.Type + ":on=" + machine.IsOn;
    }

    private static string DescribeWaypoint(RoomWaypoint waypoint)
    {
        if (waypoint == null)
            return "none";

        return waypoint.type + "@" + waypoint.WorldPos.ToString("F2");
    }

    private static string DescribeTask(RobotTask task)
    {
        if (task == null)
            return "none";

        return task.Type + ":" + DescribePayload(task.Payload);
    }

    private static string DescribePayload(object payload)
    {
        if (payload == null)
            return "none";

        if (payload is BaseMachine machine)
            return DescribeMachine(machine);

        if (payload is RoomWaypoint waypoint)
            return DescribeWaypoint(waypoint);

        if (payload is Component component && component != null)
            return component.name;

        if (payload is GameObject go && go != null)
            return go.name;

        return payload.ToString();
    }
}

