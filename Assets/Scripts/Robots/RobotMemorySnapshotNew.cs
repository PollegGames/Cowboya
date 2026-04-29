using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Snapshot of memory facts consumed by RobotBrainNew.
/// </summary>
public struct RobotMemorySnapshotNew
{
    public Vector3 LastKnownPlayerPosition;
    public bool HasLastKnownPlayerPosition;
    public bool PlayerInAttackZone;
    public bool PlayerInDetectZone;
    public bool WasRecentlyAttacked;
    public bool IsConnectedToMachine;
    public bool IsDead;
    public RoomWaypoint LastVisitedPoint;
    public Dictionary<RoomWaypoint, bool> AllAvailableWaypoints;
    public MachineType? DesiredMachineType;
    public bool IsMachineTransitionInProgress;
    // Backward-compatible alias for older code that uses lower camel case.
    public Vector3 lastKnownPlayerPosition
    {
        get => LastKnownPlayerPosition;
        set => LastKnownPlayerPosition = value;
    }
}
