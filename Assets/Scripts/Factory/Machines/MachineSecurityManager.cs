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
    private readonly List<SpawningMachine> spawningMachines = new();
    private readonly List<BaseMachine> pendingOffMachines = new();

    private readonly List<RobotBrainNew> guards = new();
    private readonly Dictionary<RobotBrainNew, SecurityMachine> guardPosts = new();
    private RobotBrainNew activeReactivationGuard;
    private BaseMachine activeReactivationMachine;

    public bool HasActiveReactivationJob => activeReactivationMachine != null;
    public int PendingReactivationJobCount => pendingOffMachines.Count;

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

    public void RegisterSpawningMachine(SpawningMachine machine)
    {
        if (machine == null || spawningMachines.Contains(machine)) return;

        spawningMachines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.SecurityGuard);
        machine.OnMachinePowerChanged += HandleMachinePowerChanged;
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

        foreach (var machine in spawningMachines)
        {
            if (machine != null)
                machine.OnMachinePowerChanged -= HandleMachinePowerChanged;
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
        guardPosts.Remove(guard);
        if (ReferenceEquals(activeReactivationGuard, guard))
            RequeueActiveReactivationJob("guard_unregistered");
    }

    private void HandleSecurityMachineTurningOff(SecurityMachine machine, RobotBrainNew currentGuard)
    {
        if (machine == null)
            return;
        if (currentGuard != null)
            guardPosts[currentGuard] = machine;

        QueuePendingOffMachine(machine, "security_power_off");
        TryDispatchPendingOffMachine("security_power_off");
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
        else if (evt.Machine is SpawningMachine spawningMachine)
            HandleSpawningMachineStateChanged(spawningMachine, evt.IsOn);
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
            HandleReactivationTargetPoweredOn(machine, "factory_power_on");
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
            HandleReactivationTargetPoweredOn(machine, "rest_power_on");
        }
    }

    private void HandleSecurityMachineStateChanged(SecurityMachine machine, bool isOn)
    {
        if (!isOn)
        {
            QueuePendingOffMachine(machine, "security_power_off");
            TryDispatchPendingOffMachine("security_power_off");
        }
        else
        {
            HandleReactivationTargetPoweredOn(machine, "security_power_on");
            if (reservationService == null)
                TryAssignClosestRestingGuard(machine);
        }
    }

    private void HandleSpawningMachineStateChanged(SpawningMachine machine, bool isOn)
    {
        if (!isOn)
        {
            QueuePendingOffMachine(machine, "spawning_power_off");
            TryDispatchPendingOffMachine("spawning_power_off");
        }
        else
        {
            HandleReactivationTargetPoweredOn(machine, "spawning_power_on");
        }
    }

    /// <summary>
    /// Checks queued powered-off machines when a guard reaches and attaches to a security machine.
    /// </summary>
    public void HandleGuardConnectedToSecurityMachine(SecurityMachine securityMachine, RobotBrainNew guard)
    {
        if (securityMachine == null || guard == null)
            return;

        bool returningFromReactivation = ReferenceEquals(activeReactivationGuard, guard);
        if (returningFromReactivation
            && guardPosts.TryGetValue(guard, out SecurityMachine assignedPost)
            && assignedPost != null
            && !ReferenceEquals(assignedPost, securityMachine))
        {
            ReturnGuardToAssignedPost(guard, "wrong_security_post");
            return;
        }

        guardPosts[guard] = securityMachine;
        if (returningFromReactivation)
        {
            activeReactivationGuard = null;
            activeReactivationMachine = null;
        }

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
        if (HasActiveReactivationJob)
            return false;

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
        if (guard == null || HasActiveReactivationJob)
            return false;

        PruneResolvedPendingOffMachines(reason + "_prune");
        BaseMachine machine = FindClosestPendingOffMachine(guard.transform.position, guard);
        if (machine == null)
            return false;

        pendingOffMachines.Remove(machine);
        Debug.Log(
            $"[MachineSecurityManager] Dispatching connected guard to pending machine guard={guard.name} reason={reason} machine={machine.name} pendingCount={pendingOffMachines.Count}",
            machine);
        DispatchGuardToReactivateMachine(guard, machine, reason);
        return true;
    }

    private BaseMachine FindClosestPendingOffMachine(Vector3? position, RobotBrainNew guard = null)
    {
        BaseMachine best = null;
        float bestDist = float.MaxValue;

        foreach (var pending in pendingOffMachines)
        {
            if (pending == null || pending.IsOn)
                continue;
            if (!CanGuardReactivate(guard, pending))
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
                MachineType.SecurityMachine,
                guard,
                "security_post") as SecurityMachine;
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
        HandleReactivationTargetPoweredOn(machine, "machine_powered_on");

        if (machine is SecurityMachine security)
            TryAssignClosestRestingGuard(security);
    }

    private void HandleReactivationTargetPoweredOn(BaseMachine machine, string reason)
    {
        RemovePendingOffMachine(machine, reason);
    }

    /// <summary>
    /// Returns a repair guard to its assigned Security machine when it is powered on.
    /// Otherwise, releases the job and sends the guard through the normal Rest/Start fallback.
    /// </summary>
    public bool ReturnGuardToAssignedPost(RobotBrainNew guard, string reason = "reactivation_completed")
    {
        if (guard == null || !ReferenceEquals(activeReactivationGuard, guard))
            return false;
        if (!guardPosts.TryGetValue(guard, out SecurityMachine assignedPost) || assignedPost == null)
            return false;

        if (!assignedPost.IsOn)
        {
            activeReactivationGuard = null;
            activeReactivationMachine = null;
            guard.Memory?.SetMachineWaypointAvailability(assignedPost, false);
            PublishGuardDispatch(guard, null, false);
            // Dispatch events can synchronously publish a plan. Keep Rest as the
            // final intent; the Brain falls back to Start when no powered Rest exists.
            guard.Memory?.SetDesiredMachineType(MachineType.RestStation);
            RobotEcosystemProbe.RecordBrainCall(
                guard,
                "MachineSecurityManager.ReturnGuardToRestOrStart",
                "reason=" + reason + " securityPost=" + DescribeMachine(assignedPost));
            TryDispatchPendingOffMachine("assigned_security_post_off");
            return true;
        }

        RobotEcosystemProbe.RecordBrainCall(
            guard,
            "MachineSecurityManager.ReturnGuardToAssignedPost",
            "reason=" + reason + " securityPost=" + DescribeMachine(assignedPost));
        PublishGuardDispatch(guard, assignedPost, assignedPost.IsOn);
        // Dispatch events can synchronously update Memory and publish a new plan.
        // Make the return-to-post intent the final fact written by the manager.
        guard.Memory?.SetDesiredMachineType(MachineType.SecurityMachine);
        return true;
    }

    private bool TryAssignClosestRestingGuard(SecurityMachine machine)
    {
        if (machine == null || !machine.IsOn || machine.IsOccupied)
            return false;

        bool reserved = false;
        if (reservationService != null)
        {
            reserved = reservationService.TryReserveStation(
                machine,
                RobotRole.SecurityGuard,
                owner: null,
                purpose: "security_post_dispatch");
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
            if (!CanGuardReactivate(guard, targetMachine))
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
            QueuePendingOffMachine(machine, "dispatch_missing_memory");
            return;
        }

        activeReactivationGuard = guard;
        activeReactivationMachine = machine;
        guard.Memory.AssignReactivationMachine(machine);
    }

    /// <summary>Checks that a guard is not assigned to reactivate its own security post.</summary>
    public bool CanGuardReactivate(RobotBrainNew guard, BaseMachine targetMachine)
    {
        if (targetMachine == null)
            return false;
        if (guard == null)
            return true;

        return !ReferenceEquals(FindSecurityMachineForGuard(guard), targetMachine);
    }

    private void RequeueActiveReactivationJob(string reason)
    {
        BaseMachine interruptedMachine = activeReactivationMachine;
        activeReactivationMachine = null;
        activeReactivationGuard = null;
        if (interruptedMachine != null && !interruptedMachine.IsOn)
            QueuePendingOffMachine(interruptedMachine, reason);
    }

    private bool IsGuardStationedAtSecurityMachine(RobotBrainNew guard)
    {
        if (guard == null)
            return false;

        SecurityMachine assignedPost = FindSecurityMachineForGuard(guard);
        return assignedPost != null
            && assignedPost.IsOn
            && ReferenceEquals(assignedPost.CurrentGuard, guard)
            && guard.Memory != null
            && guard.Memory.Snapshot.IsConnectedToMachine;
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

        if (guardPosts.TryGetValue(guard, out SecurityMachine assignedPost) && assignedPost != null)
            return assignedPost;

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

