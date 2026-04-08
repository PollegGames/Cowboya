using System;
using System.Collections.Generic;
using UnityEngine;


// RobotMemoryStateNew.cs
public enum MemoryChangeType
{
    PlayerAttackZoneChanged,
    PlayerDetectZoneChanged,
    TookDamage,
    LastVisitedPointChanged,
    WaypointAvailabilityChanged,
    ConnectedToMachine,
    NotConnectedToMachine,
    DeadStateChanged,
    Normal
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
    public bool PlayerInAttackZone => snapshot.PlayerInAttackZone;
    public bool PlayerInDetectZone => snapshot.PlayerInDetectZone;
    public bool WasRecentlyAttacked => snapshot.WasRecentlyAttacked;
    public bool IsConnectedToMachine => snapshot.IsConnectedToMachine;
    public bool IsDead => snapshot.IsDead;
    public RoomWaypoint LastVisitedPoint => snapshot.LastVisitedPoint;

    // roomwaypoint and the bool is if the waypoint is no more accessible
    // for exemple if machine off, if a waypoint is in a blocked room, if already is already there, etc..
    public Dictionary<RoomWaypoint, bool> AllAvailableWaypoints => snapshot.AllAvailableWaypoints;
    public void RememberPlayerPosition(Vector3 position)
    {
        snapshot.LastKnownPlayerPosition = position;
        snapshot.HasLastKnownPlayerPosition = true;
        Raise(MemoryChangeType.Normal);
    }

    public void ClearPlayerPosition()
    {
        if (!snapshot.HasLastKnownPlayerPosition)
            return;

        snapshot.lastKnownPlayerPosition = Vector3.zero;
        snapshot.HasLastKnownPlayerPosition = false;
        Raise(MemoryChangeType.Normal);
    }

    public void ChangeConnectionToMachine(bool isConnected)
    {
        if (snapshot.IsConnectedToMachine == isConnected) return;
        snapshot.IsConnectedToMachine = isConnected;
        Raise(isConnected ? MemoryChangeType.ConnectedToMachine : MemoryChangeType.NotConnectedToMachine);
    }

    public void SetDead(bool isDead)
    {
        if (snapshot.IsDead == isDead) return;
        snapshot.IsDead = isDead;
        Raise(MemoryChangeType.DeadStateChanged);
    }

    public void RegisterAttack()
    {
        if (snapshot.WasRecentlyAttacked) return;
        snapshot.WasRecentlyAttacked = true;
        Raise(MemoryChangeType.TookDamage);
    }

    public void ResetAttackMemory()
    {
        if (!snapshot.WasRecentlyAttacked) return;
        snapshot.WasRecentlyAttacked = false;
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
        if (snapshot.AllAvailableWaypoints == null)
            snapshot.AllAvailableWaypoints = new Dictionary<RoomWaypoint, bool>();
        if (snapshot.AllAvailableWaypoints.TryGetValue(point, out bool current) && current == isAvailable) return;
        snapshot.AllAvailableWaypoints[point] = isAvailable;
        Raise(MemoryChangeType.WaypointAvailabilityChanged);
    }

    public void SetPlayerInDetectZone(bool inZone)
    {
        if (snapshot.PlayerInDetectZone == inZone) return;
        snapshot.PlayerInDetectZone = inZone;
        Raise(MemoryChangeType.PlayerDetectZoneChanged);
    }

    private void Raise(MemoryChangeType type)
    {
        OnChanged?.Invoke(new MemoryChangeEvent
        {
            Type = type,
            Snapshot = snapshot // current full state
        });
    }

    public void ResetAll()
    {
        snapshot = new RobotMemorySnapshotNew
        {
            LastKnownPlayerPosition = Vector3.zero,
            HasLastKnownPlayerPosition = false,
            PlayerInAttackZone = false,
            PlayerInDetectZone = false,
            WasRecentlyAttacked = false,
            IsConnectedToMachine = false,
            LastVisitedPoint = null,
            AllAvailableWaypoints = new Dictionary<RoomWaypoint, bool>()
        };
        Raise(MemoryChangeType.Normal);
    }

}
