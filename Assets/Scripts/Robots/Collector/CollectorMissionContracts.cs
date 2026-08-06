using System;

/// <summary>
/// Opaque ownership token for one collectible lifecycle of one dead robot.
/// </summary>
public readonly struct CollectorTargetClaim : IEquatable<CollectorTargetClaim>
{
    public CollectorTargetClaim(int targetInstanceId, int targetGeneration, int claimVersion)
    {
        TargetInstanceId = targetInstanceId;
        TargetGeneration = targetGeneration;
        ClaimVersion = claimVersion;
    }

    public int TargetInstanceId { get; }
    public int TargetGeneration { get; }
    public int ClaimVersion { get; }
    public bool IsValid => TargetInstanceId != 0 && TargetGeneration > 0 && ClaimVersion > 0;

    public bool Equals(CollectorTargetClaim other)
    {
        return TargetInstanceId == other.TargetInstanceId
            && TargetGeneration == other.TargetGeneration
            && ClaimVersion == other.ClaimVersion;
    }

    public override bool Equals(object obj)
    {
        return obj is CollectorTargetClaim other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hashCode = TargetInstanceId;
            hashCode = (hashCode * 397) ^ TargetGeneration;
            hashCode = (hashCode * 397) ^ ClaimVersion;
            return hashCode;
        }
    }

    public static bool operator ==(CollectorTargetClaim left, CollectorTargetClaim right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(CollectorTargetClaim left, CollectorTargetClaim right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Stable reference payload shared by every task belonging to one Collector mission.
/// </summary>
public sealed class CollectorMissionAssignment
{
    public CollectorMissionAssignment(
        int missionId,
        SpawnRobotCollectorController home,
        DeadRobotCollectable target,
        CollectorTargetClaim claim)
    {
        MissionId = missionId;
        Home = home;
        Target = target;
        Claim = claim;
    }

    public int MissionId { get; }
    public SpawnRobotCollectorController Home { get; }
    public DeadRobotCollectable Target { get; }
    public CollectorTargetClaim Claim { get; }

    /// <summary>
    /// Returns whether the assignment contains the stable references required to start a mission.
    /// Claim ownership is validated separately by the target.
    /// </summary>
    public bool HasRequiredReferences => MissionId > 0 && Home != null && Target != null && Claim.IsValid;
}

/// <summary>
/// Factual Collector mission data stored in robot Memory.
/// </summary>
[Serializable]
public struct CollectorMissionFacts
{
    public CollectorMissionAssignment Assignment;
    public bool LaunchExitReached;
    public bool TargetApproachReached;
    public int RequiredPartCount;
    public int SecuredPartCount;
    public bool CargoSecure;
    public bool CargoLost;
    public bool TargetUnavailable;
    public bool MissionCancelled;
    public bool DockApproachReached;
    public bool DockAccessGranted;
    public bool IntakeConfirmed;
    public bool FlightFault;
}

/// <summary>
/// Identifies a discrete physical observation produced by the Collector body.
/// </summary>
public enum CollectorBodyObservationType
{
    LaunchExitChanged = 0,
    TargetApproachChanged = 1,
    CargoChanged = 2,
    DockApproachChanged = 3,
    FlightFaultChanged = 4
}

/// <summary>
/// Immutable observation sent from the physical Collector body toward Brain and Memory.
/// </summary>
public readonly struct CollectorBodyObservation
{
    private CollectorBodyObservation(
        CollectorBodyObservationType type,
        CollectorMissionAssignment assignment,
        int commandToken,
        bool value,
        int requiredPartCount,
        int securedPartCount,
        bool cargoSecure,
        bool cargoLost)
    {
        Type = type;
        Assignment = assignment;
        CommandToken = commandToken;
        Value = value;
        RequiredPartCount = requiredPartCount;
        SecuredPartCount = securedPartCount;
        CargoSecure = cargoSecure;
        CargoLost = cargoLost;
    }

    public CollectorBodyObservationType Type { get; }
    public CollectorMissionAssignment Assignment { get; }
    public CollectorTargetClaim Claim => Assignment != null ? Assignment.Claim : default;
    public int CommandToken { get; }
    public bool Value { get; }
    public int RequiredPartCount { get; }
    public int SecuredPartCount { get; }
    public bool CargoSecure { get; }
    public bool CargoLost { get; }

    public static CollectorBodyObservation LaunchExit(
        CollectorMissionAssignment assignment,
        int commandToken,
        bool reached = true)
    {
        return CreateBoolean(
            CollectorBodyObservationType.LaunchExitChanged,
            assignment,
            commandToken,
            reached);
    }

    public static CollectorBodyObservation TargetApproach(
        CollectorMissionAssignment assignment,
        int commandToken,
        bool reached = true)
    {
        return CreateBoolean(
            CollectorBodyObservationType.TargetApproachChanged,
            assignment,
            commandToken,
            reached);
    }

    public static CollectorBodyObservation Cargo(
        CollectorMissionAssignment assignment,
        int commandToken,
        int requiredPartCount,
        int securedPartCount,
        bool cargoSecure,
        bool cargoLost)
    {
        return new CollectorBodyObservation(
            CollectorBodyObservationType.CargoChanged,
            assignment,
            commandToken,
            value: false,
            requiredPartCount,
            securedPartCount,
            cargoSecure,
            cargoLost);
    }

    public static CollectorBodyObservation DockApproach(
        CollectorMissionAssignment assignment,
        int commandToken,
        bool reached = true)
    {
        return CreateBoolean(
            CollectorBodyObservationType.DockApproachChanged,
            assignment,
            commandToken,
            reached);
    }

    public static CollectorBodyObservation FlightFault(
        CollectorMissionAssignment assignment,
        int commandToken,
        bool faulted = true)
    {
        return CreateBoolean(
            CollectorBodyObservationType.FlightFaultChanged,
            assignment,
            commandToken,
            faulted);
    }

    private static CollectorBodyObservation CreateBoolean(
        CollectorBodyObservationType type,
        CollectorMissionAssignment assignment,
        int commandToken,
        bool value)
    {
        return new CollectorBodyObservation(
            type,
            assignment,
            commandToken,
            value,
            requiredPartCount: 0,
            securedPartCount: 0,
            cargoSecure: false,
            cargoLost: false);
    }
}

/// <summary>
/// Narrow physical execution seam used by Collector tasks.
/// </summary>
public interface ICollectorTaskBody
{
    event Action<CollectorBodyObservation> OnObservation;

    void BeginLaunch(CollectorMissionAssignment assignment);
    void BeginOutbound(CollectorMissionAssignment assignment);
    void BeginGathering(CollectorMissionAssignment assignment);
    void BeginReturn(CollectorMissionAssignment assignment);
    void BeginAbortReturn(CollectorMissionAssignment assignment);
    void BeginDocking(CollectorMissionAssignment assignment);
    void CancelCurrentCommand(CollectorMissionAssignment assignment);
    void StopAllActuators();
    void ResetPhysicalState();
}
