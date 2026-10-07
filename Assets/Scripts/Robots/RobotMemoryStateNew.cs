using System;
using System.Collections.Generic;
using UnityEngine;


// RobotMemoryStateNew.cs
public enum MemoryChangeType
{
    PlayerAttackZoneChanged,
    PlayerDetectZoneChanged,
    PlayerWaypointChanged,
    TookDamage,
    LastVisitedPointChanged,
    WaypointAvailabilityChanged,
    ConnectedToMachine,
    NotConnectedToMachine,
    MachineDetachedTransient,
    MachineDetachedFinal,
    ReactivationCompleted,
    ReactivationAssigned,
    DeadStateChanged,
    DesiredMachineChanged,
    Normal,
    CollectorMissionAssigned,
    CollectorLaunchChanged,
    CollectorTargetChanged,
    CollectorCargoChanged,
    CollectorDockChanged,
    CollectorTargetInvalidated,
    CollectorFlightFaultChanged,
    CollectorMissionCleared,
    WorkerCollectorMissionAssigned,
    WorkerCollectorTargetChanged,
    WorkerCollectorCargoChanged,
    WorkerCollectorTargetInvalidated,
    WorkerCollectorDropOffChanged,
    WorkerCollectorDeliveryChanged,
    WorkerCollectorBatchCompleted,
    WorkerCollectorRestChanged,
    WorkerCollectorMissionCleared,
    WorkerCollectorSourceChanged,
    WorkerCollectorGarageAvailabilityChanged
}

public struct MemoryChangeEvent
{
    public MemoryChangeType Type;
    public RobotMemorySnapshotNew Snapshot;
}

/// <summary>
/// Plain data container for the Memory pillar to store factual observations.
/// </summary>
public class RobotMemoryStateNew
{
    public event Action<MemoryChangeEvent> OnChanged;
    private RobotMemorySnapshotNew snapshot = new RobotMemorySnapshotNew
    {
        AllAvailableWaypoints = new Dictionary<RoomWaypoint, bool>()
    };

    public RobotMemorySnapshotNew Snapshot => snapshot;

    public Vector3 LastKnownPlayerPosition => snapshot.LastKnownPlayerPosition;
    public Transform LastKnownPlayerTransform => snapshot.LastKnownPlayerTransform;
    public RoomWaypoint LastKnownPlayerWaypoint => snapshot.LastKnownPlayerWaypoint;
    public bool PlayerInAttackZone => snapshot.PlayerInAttackZone;
    public bool PlayerInDetectZone => snapshot.PlayerInDetectZone;
    public bool WasRecentlyAttacked => snapshot.WasRecentlyAttacked;
    public Vector3 LastAttackPosition => snapshot.LastAttackPosition;
    public bool HasLastAttackPosition => snapshot.HasLastAttackPosition;
    public bool IsConnectedToMachine => snapshot.IsConnectedToMachine;
    public bool IsDead => snapshot.IsDead;
    public RoomWaypoint LastVisitedPoint => snapshot.LastVisitedPoint;
    public MachineType? DesiredMachineType => snapshot.DesiredMachineType;
    public BaseMachine PendingReactivationMachine => snapshot.PendingReactivationMachine;

    // roomwaypoint and the bool is if the waypoint is no more accessible
    // for exemple if machine off, if a waypoint is in a blocked room, if already is already there, etc..
    public Dictionary<RoomWaypoint, bool> AllAvailableWaypoints => snapshot.AllAvailableWaypoints;
    public void RememberPlayerPosition(Vector3 position)
    {
        snapshot.LastKnownPlayerPosition = position;
        snapshot.HasLastKnownPlayerPosition = true;
        Raise(MemoryChangeType.Normal);
    }

    public void RememberPlayerTransform(Transform playerTransform)
    {
        if (playerTransform == null)
            return;

        snapshot.LastKnownPlayerTransform = playerTransform;
        snapshot.LastKnownPlayerPosition = playerTransform.position;
        snapshot.HasLastKnownPlayerPosition = true;
        Raise(MemoryChangeType.Normal);
    }

    public void RememberPlayerWaypoint(RoomWaypoint playerWaypoint, Vector3 playerPosition)
    {
        if (playerWaypoint == null)
            return;

        bool waypointChanged = snapshot.LastKnownPlayerWaypoint != playerWaypoint;
        snapshot.LastKnownPlayerWaypoint = playerWaypoint;
        snapshot.LastKnownPlayerPosition = playerPosition;
        snapshot.HasLastKnownPlayerPosition = true;
        Raise(waypointChanged ? MemoryChangeType.PlayerWaypointChanged : MemoryChangeType.Normal);
    }

    public void ClearPlayerPosition()
    {
        if (!snapshot.HasLastKnownPlayerPosition)
            return;

        snapshot.lastKnownPlayerPosition = Vector3.zero;
        snapshot.LastKnownPlayerTransform = null;
        snapshot.LastKnownPlayerWaypoint = null;
        snapshot.HasLastKnownPlayerPosition = false;
        Raise(MemoryChangeType.Normal);
    }

    public void ChangeConnectionToMachine(bool isConnected)
    {
        if (snapshot.IsConnectedToMachine == isConnected) return;
        snapshot.IsConnectedToMachine = isConnected;
        if (isConnected)
            snapshot.IsMachineTransitionInProgress = false;
        Raise(isConnected ? MemoryChangeType.ConnectedToMachine : MemoryChangeType.NotConnectedToMachine);
    }

    public void NotifyMachineSlotAttached(RoomWaypoint point)
    {
        bool pointChanged = point != null && snapshot.LastVisitedPoint != point;
        bool connectionChanged = !snapshot.IsConnectedToMachine;
        bool desiredChanged = snapshot.DesiredMachineType.HasValue;
        if (point != null)
            snapshot.LastVisitedPoint = point;
        snapshot.IsConnectedToMachine = true;
        snapshot.DesiredMachineType = null;
        snapshot.IsMachineTransitionInProgress = false;

        if (connectionChanged)
            Raise(MemoryChangeType.ConnectedToMachine);
        else if (desiredChanged)
            Raise(MemoryChangeType.DesiredMachineChanged);
        else if (pointChanged)
            Raise(MemoryChangeType.LastVisitedPointChanged);
        else
            Raise(MemoryChangeType.Normal);
    }

    public void NotifyMachineSlotReleased()
    {
        NotifyMachineSlotReleasedFinal();
    }

    public void NotifyMachineSlotReleasedTransient()
    {
        bool connectionChanged = snapshot.IsConnectedToMachine;
        bool transitionChanged = !snapshot.IsMachineTransitionInProgress;
        snapshot.IsConnectedToMachine = false;
        snapshot.IsMachineTransitionInProgress = true;

        if (connectionChanged || transitionChanged)
            Raise(MemoryChangeType.MachineDetachedTransient);
        else
            Raise(MemoryChangeType.Normal);
    }

    public void NotifyMachineSlotReleasedFinal()
    {
        bool connectionChanged = snapshot.IsConnectedToMachine;
        bool desiredChanged = snapshot.DesiredMachineType.HasValue;
        bool transitionChanged = snapshot.IsMachineTransitionInProgress;
        snapshot.IsConnectedToMachine = false;
        snapshot.DesiredMachineType = null;
        snapshot.IsMachineTransitionInProgress = false;

        if (connectionChanged)
            Raise(MemoryChangeType.MachineDetachedFinal);
        else if (transitionChanged)
            Raise(MemoryChangeType.MachineDetachedFinal);
        else if (desiredChanged)
            Raise(MemoryChangeType.DesiredMachineChanged);
        else
            Raise(MemoryChangeType.Normal);
    }

    public void SetDesiredMachineType(MachineType? machineType)
    {
        if (snapshot.DesiredMachineType == machineType)
            return;

        snapshot.DesiredMachineType = machineType;
        if (machineType.HasValue && !snapshot.IsConnectedToMachine)
            snapshot.IsMachineTransitionInProgress = true;
        if (!machineType.HasValue && !snapshot.IsConnectedToMachine)
            snapshot.IsMachineTransitionInProgress = false;
        Raise(MemoryChangeType.DesiredMachineChanged);
    }

    public void AssignReactivationMachine(BaseMachine machine)
    {
        if (snapshot.PendingReactivationMachine == machine)
            return;

        snapshot.PendingReactivationMachine = machine;
        Raise(MemoryChangeType.ReactivationAssigned);
    }

    public void SetDead(bool isDead)
    {
        if (snapshot.IsDead == isDead) return;
        snapshot.IsDead = isDead;
        Raise(MemoryChangeType.DeadStateChanged);
    }

    public void RegisterAttack()
    {
        RegisterAttack(null);
    }

    public void RegisterAttack(Vector3 attackerPosition)
    {
        RegisterAttack((Vector3?)attackerPosition);
    }

    private void RegisterAttack(Vector3? attackerPosition)
    {
        bool alreadyAttacked = snapshot.WasRecentlyAttacked;
        bool attackPositionChanged = false;

        if (attackerPosition.HasValue)
        {
            attackPositionChanged = !snapshot.HasLastAttackPosition
                || snapshot.LastAttackPosition != attackerPosition.Value;
            snapshot.LastAttackPosition = attackerPosition.Value;
            snapshot.HasLastAttackPosition = true;
        }

        if (alreadyAttacked && !attackPositionChanged) return;
        snapshot.WasRecentlyAttacked = true;
        Raise(MemoryChangeType.TookDamage);
    }

    public void ResetAttackMemory()
    {
        if (!snapshot.WasRecentlyAttacked) return;
        snapshot.WasRecentlyAttacked = false;
        snapshot.LastAttackPosition = Vector3.zero;
        snapshot.HasLastAttackPosition = false;
        Raise(MemoryChangeType.Normal);
    }

    public void SetLastVisitedPoint(RoomWaypoint point)
    {
        if (snapshot.LastVisitedPoint == point) return;
        snapshot.LastVisitedPoint = point;
        Raise(MemoryChangeType.LastVisitedPointChanged);
    }

    public void SetPlayerInAttackZone(bool inZone)
    {
        if (snapshot.PlayerInAttackZone == inZone) return;
        snapshot.PlayerInAttackZone = inZone;
        Raise(MemoryChangeType.PlayerAttackZoneChanged);
    }
    public void SetRoomWaypointAvailability(RoomWaypoint point, bool isAvailable)
    {
        if (point == null)
            return;

        if (snapshot.AllAvailableWaypoints == null)
            snapshot.AllAvailableWaypoints = new Dictionary<RoomWaypoint, bool>();
        if (snapshot.AllAvailableWaypoints.TryGetValue(point, out bool current) && current == isAvailable) return;
        snapshot.AllAvailableWaypoints[point] = isAvailable;
        Raise(MemoryChangeType.WaypointAvailabilityChanged);
    }

    public void NotifyReactivationCompleted(RoomWaypoint point, MachineType? nextDesiredMachineType, bool connectedToReactivatedMachine)
    {
        if (point != null)
        {
            if (snapshot.AllAvailableWaypoints == null)
                snapshot.AllAvailableWaypoints = new Dictionary<RoomWaypoint, bool>();
            snapshot.AllAvailableWaypoints[point] = true;
        }

        snapshot.IsConnectedToMachine = connectedToReactivatedMachine;
        snapshot.DesiredMachineType = nextDesiredMachineType;
        snapshot.IsMachineTransitionInProgress = nextDesiredMachineType.HasValue && !connectedToReactivatedMachine;
        snapshot.PendingReactivationMachine = null;

        Raise(MemoryChangeType.ReactivationCompleted);
    }

    public void SetPlayerInDetectZone(bool inZone)
    {
        if (snapshot.PlayerInDetectZone == inZone) return;
        snapshot.PlayerInDetectZone = inZone;
        Raise(MemoryChangeType.PlayerDetectZoneChanged);
    }

    public void ReplaceWaypointAvailability(IEnumerable<RoomWaypoint> waypoints)
    {
        if (snapshot.AllAvailableWaypoints == null)
            snapshot.AllAvailableWaypoints = new Dictionary<RoomWaypoint, bool>();
        else
            snapshot.AllAvailableWaypoints.Clear();

        if (waypoints != null)
        {
            foreach (var waypoint in waypoints)
            {
                if (waypoint == null)
                    continue;
                snapshot.AllAvailableWaypoints[waypoint] = waypoint.IsAvailable;
            }
        }

        Raise(MemoryChangeType.WaypointAvailabilityChanged);
    }

    /// <summary>
    /// Remembers a Worker Collector route, optionally with a cube already claimed.
    /// </summary>
    public bool TryAssignWorkerCollectorMission(WorkerCollectorMissionAssignment assignment) {
        if (assignment == null || !assignment.HasRequiredReferences
            || snapshot.WorkerCollector.Assignment != null
            || (assignment.Target != null && !assignment.Target.IsClaimValid(assignment.Claim))
            || (assignment.Target == null && assignment.Claim.IsValid))
            return false;

        snapshot.WorkerCollector = new WorkerCollectorMissionFacts {
            Assignment = assignment,
            GarageAvailable = true
        };
        Raise(MemoryChangeType.WorkerCollectorMissionAssigned);
        return true;
    }

    /// <summary>
    /// Records a nearby claimed cube without changing the remembered route or mission identity.
    /// </summary>
    public bool TryAcquireWorkerCollectorTarget(
        WorkerCollectorMissionAssignment sourceAssignment,
        WorkerCollectorMissionAssignment claimedAssignment) {
        WorkerCollectorMissionFacts facts = snapshot.WorkerCollector;
        if (sourceAssignment == null || claimedAssignment == null
            || !ReferenceEquals(facts.Assignment, sourceAssignment)
            || sourceAssignment.HasClaimedTarget || !facts.SourceApproachReached
            || !claimedAssignment.HasRequiredReferences || !claimedAssignment.HasClaimedTarget
            || !claimedAssignment.Target.IsClaimValid(claimedAssignment.Claim)
            || claimedAssignment.MissionId != sourceAssignment.MissionId
            || claimedAssignment.Source != sourceAssignment.Source
            || claimedAssignment.DropOff != sourceAssignment.DropOff
            || claimedAssignment.Rest != sourceAssignment.Rest)
            return false;

        facts.Assignment = claimedAssignment;
        facts.TargetApproachReached = true;
        facts.GarageAvailable = true;
        snapshot.WorkerCollector = facts;
        Raise(MemoryChangeType.WorkerCollectorTargetChanged);
        return true;
    }

    /// <summary>
    /// Starts the next collection attempt on the same remembered route.
    /// </summary>
    public bool TryResetWorkerCollectorMission(
        WorkerCollectorMissionAssignment assignment, bool sourceApproachReached = false) {
        if (assignment == null || !assignment.HasRequiredReferences
            || !ReferenceEquals(snapshot.WorkerCollector.Assignment, assignment)
            || !assignment.Source.isActiveAndEnabled || !assignment.DropOff.isActiveAndEnabled)
            return false;

        snapshot.WorkerCollector = new WorkerCollectorMissionFacts {
            Assignment = new WorkerCollectorMissionAssignment(
                assignment.MissionId, assignment.Source, assignment.DropOff, assignment.Rest),
            SourceApproachReached = sourceApproachReached,
            GarageAvailable = true
        };
        Raise(MemoryChangeType.WorkerCollectorMissionAssigned);
        return true;
    }

    /// <summary>
    /// Applies one assignment-scoped observation from the Worker Collector body.
    /// </summary>
    public bool TryApplyWorkerCollectorObservation(WorkerCollectorBodyObservation observation)
    {
        WorkerCollectorMissionFacts facts = snapshot.WorkerCollector;
        if (observation.Assignment == null || observation.CommandToken <= 0
            || !ReferenceEquals(facts.Assignment, observation.Assignment))
            return false;

        switch (observation.Type)
        {
            case WorkerCollectorBodyObservationType.SourceApproachChanged:
                if (facts.Assignment.HasClaimedTarget || facts.SourceApproachReached == observation.Value)
                    return false;
                facts.SourceApproachReached = observation.Value;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorSourceChanged);
                return true;

            case WorkerCollectorBodyObservationType.GarageAvailabilityChanged:
                if (!facts.CargoSecured || facts.GarageAvailable == observation.Value)
                    return false;
                facts.GarageAvailable = observation.Value;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorGarageAvailabilityChanged);
                return true;

            case WorkerCollectorBodyObservationType.TargetApproachChanged:
                if (!HasValidWorkerCollectorClaim() || facts.TargetApproachReached == observation.Value)
                    return false;
                facts.TargetApproachReached = observation.Value;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorTargetChanged);
                return true;

            case WorkerCollectorBodyObservationType.CargoChanged:
                if (!HasValidWorkerCollectorClaim() || facts.CargoSecured == observation.Value)
                    return false;
                facts.CargoSecured = observation.Value;
                facts.CargoLost = !observation.Value;
                if (observation.Value)
                    facts.SourceApproachReached = false;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorCargoChanged);
                return true;

            case WorkerCollectorBodyObservationType.TargetUnavailable:
                if (facts.TargetUnavailable)
                    return false;
                facts.TargetUnavailable = true;
                facts.CargoSecured = false;
                facts.CargoLost = true;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorTargetInvalidated);
                return true;

            case WorkerCollectorBodyObservationType.DropOffApproachChanged:
                if (!facts.CargoSecured || facts.DropOffApproachReached == observation.Value)
                    return false;
                facts.DropOffApproachReached = observation.Value;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorDropOffChanged);
                return true;

            case WorkerCollectorBodyObservationType.DeliveryAccepted:
                if (facts.DeliveryAccepted)
                    return false;
                facts.DeliveryAccepted = true;
                facts.WaitingForBatch = observation.Value;
                facts.CargoSecured = false;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorDeliveryChanged);
                return true;

            case WorkerCollectorBodyObservationType.BatchCompleted:
                if (!facts.WaitingForBatch || facts.BatchCompleted)
                    return false;
                facts.BatchCompleted = true;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorBatchCompleted);
                return true;

            case WorkerCollectorBodyObservationType.BatchInterrupted:
                if (!facts.WaitingForBatch || facts.BatchCompleted)
                    return false;
                facts.WaitingForBatch = false;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorDeliveryChanged);
                return true;

            case WorkerCollectorBodyObservationType.RestApproachChanged:
                if (!facts.BatchCompleted || facts.RestApproachReached)
                    return false;
                facts.RestApproachReached = true;
                facts.RestUntil = Time.time + (facts.Assignment.Rest != null
                    ? facts.Assignment.Rest.RestDuration : 0f);
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorRestChanged);
                return true;

            case WorkerCollectorBodyObservationType.RestCompleted:
                if (!facts.RestApproachReached || facts.RestCompleted)
                    return false;
                facts.RestCompleted = true;
                snapshot.WorkerCollector = facts;
                Raise(MemoryChangeType.WorkerCollectorRestChanged);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Clears only the matching Worker Collector assignment.
    /// </summary>
    public bool TryClearWorkerCollectorMission(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment == null || !ReferenceEquals(snapshot.WorkerCollector.Assignment, assignment))
            return false;
        snapshot.WorkerCollector = default;
        Raise(MemoryChangeType.WorkerCollectorMissionCleared);
        return true;
    }

    private bool HasValidWorkerCollectorClaim()
    {
        WorkerCollectorMissionAssignment assignment = snapshot.WorkerCollector.Assignment;
        return assignment != null && assignment.Target != null
            && assignment.Target.IsClaimValid(assignment.Claim);
    }

    /// <summary>
    /// Atomically installs a new Collector mission and clears all progress from the previous mission.
    /// </summary>
    public bool TryAssignCollectorMission(CollectorMissionAssignment assignment)
    {
        if (assignment == null || !assignment.HasRequiredReferences)
            return false;
        if (!assignment.Target.IsClaimValid(assignment.Claim))
            return false;
        if (ReferenceEquals(snapshot.Collector.Assignment, assignment))
            return false;

        snapshot.Collector = new CollectorMissionFacts
        {
            Assignment = assignment
        };
        Raise(MemoryChangeType.CollectorMissionAssigned);
        return true;
    }

    /// <summary>
    /// Applies one discrete, assignment-scoped observation from the Collector body.
    /// </summary>
    public bool TryApplyCollectorObservation(CollectorBodyObservation observation)
    {
        if (!IsCurrentCollectorAssignment(observation.Assignment) || observation.CommandToken <= 0)
            return false;

        switch (observation.Type)
        {
            case CollectorBodyObservationType.LaunchExitChanged:
                if (snapshot.Collector.LaunchExitReached == observation.Value)
                    return false;
                snapshot.Collector.LaunchExitReached = observation.Value;
                Raise(MemoryChangeType.CollectorLaunchChanged);
                return true;

            case CollectorBodyObservationType.TargetApproachChanged:
                if (!HasValidCollectorClaim(observation.Assignment))
                    return false;
                if (snapshot.Collector.TargetApproachReached == observation.Value)
                    return false;
                snapshot.Collector.TargetApproachReached = observation.Value;
                Raise(MemoryChangeType.CollectorTargetChanged);
                return true;

            case CollectorBodyObservationType.CargoChanged:
                return TryApplyCollectorCargoObservation(observation);

            case CollectorBodyObservationType.DockApproachChanged:
                if (snapshot.Collector.DockApproachReached == observation.Value)
                    return false;
                snapshot.Collector.DockApproachReached = observation.Value;
                if (!observation.Value)
                    snapshot.Collector.DockAccessGranted = false;
                Raise(MemoryChangeType.CollectorDockChanged);
                return true;

            case CollectorBodyObservationType.FlightFaultChanged:
                if (snapshot.Collector.FlightFault == observation.Value)
                    return false;
                snapshot.Collector.FlightFault = observation.Value;
                Raise(MemoryChangeType.CollectorFlightFaultChanged);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Records whether the owning machine currently grants access to its Collector intake.
    /// </summary>
    public bool TrySetCollectorDockAccess(CollectorMissionAssignment assignment, bool granted)
    {
        if (!IsCurrentCollectorAssignment(assignment))
            return false;
        if (granted && !snapshot.Collector.DockApproachReached)
            return false;
        if (snapshot.Collector.DockAccessGranted == granted)
            return false;

        snapshot.Collector.DockAccessGranted = granted;
        Raise(MemoryChangeType.CollectorDockChanged);
        return true;
    }

    /// <summary>
    /// Confirms an intake transaction after validating the current mission and its success/abort state.
    /// </summary>
    public bool TryConfirmCollectorIntake(CollectorMissionAssignment assignment)
    {
        if (!IsCurrentCollectorAssignment(assignment)
            || snapshot.Collector.IntakeConfirmed
            || !snapshot.Collector.DockAccessGranted)
        {
            return false;
        }

        bool aborting = snapshot.Collector.TargetUnavailable
            || snapshot.Collector.MissionCancelled
            || snapshot.Collector.FlightFault;
        if (!aborting && (!snapshot.Collector.CargoSecure || !HasValidCollectorClaim(assignment)))
            return false;

        snapshot.Collector.IntakeConfirmed = true;
        Raise(MemoryChangeType.CollectorDockChanged);
        return true;
    }

    /// <summary>
    /// Invalidates the current target without mutating or destroying the target object.
    /// </summary>
    public bool TryInvalidateCollectorTarget(CollectorMissionAssignment assignment)
    {
        if (!IsCurrentCollectorAssignment(assignment) || snapshot.Collector.TargetUnavailable)
            return false;

        snapshot.Collector.TargetUnavailable = true;
        Raise(MemoryChangeType.CollectorTargetInvalidated);
        return true;
    }

    /// <summary>
    /// Marks the current Collector mission as externally cancelled.
    /// </summary>
    public bool TryCancelCollectorMission(CollectorMissionAssignment assignment)
    {
        if (!IsCurrentCollectorAssignment(assignment) || snapshot.Collector.MissionCancelled)
            return false;

        snapshot.Collector.MissionCancelled = true;
        Raise(MemoryChangeType.CollectorTargetInvalidated);
        return true;
    }

    /// <summary>
    /// Clears the matching Collector mission. A stale assignment cannot clear a newer pooled mission.
    /// </summary>
    public bool TryClearCollectorMission(CollectorMissionAssignment assignment, bool notify)
    {
        if (!IsCurrentCollectorAssignment(assignment))
            return false;

        snapshot.Collector = default;
        if (notify)
            Raise(MemoryChangeType.CollectorMissionCleared);
        return true;
    }

    private bool TryApplyCollectorCargoObservation(CollectorBodyObservation observation)
    {
        if (!HasValidCollectorClaim(observation.Assignment))
            return false;
        if (observation.RequiredPartCount < 0
            || observation.SecuredPartCount < 0
            || observation.SecuredPartCount > observation.RequiredPartCount
            || (observation.CargoSecure && observation.CargoLost)
            || (observation.CargoSecure
                && (observation.RequiredPartCount == 0
                    || observation.SecuredPartCount != observation.RequiredPartCount)))
        {
            return false;
        }

        bool cargoLost = observation.CargoSecure
            ? false
            : snapshot.Collector.CargoLost || observation.CargoLost;
        bool changed = snapshot.Collector.RequiredPartCount != observation.RequiredPartCount
            || snapshot.Collector.SecuredPartCount != observation.SecuredPartCount
            || snapshot.Collector.CargoSecure != observation.CargoSecure
            || snapshot.Collector.CargoLost != cargoLost;
        if (!changed)
            return false;

        snapshot.Collector.RequiredPartCount = observation.RequiredPartCount;
        snapshot.Collector.SecuredPartCount = observation.SecuredPartCount;
        snapshot.Collector.CargoSecure = observation.CargoSecure;
        snapshot.Collector.CargoLost = cargoLost;

        if (cargoLost)
        {
            snapshot.Collector.CargoSecure = false;
            snapshot.Collector.DockApproachReached = false;
            snapshot.Collector.DockAccessGranted = false;
        }

        Raise(MemoryChangeType.CollectorCargoChanged);
        return true;
    }

    private bool IsCurrentCollectorAssignment(CollectorMissionAssignment assignment)
    {
        return assignment != null && ReferenceEquals(snapshot.Collector.Assignment, assignment);
    }

    private static bool HasValidCollectorClaim(CollectorMissionAssignment assignment)
    {
        return assignment != null
            && assignment.Target != null
            && assignment.Claim.IsValid
            && assignment.Target.IsClaimValid(assignment.Claim);
    }

    private void Raise(MemoryChangeType type)
    {
        OnChanged?.Invoke(new MemoryChangeEvent
        {
            Type = type,
            Snapshot = snapshot // current full state
        });
    }

    public void ResetAll(bool notify = true)
    {
        snapshot = new RobotMemorySnapshotNew
        {
            LastKnownPlayerPosition = Vector3.zero,
            LastKnownPlayerTransform = null,
            LastKnownPlayerWaypoint = null,
            HasLastKnownPlayerPosition = false,
            PlayerInAttackZone = false,
            PlayerInDetectZone = false,
            WasRecentlyAttacked = false,
            LastAttackPosition = Vector3.zero,
            HasLastAttackPosition = false,
            IsConnectedToMachine = false,
            LastVisitedPoint = null,
            AllAvailableWaypoints = new Dictionary<RoomWaypoint, bool>(),
            DesiredMachineType = null,
            IsMachineTransitionInProgress = false,
            PendingReactivationMachine = null,
            Collector = default
        };
        if (notify)
            Raise(MemoryChangeType.Normal);
    }

}
