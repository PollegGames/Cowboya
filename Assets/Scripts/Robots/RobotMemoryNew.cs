using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores information remembered by the enemy such as the last known player position and received attacks.
/// </summary>
public class RobotMemoryNew : MonoBehaviour, IRobotMemoryNew
{
    [Header("Player Memory")]

    private readonly RobotMemoryStateNew memoryState = new RobotMemoryStateNew();
    public Vector3 LastKnownPlayerPosition => memoryState.LastKnownPlayerPosition;
    public bool PlayerInAttackZone => memoryState.PlayerInAttackZone;
    public bool PlayerInDetectZone => memoryState.PlayerInDetectZone;

    [Header("Aggression Memory")]
    public bool WasRecentlyAttacked => memoryState.WasRecentlyAttacked;
    public bool IsConnectedToMachine => memoryState.IsConnectedToMachine;
    public bool IsDead => memoryState.IsDead;
    public RoomWaypoint LastVisitedPoint => memoryState.LastVisitedPoint;
    public Dictionary<RoomWaypoint, bool> AllAvailableWaypoints => memoryState.AllAvailableWaypoints;
    public event Action<MemoryChangeEvent> OnMemoryChanged;
    public RobotMemorySnapshotNew Snapshot => memoryState.Snapshot;

    private void Awake()
    {
        memoryState.OnChanged += HandleStateChanged;
    }
    private void OnDestroy()
    {
        memoryState.OnChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(MemoryChangeEvent e)
    {
        OnMemoryChanged?.Invoke(e); // Brain listens here
    }
    
    public void SetLastVisitedPoint(RoomWaypoint point) => memoryState.SetLastVisitedPoint(point);

    /// <summary>
    /// Updates the last known player position.
    /// </summary>
    /// <param name="playerPosition">Detected player position.</param>
    public void RememberPlayerPosition(Vector3 playerPosition) => memoryState.RememberPlayerPosition(playerPosition);

    /// <summary>
    /// Clears the memory of the player's position.
    /// </summary>
    public void ClearPlayerPosition() => memoryState.ClearPlayerPosition();

    /// <summary>
    /// Records whether the player is currently within the attack zone.
    /// </summary>
    /// <param name="inZone">True if the player is in range.</param>
    public void SetPlayerInAttackZone(bool inZone) => memoryState.SetPlayerInAttackZone(inZone);


    /// <summary>
    /// Records whether the player is currently within the detection zone.
    /// </summary>
    /// <param name="inZone">True if the player is in range.</param>
    public void SetPlayerInDetectZone(bool inZone) => memoryState.SetPlayerInDetectZone(inZone);


    /// <summary>
    /// Records that the enemy has just been attacked.
    /// </summary>
    public void RegisterAttack() => memoryState.RegisterAttack();

    /// <summary>
    /// Resets the aggression state after a certain period.
    /// </summary>
    public void ResetAttackMemory() => memoryState.ResetAttackMemory();

    public void SetRoomWaypointAvailability(RoomWaypoint point, bool isAvailable) => memoryState.SetRoomWaypointAvailability(point, isAvailable);

    public void ChangeConnectionToMachine(bool isConnected) => memoryState.ChangeConnectionToMachine(isConnected);

    public void SetDead(bool isDead) => memoryState.SetDead(isDead);


}
