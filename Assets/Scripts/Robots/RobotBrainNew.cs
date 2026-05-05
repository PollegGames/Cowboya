using System;
using System.Collections.Generic;
using UnityEngine;


[Flags]
public enum BrainOption
{
    None = 0,
    Dead = 1 << 0,
    InDanger = 1 << 1,
    CanAttack = 1 << 2,
    PlayerDetected = 1 << 3,
    NeedMachine = 1 << 4,
    MachineUnavailable = 1 << 5
}

/// <summary>
/// Brain mediates between perception/events and the Heart intent stack,
/// applying a role config to decide which task to surface.
/// </summary>
[RequireComponent(typeof(RobotHeartNew))]
public class RobotBrainNew : MonoBehaviour
{
    public event Action<BrainOption> UpdateBrainOption;
    public event Action<RobotTask> UpdatePlannedTask;

    [SerializeField] private RobotHeartNew heart;
    [SerializeField] private RobotBodyController body;

    // Memory holds contextual facts used to compute brain options.
    [SerializeField] private RobotMemoryNew memory;

    private BrainOption options;
    private RobotTask lastPlannedTask;
    public BrainOption CurrentOptions => options;
    public RobotHeartNew Heart => heart;
    public RobotBodyController Body => body;
    public RobotMemoryNew Memory => memory;
    public bool IsSecurityGuard => heart != null && heart.Role == RobotRole.SecurityGuard;

    private void Awake()
    {
        if (heart == null)
            heart = GetComponent<RobotHeartNew>();
        if (body == null)
            body = GetComponent<RobotBodyController>();
        if (memory == null)
            memory = GetComponent<RobotMemoryNew>();
    }

    private void OnEnable()
    {
        if (memory != null)
        {
            memory.OnMemoryChanged += UpdateMemoryState;
            PublishPlan(memory.Snapshot);
        }
    }

    private void OnDisable()
    {
        if (memory != null)
            memory.OnMemoryChanged -= UpdateMemoryState;
        RobotMachineDestinationBalancer.ReleaseRobot(gameObject.GetInstanceID());
    }

    private void UpdateMemoryState(MemoryChangeEvent e)
    {
        PublishPlan(e.Snapshot);
    }

    public void OnPerceptionChanged(
        bool playerInDetectZone,
        bool playerInAttackZone,
        Vector3 playerPosition,
        bool hasKnownPosition = true,
        Transform playerTransform = null,
        RoomWaypoint playerWaypoint = null)
    {
        if (!RobotNewPipelineRuntime.IsNewPipelineActive || memory == null)
            return;

        RobotEcosystemProbe.RecordBrainCall(
            this,
            "OnPerceptionChanged",
            "detect=" + playerInDetectZone
            + " attack=" + playerInAttackZone
            + " knownPos=" + hasKnownPosition
            + " playerRef=" + (playerTransform != null ? playerTransform.name : "null")
            + " playerWaypoint=" + DescribeWaypoint(playerWaypoint));

        if (playerWaypoint != null)
            memory.RememberPlayerWaypoint(playerWaypoint, playerPosition);
        else if (playerTransform != null)
            memory.RememberPlayerTransform(playerTransform);
        else if (hasKnownPosition)
            memory.RememberPlayerPosition(playerPosition);

        memory.SetPlayerInDetectZone(playerInDetectZone);
        memory.SetPlayerInAttackZone(playerInAttackZone);

        if (!hasKnownPosition && playerTransform == null && !playerInDetectZone && !playerInAttackZone)
            memory.ClearPlayerPosition();
    }

    public void OnDamageTaken(int damage)
    {
        OnDamageTaken(damage, null);
    }

    public void OnDamageTaken(int damage, Vector3? attackerPosition)
    {
        _ = damage;
        if (!RobotNewPipelineRuntime.IsNewPipelineActive || memory == null)
            return;

        RobotEcosystemProbe.RecordBrainCall(
            this,
            "OnDamageTaken",
            "damage=" + damage
            + " attackerPosition=" + (attackerPosition.HasValue ? attackerPosition.Value.ToString("F2") : "unknown"));

        if (attackerPosition.HasValue)
            memory.RegisterAttack(attackerPosition.Value);
        else
            memory.RegisterAttack();
    }

    /// <summary>
    /// Backward-compatible bridge for legacy trigger handlers.
    /// </summary>
    public void OnPlayerInAttackZoneChanged(bool inZone, Transform playerReference)
    {
        Vector3 playerPosition = playerReference != null ? playerReference.position : Vector3.zero;
        bool playerDetected = memory != null && memory.PlayerInDetectZone;
        OnPerceptionChanged(playerDetected, inZone, playerPosition, hasKnownPosition: playerReference != null, playerTransform: playerReference);
    }

    public void OnMachineStateEvent(object machinePayload, bool isOn)
    {
        if (!RobotNewPipelineRuntime.IsNewPipelineActive || memory == null)
            return;

        RobotEcosystemProbe.RecordBrainCall(
            this,
            "OnMachineStateEvent",
            "payload=" + DescribePayload(machinePayload) + " isOn=" + isOn);

        RoomWaypoint waypoint = ResolveWaypoint(machinePayload);
        if (waypoint != null)
            memory.SetRoomWaypointAvailability(waypoint, isOn);

        if (!isOn)
            memory.ChangeConnectionToMachine(false);
    }

    public void OnSecurityDispatch(object machinePayload)
    {
        if (!RobotNewPipelineRuntime.IsNewPipelineActive || memory == null)
            return;

        RobotEcosystemProbe.RecordBrainCall(
            this,
            "OnSecurityDispatch",
            "payload=" + DescribePayload(machinePayload));

        RoomWaypoint waypoint = ResolveWaypoint(machinePayload);
        if (waypoint != null)
            memory.SetLastVisitedPoint(waypoint);
    }

    public bool CanUseMachineSlot(BaseMachine machine, RobotTaskType slotTaskType)
    {
        _ = machine;
        if (heart == null || heart.CurrentTask == null)
            return false;
        return heart.CurrentTask.Type == slotTaskType;
    }

    public void CompleteReactivateTask(BaseMachine machine, bool reached)
    {
        _ = reached;
        MachineType? nextDesiredMachineType = IsSecurityGuard
            && machine != null
            && machine.Type != MachineType.SecurityMachine
            ? MachineType.SecurityMachine
            : null;

        heart?.CompleteCurrentTask();

        if (memory != null && machine != null)
        {
            Debug.Log(
                $"[RobotBrainNew] Reactivation task completed machine={machine.name} nextDesired={(nextDesiredMachineType.HasValue ? nextDesiredMachineType.Value.ToString() : "none")}",
                this);
            memory.NotifyReactivationCompleted(machine, nextDesiredMachineType);
        }
    }

    public bool TryGetCurrentPlan(out BrainOption currentOptions, out RobotTask plannedTask)
    {
        if (memory == null)
        {
            currentOptions = BrainOption.None;
            plannedTask = null;
            return false;
        }

        var snapshot = memory.Snapshot;
        currentOptions = BuildOptions(snapshot);
        plannedTask = BuildTaskFromOptions(currentOptions, snapshot);
        return true;
    }

    private void PublishPlan(RobotMemorySnapshotNew snapshot)
    {
        var nextOptions = BuildOptions(snapshot);
        if (!nextOptions.HasFlag(BrainOption.NeedMachine))
            RobotMachineDestinationBalancer.ReleaseRobot(gameObject.GetInstanceID());

        if (nextOptions != options)
        {
            options = nextOptions;
            UpdateBrainOption?.Invoke(options);
        }

        var nextTask = BuildTaskFromOptions(nextOptions, snapshot);
        if (IsSameTask(nextTask, lastPlannedTask))
            return;

        lastPlannedTask = nextTask;
        if (nextTask != null)
            UpdatePlannedTask?.Invoke(nextTask);

        RobotNewTrace.Log(
            this,
            eventSource: "BrainNew.PublishPlan",
            memoryDelta: "snapshot",
            brainOptions: nextOptions,
            plannedTask: nextTask,
            heartCurrentTask: heart != null ? heart.CurrentTask : null,
            taskSignal: "plan");
    }

    private BrainOption BuildOptions(RobotMemorySnapshotNew s)
    {
        BrainOption o = BrainOption.None;
        if (s.IsDead) o |= BrainOption.Dead;
        if (s.WasRecentlyAttacked) o |= BrainOption.InDanger;
        if (s.PlayerInAttackZone) o |= BrainOption.CanAttack;
        if (s.PlayerInDetectZone) o |= BrainOption.PlayerDetected;
        if (ShouldNeedMachine(s)) o |= BrainOption.NeedMachine;
        bool machineUnavailable = heart != null && heart.Role == RobotRole.Worker
            ? !HasAnyWorkerMachineWaypoint(s)
            : !HasAvailableWaypoint(s);
        if (machineUnavailable) o |= BrainOption.MachineUnavailable;
        return o;
    }

    private static bool HasAvailableWaypoint(RobotMemorySnapshotNew s)
    {
        if (s.AllAvailableWaypoints == null || s.AllAvailableWaypoints.Count == 0)
            return false;

        foreach (var entry in s.AllAvailableWaypoints)
        {
            if (entry.Value)
                return true;
        }

        return false;
    }

    private static bool HasAnyWorkerMachineWaypoint(RobotMemorySnapshotNew s)
    {
        if (s.AllAvailableWaypoints == null || s.AllAvailableWaypoints.Count == 0)
            return false;

        foreach (var entry in s.AllAvailableWaypoints)
        {
            if (entry.Key == null)
                continue;

            if (!entry.Value)
                continue;

            if (entry.Key.type == WaypointType.Work || entry.Key.type == WaypointType.Rest)
                return true;
        }

        return false;
    }

    private static bool ShouldNeedMachine(RobotMemorySnapshotNew s)
    {
        if (s.IsConnectedToMachine)
            return false;

        // If a desired machine is known, we must keep planning toward it even while
        // a transition flag is active (handoff/replacement path).
        if (s.DesiredMachineType.HasValue)
            return true;

        return !s.IsMachineTransitionInProgress;
    }

    private RobotTask BuildTaskFromOptions(BrainOption o, RobotMemorySnapshotNew snapshot)
    {
        if (o.HasFlag(BrainOption.Dead))
            return new RobotTask(RobotTaskType.Dead);

        if (heart == null)
            throw new InvalidOperationException($"{nameof(RobotHeartNew)} is required to resolve role in {nameof(RobotBrainNew)}.");

        switch (heart.Role)
        {
            case RobotRole.Worker:
                if (o.HasFlag(BrainOption.InDanger))
                    return new RobotTask(RobotTaskType.Flee);

                if (o.HasFlag(BrainOption.NeedMachine))
                {
                    if (o.HasFlag(BrainOption.MachineUnavailable))
                        return new RobotTask(RobotTaskType.ReturnHome, ResolveStartWaypoint());

                    int robotId = gameObject.GetInstanceID();
                    var waypoint = FindBestWaypointForRole(RobotRole.Worker, snapshot, robotId);
                    if (waypoint == null)
                        return new RobotTask(RobotTaskType.ReturnHome, ResolveStartWaypoint());

                    RobotMachineDestinationBalancer.AssignDestination(robotId, waypoint);
                    return new RobotTask(RobotTaskType.GoToMachine, waypoint);
                }

                if (snapshot.LastVisitedPoint != null)
                {
                    if (snapshot.LastVisitedPoint.type == WaypointType.Work)
                        return new RobotTask(RobotTaskType.WorkAtMachine, snapshot.LastVisitedPoint);
                    else if (snapshot.LastVisitedPoint.type == WaypointType.Rest)
                        return new RobotTask(RobotTaskType.Rest, snapshot.LastVisitedPoint);
                    else
                    {
                        return new RobotTask(RobotTaskType.SearchForMachine);
                    }
                }

                return new RobotTask(RobotTaskType.WorkAtMachine);

            case RobotRole.SecurityGuard:
                if (o.HasFlag(BrainOption.CanAttack))
                    return new RobotTask(RobotTaskType.AttackTarget, BuildPlayerPayload(snapshot));
                if (o.HasFlag(BrainOption.PlayerDetected))
                    return new RobotTask(RobotTaskType.ChasePlayer, BuildPlayerPayload(snapshot));
                if (o.HasFlag(BrainOption.InDanger))
                    return new RobotTask(RobotTaskType.Flee);
                if (o.HasFlag(BrainOption.NeedMachine))
                {
                    if (o.HasFlag(BrainOption.MachineUnavailable))
                        return new RobotTask(RobotTaskType.Idle);

                    var securityWaypoint = FindBestWaypointForRole(RobotRole.SecurityGuard, snapshot, gameObject.GetInstanceID());
                    if (securityWaypoint == null)
                        return new RobotTask(RobotTaskType.Idle);

                    if (IsBalancedMachineWaypointType(securityWaypoint.type))
                        RobotMachineDestinationBalancer.AssignDestination(gameObject.GetInstanceID(), securityWaypoint);
                    return new RobotTask(RobotTaskType.GoToMachine, securityWaypoint);
                }
                if (snapshot.LastVisitedPoint != null)
                {
                    if (snapshot.LastVisitedPoint.type == WaypointType.Security)
                        return new RobotTask(RobotTaskType.GuardPost, snapshot.LastVisitedPoint);
                    if (snapshot.LastVisitedPoint.type == WaypointType.Rest)
                        return new RobotTask(RobotTaskType.Rest, snapshot.LastVisitedPoint);
                }
                return new RobotTask(RobotTaskType.GuardPost);

            case RobotRole.WorkerSpawner:
                if (o.HasFlag(BrainOption.Dead))
                    return new RobotTask(RobotTaskType.Dead);
                if (o.HasFlag(BrainOption.InDanger))
                    return new RobotTask(RobotTaskType.Faint);
                if (o.HasFlag(BrainOption.NeedMachine))
                {
                    var spawnWaypoint = FindBestWaypointForRole(RobotRole.WorkerSpawner, snapshot, gameObject.GetInstanceID());
                    return spawnWaypoint != null
                        ? new RobotTask(RobotTaskType.GoToMachine, spawnWaypoint)
                        : new RobotTask(RobotTaskType.Idle);
                }
                return new RobotTask(RobotTaskType.SpawnFollowers);

            case RobotRole.Follower:
                if (o.HasFlag(BrainOption.PlayerDetected) || o.HasFlag(BrainOption.CanAttack))
                    return new RobotTask(RobotTaskType.ChasePlayer, BuildFollowerPlayerPayload(snapshot));
                if (snapshot.HasLastKnownPlayerPosition)
                    return new RobotTask(RobotTaskType.ChasePlayer, BuildFollowerPlayerPayload(snapshot));
                return new RobotTask(RobotTaskType.Idle);

            case RobotRole.Boss:
                if (o.HasFlag(BrainOption.CanAttack))
                {
                    Debug.Log(
                        $"[Boss] Planning attack target={DescribePayload(BuildPlayerPayload(snapshot))}.",
                        this);
                    return new RobotTask(RobotTaskType.AttackTarget, BuildPlayerPayload(snapshot));
                }
                if (o.HasFlag(BrainOption.PlayerDetected))
                {
                    Debug.Log("[Boss] Player detected outside attack state; staying on end-room patrol instead of chasing.", this);
                    return new RobotTask(RobotTaskType.Patrol, FindRandomEndRoomWaypoint(snapshot));
                }
                if (o.HasFlag(BrainOption.InDanger))
                {
                    Debug.Log("[Boss] Damage/danger detected; staying on end-room patrol instead of fleeing.", this);
                    return new RobotTask(RobotTaskType.Patrol, FindRandomEndRoomWaypoint(snapshot));
                }
                return new RobotTask(RobotTaskType.Patrol, FindRandomEndRoomWaypoint(snapshot));

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static RoomWaypoint FindBestWaypointForRole(RobotRole role, RobotMemorySnapshotNew snapshot, int robotId)
    {
        switch (role)
        {
            case RobotRole.Worker:
                return FindBestWaypointForWorker(snapshot, robotId);
            case RobotRole.SecurityGuard:
                return FindBestWaypointForGuard(snapshot, robotId);
            case RobotRole.WorkerSpawner:
                return FindBestWaypointForSpawner(snapshot);
            default:
                return null;
        }
    }

    private static RoomWaypoint FindBestWaypointForWorker(RobotMemorySnapshotNew snapshot, int robotId)
    {
        if (snapshot.DesiredMachineType.HasValue)
        {
            var desired = FindByDesiredMachineTypeBalanced(snapshot, snapshot.DesiredMachineType.Value, availableOnly: true, robotId);
            if (desired != null)
                return desired;

            var fallback = FindFallbackWaypointForUnavailableDesiredMachine(snapshot, snapshot.DesiredMachineType.Value, robotId);
            if (fallback != null)
                return fallback;
        }

        // Cycle preference: after Work go to Rest, after Rest go to Work.
        if (snapshot.LastVisitedPoint != null)
        {
            if (snapshot.LastVisitedPoint.type == WaypointType.Work)
                return FindByPriorityBalanced(snapshot, availableOnly: true, robotId, WaypointType.Rest, WaypointType.Work);
            if (snapshot.LastVisitedPoint.type == WaypointType.Rest)
                return FindByPriorityBalanced(snapshot, availableOnly: true, robotId, WaypointType.Work, WaypointType.Rest);
        }

        return FindByPriorityBalanced(snapshot, availableOnly: true, robotId, WaypointType.Work, WaypointType.Rest);
    }

    private static RoomWaypoint FindFallbackWaypointForUnavailableDesiredMachine(
        RobotMemorySnapshotNew snapshot,
        MachineType desiredMachineType,
        int robotId)
    {
        switch (desiredMachineType)
        {
            case MachineType.WorkStation:
                return FindByPriorityBalanced(snapshot, availableOnly: true, robotId, WaypointType.Rest);
            case MachineType.RestStation:
                return FindByPriorityBalanced(snapshot, availableOnly: true, robotId, WaypointType.Work);
            default:
                return null;
        }
    }

    private static RoomWaypoint FindByDesiredMachineTypeBalanced(RobotMemorySnapshotNew snapshot, MachineType desiredMachineType, bool availableOnly, int robotId)
    {
        WaypointType? desiredWaypointType = null;
        switch (desiredMachineType)
        {
            case MachineType.WorkStation:
                desiredWaypointType = WaypointType.Work;
                break;
            case MachineType.RestStation:
                desiredWaypointType = WaypointType.Rest;
                break;
            case MachineType.SecurityMachine:
                desiredWaypointType = WaypointType.Security;
                break;
        }

        if (!desiredWaypointType.HasValue)
            return null;

        return FindByPriorityBalanced(snapshot, availableOnly, robotId, desiredWaypointType.Value);
    }

    private static RoomWaypoint FindByPriorityBalanced(RobotMemorySnapshotNew snapshot, bool availableOnly, int robotId, params WaypointType[] orderedTypes)
    {
        if (snapshot.AllAvailableWaypoints == null || snapshot.AllAvailableWaypoints.Count == 0)
            return null;

        var candidates = new List<RoomWaypoint>();
        foreach (WaypointType type in orderedTypes)
        {
            candidates.Clear();
            foreach (var pair in snapshot.AllAvailableWaypoints)
            {
                if (pair.Key == null)
                    continue;
                if (availableOnly && !pair.Value)
                    continue;
                if (pair.Key.type != type)
                    continue;

                candidates.Add(pair.Key);
            }

            if (!IsBalancedMachineWaypointType(type))
            {
                if (candidates.Count > 0)
                    return candidates[0];
                continue;
            }

            RoomWaypoint selected = RobotMachineDestinationBalancer.SelectLeastTargeted(candidates, robotId);
            if (selected != null)
                return selected;
        }

        return null;
    }

    private static RoomWaypoint FindBestWaypointForGuard(RobotMemorySnapshotNew snapshot, int robotId)
    {
        if (snapshot.DesiredMachineType.HasValue)
        {
            var desired = FindByDesiredMachineTypeBalanced(snapshot, snapshot.DesiredMachineType.Value, availableOnly: true, robotId);
            if (desired != null)
                return desired;
        }

        if (snapshot.LastVisitedPoint != null && snapshot.LastVisitedPoint.type == WaypointType.Rest)
            return FindByPriorityBalanced(snapshot, availableOnly: true, robotId, WaypointType.Rest, WaypointType.Security, WaypointType.Center);

        // Security guards only use security/rest stations during normal machine cycles.
        return FindByPriorityBalanced(snapshot, availableOnly: true, robotId, WaypointType.Security, WaypointType.Rest, WaypointType.Center);
    }

    private static bool IsBalancedMachineWaypointType(WaypointType type)
    {
        return type == WaypointType.Work
            || type == WaypointType.Rest
            || type == WaypointType.Security;
    }

    private static RoomWaypoint FindBestWaypointForSpawner(RobotMemorySnapshotNew snapshot)
    {
        // Priority: original spawner point if still available.
        var last = snapshot.LastVisitedPoint;
        if (last != null
            && last.type == WaypointType.Spawner
            && snapshot.AllAvailableWaypoints != null
            && snapshot.AllAvailableWaypoints.TryGetValue(last, out bool isAvailable)
            && isAvailable)
        {
            return last;
        }

        return FindByPriority(snapshot, availableOnly: true, WaypointType.Spawner);
    }

    private static RoomWaypoint FindRandomEndRoomWaypoint(RobotMemorySnapshotNew snapshot)
    {
        if (snapshot.AllAvailableWaypoints == null || snapshot.AllAvailableWaypoints.Count == 0)
            return null;

        var candidates = new List<RoomWaypoint>();
        foreach (var pair in snapshot.AllAvailableWaypoints)
        {
            var waypoint = pair.Key;
            if (waypoint == null || waypoint.parentRoom == null || waypoint.parentRoom.roomProperties == null)
                continue;
            if (waypoint.parentRoom.roomProperties.usageType != UsageType.End)
                continue;
            if (waypoint.type != WaypointType.Center)
                continue;

            candidates.Add(waypoint);
        }

        if (candidates.Count == 0)
            return null;

        if (snapshot.LastVisitedPoint != null && candidates.Count > 1)
            candidates.Remove(snapshot.LastVisitedPoint);

        return candidates[UnityEngine.Random.Range(0, candidates.Count)];
    }

    private static RoomWaypoint FindByPriority(RobotMemorySnapshotNew snapshot, bool availableOnly, params WaypointType[] orderedTypes)
    {
        if (snapshot.AllAvailableWaypoints == null || snapshot.AllAvailableWaypoints.Count == 0)
            return null;

        foreach (var type in orderedTypes)
        {
            foreach (var pair in snapshot.AllAvailableWaypoints)
            {
                if (pair.Key == null)
                    continue;
                if (availableOnly && !pair.Value)
                    continue;

                if (pair.Key.type == type)
                    return pair.Key;
            }
        }

        return null;
    }

    private static object BuildPlayerPayload(RobotMemorySnapshotNew snapshot)
    {
        if (snapshot.LastKnownPlayerTransform != null)
            return snapshot.LastKnownPlayerTransform;

        return snapshot.HasLastKnownPlayerPosition ? snapshot.LastKnownPlayerPosition : null;
    }

    private static object BuildFollowerPlayerPayload(RobotMemorySnapshotNew snapshot)
    {
        if (snapshot.LastKnownPlayerWaypoint != null)
        {
            return new RobotPlayerChaseTarget(
                snapshot.LastKnownPlayerWaypoint,
                snapshot.LastKnownPlayerPosition,
                snapshot.HasLastKnownPlayerPosition);
        }

        return snapshot.HasLastKnownPlayerPosition ? snapshot.LastKnownPlayerPosition : null;
    }

    private RoomWaypoint ResolveStartWaypoint()
    {
        return body != null ? body.StartPoint : null;
    }

    private static bool IsSameTask(RobotTask left, RobotTask right)
    {
        if (left == null && right == null)
            return true;
        if (left == null || right == null)
            return false;

        return left.Type == right.Type && Equals(left.Payload, right.Payload);
    }

    private static RoomWaypoint ResolveWaypoint(object payload)
    {
        if (payload is RoomWaypoint waypoint)
            return waypoint;

        if (payload is BaseMachine machine && machine != null)
            return MachineWaypointResolver.Resolve(machine);

        if (payload is Component component && component != null)
            return component.GetComponent<RoomWaypoint>() ?? component.GetComponentInParent<RoomWaypoint>();

        if (payload is GameObject gameObject && gameObject != null)
            return gameObject.GetComponent<RoomWaypoint>() ?? gameObject.GetComponentInParent<RoomWaypoint>();

        return null;
    }

    private static string DescribePayload(object payload)
    {
        if (payload == null)
            return "null";
        if (payload is RoomWaypoint waypoint && waypoint != null)
            return "RoomWaypoint:" + waypoint.type;
        if (payload is BaseMachine machine && machine != null)
            return "BaseMachine:" + machine.name;
        if (payload is Component component && component != null)
            return "Component:" + component.name;
        if (payload is GameObject gameObject && gameObject != null)
            return "GameObject:" + gameObject.name;
        return payload.GetType().Name;
    }

    private static string DescribeWaypoint(RoomWaypoint waypoint)
    {
        return waypoint != null ? waypoint.type + "@" + waypoint.WorldPos.ToString("F2") : "null";
    }
}
