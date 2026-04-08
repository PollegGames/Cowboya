using System;
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

    // Memory holds contextual facts used to compute brain options.
    [SerializeField] private RobotMemoryNew memory;

    private BrainOption options;
    private RobotTask lastPlannedTask;
    public BrainOption CurrentOptions => options;

    private void Awake()
    {
        if (heart == null)
            heart = GetComponent<RobotHeartNew>();
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
    }

    private void UpdateMemoryState(MemoryChangeEvent e)
    {
        PublishPlan(e.Snapshot);
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
    }

    private BrainOption BuildOptions(RobotMemorySnapshotNew s)
    {
        BrainOption o = BrainOption.None;
        if (s.IsDead) o |= BrainOption.Dead;
        if (s.WasRecentlyAttacked) o |= BrainOption.InDanger;
        if (s.PlayerInAttackZone) o |= BrainOption.CanAttack;
        if (s.PlayerInDetectZone) o |= BrainOption.PlayerDetected;
        if (!s.IsConnectedToMachine) o |= BrainOption.NeedMachine;
        if (!HasAvailableWaypoint(s)) o |= BrainOption.MachineUnavailable;
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
                        return new RobotTask(RobotTaskType.Idle);

                    var waypoint = FindBestWaypointForRole(RobotRole.Worker, snapshot);
                    return waypoint != null
                        ? new RobotTask(RobotTaskType.GoToMachine, waypoint)
                        : new RobotTask(RobotTaskType.Idle);
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

                    var securityWaypoint = FindBestWaypointForRole(RobotRole.SecurityGuard, snapshot);
                    return securityWaypoint != null
                        ? new RobotTask(RobotTaskType.GoToMachine, securityWaypoint)
                        : new RobotTask(RobotTaskType.Idle);
                }
                return new RobotTask(RobotTaskType.Patrol);

            case RobotRole.WorkerSpawner:
                if (o.HasFlag(BrainOption.Dead))
                    return new RobotTask(RobotTaskType.Dead);
                if (o.HasFlag(BrainOption.InDanger))
                    return new RobotTask(RobotTaskType.Faint);
                if (o.HasFlag(BrainOption.NeedMachine))
                {
                    var spawnWaypoint = FindBestWaypointForRole(RobotRole.WorkerSpawner, snapshot);
                    return spawnWaypoint != null
                        ? new RobotTask(RobotTaskType.GoToMachine, spawnWaypoint)
                        : new RobotTask(RobotTaskType.Idle);
                }
                return new RobotTask(RobotTaskType.SpawnFollowers);

            case RobotRole.Follower:
                if (o.HasFlag(BrainOption.CanAttack))
                    return new RobotTask(RobotTaskType.AttackTarget, BuildPlayerPayload(snapshot));
                if (o.HasFlag(BrainOption.PlayerDetected))
                    return new RobotTask(RobotTaskType.ChasePlayer, BuildPlayerPayload(snapshot));
                if (snapshot.HasLastKnownPlayerPosition)
                    return new RobotTask(RobotTaskType.ChasePlayer, snapshot.LastKnownPlayerPosition);
                return new RobotTask(RobotTaskType.Idle);

            case RobotRole.Boss:
                if (o.HasFlag(BrainOption.CanAttack))
                    return new RobotTask(RobotTaskType.AttackTarget, BuildPlayerPayload(snapshot));
                if (o.HasFlag(BrainOption.PlayerDetected))
                    return new RobotTask(RobotTaskType.ChasePlayer, BuildPlayerPayload(snapshot));
                if (o.HasFlag(BrainOption.InDanger))
                    return new RobotTask(RobotTaskType.Flee);
                return new RobotTask(RobotTaskType.Patrol);

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static RoomWaypoint FindBestWaypointForRole(RobotRole role, RobotMemorySnapshotNew snapshot)
    {
        switch (role)
        {
            case RobotRole.Worker:
                return FindBestWaypointForWorker(snapshot);
            case RobotRole.SecurityGuard:
                return FindBestWaypointForGuard(snapshot);
            case RobotRole.WorkerSpawner:
                return FindBestWaypointForSpawner(snapshot);
            default:
                return null;
        }
    }

    private static RoomWaypoint FindBestWaypointForWorker(RobotMemorySnapshotNew snapshot)
    {
        // Priority: Work -> Rest -> Center (start room fallback).
        return FindByPriority(snapshot, WaypointType.Work, WaypointType.Rest, WaypointType.Center);
    }

    private static RoomWaypoint FindBestWaypointForGuard(RobotMemorySnapshotNew snapshot)
    {
        // Priority: Security -> Work/Rest (other machines) -> Center fallback.
        return FindByPriority(snapshot, WaypointType.Security, WaypointType.Work, WaypointType.Rest, WaypointType.Center);
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

        return FindByPriority(snapshot, WaypointType.Spawner);
    }

    private static RoomWaypoint FindByPriority(RobotMemorySnapshotNew snapshot, params WaypointType[] orderedTypes)
    {
        if (snapshot.AllAvailableWaypoints == null || snapshot.AllAvailableWaypoints.Count == 0)
            return null;

        foreach (var type in orderedTypes)
        {
            foreach (var pair in snapshot.AllAvailableWaypoints)
            {
                if (!pair.Value || pair.Key == null)
                    continue;

                if (pair.Key.type == type)
                    return pair.Key;
            }
        }

        return null;
    }

    private static object BuildPlayerPayload(RobotMemorySnapshotNew snapshot)
    {
        return snapshot.HasLastKnownPlayerPosition ? snapshot.LastKnownPlayerPosition : null;
    }

    private static bool IsSameTask(RobotTask left, RobotTask right)
    {
        if (left == null && right == null)
            return true;
        if (left == null || right == null)
            return false;

        return left.Type == right.Type && Equals(left.Payload, right.Payload);
    }
}
