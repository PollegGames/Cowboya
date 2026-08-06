using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snapshot of memory facts consumed by RobotBrainNew.
/// </summary>
public struct RobotMemorySnapshotNew
{
    public Vector3 LastKnownPlayerPosition;
    public Transform LastKnownPlayerTransform;
    public RoomWaypoint LastKnownPlayerWaypoint;
    public bool HasLastKnownPlayerPosition;
    public bool PlayerInAttackZone;
    public bool PlayerInDetectZone;
    public bool WasRecentlyAttacked;
    public Vector3 LastAttackPosition;
    public bool HasLastAttackPosition;
    public bool IsConnectedToMachine;
    public bool IsDead;
    public RoomWaypoint LastVisitedPoint;
    public Dictionary<RoomWaypoint, bool> AllAvailableWaypoints;
    public MachineType? DesiredMachineType;
    public bool IsMachineTransitionInProgress;
    public BaseMachine PendingReactivationMachine;
    public CollectorMissionFacts Collector;

    // Backward-compatible alias for older code that uses lower camel case.
    public Vector3 lastKnownPlayerPosition
    {
        get => LastKnownPlayerPosition;
        set => LastKnownPlayerPosition = value;
    }
}
