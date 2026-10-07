using System;
using UnityEngine;

/// <summary>
/// Immutable service route shared by Worker Collector tasks, with optional per-delivery cargo ownership.
/// </summary>
public sealed class WorkerCollectorMissionAssignment
{
    /// <summary>
    /// Remembers the collection route before the worker observes a nearby cube.
    /// </summary>
    public WorkerCollectorMissionAssignment(
        int missionId,
        WorkerCollectorWhiteCubeSourceProvider source,
        WorkerCollectorDropOffProvider dropOff,
        WorkerCollectorSpawnRestProvider rest)
        : this(missionId, source, dropOff, rest, null, default, default) {
    }

    public WorkerCollectorMissionAssignment(
        int missionId,
        WorkerCollectorWhiteCubeSourceProvider source,
        WorkerCollectorDropOffProvider dropOff,
        WhiteCubeCargo target,
        WhiteCubeClaim claim)
        : this(missionId, source, dropOff, null, target, claim, default)
    {
    }

    public WorkerCollectorMissionAssignment(
        int missionId,
        WorkerCollectorWhiteCubeSourceProvider source,
        WorkerCollectorDropOffProvider dropOff,
        WorkerCollectorSpawnRestProvider rest,
        WhiteCubeCargo target,
        WhiteCubeClaim claim,
        GarageSlotReservation reservation)
    {
        MissionId = missionId;
        Source = source;
        DropOff = dropOff;
        Rest = rest;
        Target = target;
        Claim = claim;
        Reservation = reservation;
    }

    public int MissionId { get; }
    public WorkerCollectorWhiteCubeSourceProvider Source { get; }
    public WorkerCollectorDropOffProvider DropOff { get; }
    public WorkerCollectorSpawnRestProvider Rest { get; }
    public WhiteCubeCargo Target { get; }
    public WhiteCubeClaim Claim { get; }
    public GarageSlotReservation Reservation { get; }
    public bool HasRequiredReferences => MissionId > 0 && Source != null && DropOff != null;
    public bool HasClaimedTarget => Target != null && Claim.IsValid;
}

[Serializable]
public struct WorkerCollectorMissionFacts
{
    public WorkerCollectorMissionAssignment Assignment;
    public bool SourceApproachReached;
    public bool TargetApproachReached;
    public bool CargoSecured;
    public bool CargoLost;
    public bool GarageAvailable;
    public bool TargetUnavailable;
    public bool DropOffApproachReached;
    public bool DeliveryAccepted;
    public bool WaitingForBatch;
    public bool BatchCompleted;
    public bool RestApproachReached;
    public float RestUntil;
    public bool RestCompleted;
}

public enum WorkerCollectorBodyObservationType
{
    TargetApproachChanged = 0,
    CargoChanged = 1,
    TargetUnavailable = 2,
    DropOffApproachChanged = 3,
    DeliveryAccepted = 4,
    BatchCompleted = 5,
    RestApproachChanged = 6,
    RestCompleted = 7,
    BatchInterrupted = 8,
    SourceApproachChanged = 9,
    GarageAvailabilityChanged = 10
}

public readonly struct WorkerCollectorBodyObservation
{
    private WorkerCollectorBodyObservation(
        WorkerCollectorBodyObservationType type,
        WorkerCollectorMissionAssignment assignment,
        int commandToken,
        bool value)
    {
        Type = type;
        Assignment = assignment;
        CommandToken = commandToken;
        Value = value;
    }

    public WorkerCollectorBodyObservationType Type { get; }
    public WorkerCollectorMissionAssignment Assignment { get; }
    public int CommandToken { get; }
    public bool Value { get; }

    public static WorkerCollectorBodyObservation SourceApproach(
        WorkerCollectorMissionAssignment assignment, int token, bool reached = true) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.SourceApproachChanged, assignment, token, reached);

    public static WorkerCollectorBodyObservation GarageAvailability(
        WorkerCollectorMissionAssignment assignment, int token, bool available) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.GarageAvailabilityChanged, assignment, token, available);

    public static WorkerCollectorBodyObservation TargetApproach(
        WorkerCollectorMissionAssignment assignment, int token, bool reached = true) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.TargetApproachChanged, assignment, token, reached);

    public static WorkerCollectorBodyObservation Cargo(
        WorkerCollectorMissionAssignment assignment, int token, bool secured) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.CargoChanged, assignment, token, secured);

    public static WorkerCollectorBodyObservation TargetLost(
        WorkerCollectorMissionAssignment assignment, int token) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.TargetUnavailable, assignment, token, true);

    public static WorkerCollectorBodyObservation DropOffApproach(
        WorkerCollectorMissionAssignment assignment, int token, bool reached = true) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.DropOffApproachChanged, assignment, token, reached);

    public static WorkerCollectorBodyObservation Delivery(
        WorkerCollectorMissionAssignment assignment, int token, bool waitingForBatch) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.DeliveryAccepted, assignment, token, waitingForBatch);

    public static WorkerCollectorBodyObservation BatchProcessed(
        WorkerCollectorMissionAssignment assignment, int token) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.BatchCompleted, assignment, token, true);

    public static WorkerCollectorBodyObservation BatchInterrupted(
        WorkerCollectorMissionAssignment assignment, int token) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.BatchInterrupted, assignment, token, false);

    public static WorkerCollectorBodyObservation RestApproach(
        WorkerCollectorMissionAssignment assignment, int token) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.RestApproachChanged, assignment, token, true);

    public static WorkerCollectorBodyObservation RestFinished(
        WorkerCollectorMissionAssignment assignment, int token) =>
        new WorkerCollectorBodyObservation(
            WorkerCollectorBodyObservationType.RestCompleted, assignment, token, true);
}

public interface IWorkerCollectorTaskBody
{
    void FindCube();
    void BeginMoveToSource(WorkerCollectorMissionAssignment assignment);
    void AcquireCube(WorkerCollectorMissionAssignment assignment);
    void BeginMoveToCube(WorkerCollectorMissionAssignment assignment);
    void GrabCube(WorkerCollectorMissionAssignment assignment);
    void BeginMoveToDropOff(WorkerCollectorMissionAssignment assignment);
    void DepositCube(WorkerCollectorMissionAssignment assignment);
    void WaitForGarage(WorkerCollectorMissionAssignment assignment);
    void WaitAtDropOff(WorkerCollectorMissionAssignment assignment);
    void BeginMoveToRest(WorkerCollectorMissionAssignment assignment);
    void Rest(WorkerCollectorMissionAssignment assignment);
    void CancelCurrentCommand(WorkerCollectorMissionAssignment assignment);
    void StopAllActuators();
}
