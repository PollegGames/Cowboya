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
    public Transform LastKnownPlayerTransform => memoryState.LastKnownPlayerTransform;
    public RoomWaypoint LastKnownPlayerWaypoint => memoryState.LastKnownPlayerWaypoint;
    public bool PlayerInAttackZone => memoryState.PlayerInAttackZone;
    public bool PlayerInDetectZone => memoryState.PlayerInDetectZone;

    [Header("Aggression Memory")]
    public bool WasRecentlyAttacked => memoryState.WasRecentlyAttacked;
    public bool IsConnectedToMachine => memoryState.IsConnectedToMachine;
    public bool IsDead => memoryState.IsDead;
    public RoomWaypoint LastVisitedPoint => memoryState.LastVisitedPoint;
    public MachineType? DesiredMachineType => memoryState.DesiredMachineType;
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
        RobotNewTrace.Log(
            this,
            eventSource: "MemoryNew.OnChanged",
            memoryDelta: e.Type.ToString(),
            brainOptions: BrainOption.None,
            plannedTask: null,
            heartCurrentTask: null,
            taskSignal: "none");
    }

    public void InitializeWaypointAvailability(IEnumerable<RoomWaypoint> waypoints)
    {
        memoryState.ReplaceWaypointAvailability(waypoints);
    }

    public void SetMachineWaypointAvailability(BaseMachine machine, bool isAvailable)
    {
        if (machine == null)
            return;

        var waypoint = MachineWaypointResolver.Resolve(machine);
        if (waypoint == null)
            return;

        memoryState.SetRoomWaypointAvailability(waypoint, isAvailable);
    }

    public void NotifyReactivationCompleted(BaseMachine machine, MachineType? nextDesiredMachineType)
    {
        if (machine == null)
            return;

        var waypoint = MachineWaypointResolver.Resolve(machine);
        bool connectedToReactivatedMachine = machine.IsOn && !nextDesiredMachineType.HasValue;
        Debug.Log(
            $"[RobotMemoryNew] Reactivation completed machine={machine.name} nextDesired={(nextDesiredMachineType.HasValue ? nextDesiredMachineType.Value.ToString() : "none")} connectedToReactivatedMachine={connectedToReactivatedMachine}",
            this);
        memoryState.NotifyReactivationCompleted(waypoint, nextDesiredMachineType, connectedToReactivatedMachine);
    }

    public void SetLastVisitedPoint(RoomWaypoint point) => memoryState.SetLastVisitedPoint(point);

    /// <summary>
    /// Updates the last known player position.
    /// </summary>
    /// <param name="playerPosition">Detected player position.</param>
    public void RememberPlayerPosition(Vector3 playerPosition) => memoryState.RememberPlayerPosition(playerPosition);

    /// <summary>
    /// Updates the currently detected player target reference and position.
    /// </summary>
    /// <param name="playerTransform">Detected player transform.</param>
    public void RememberPlayerTransform(Transform playerTransform) => memoryState.RememberPlayerTransform(playerTransform);

    /// <summary>
    /// Updates the waypoint currently nearest to the player for navigation-based chasing.
    /// </summary>
    /// <param name="playerWaypoint">Nearest waypoint to the player.</param>
    /// <param name="playerPosition">Current player position.</param>
    public void RememberPlayerWaypoint(RoomWaypoint playerWaypoint, Vector3 playerPosition) =>
        memoryState.RememberPlayerWaypoint(playerWaypoint, playerPosition);

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
    /// Backward-compatible alias for older code paths.
    /// </summary>
    public void SetCanSeePlayer(bool canSeePlayer)
    {
        SetPlayerInDetectZone(canSeePlayer);
        if (!canSeePlayer && !PlayerInAttackZone)
            ClearPlayerPosition();
    }


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

    public void NotifyMachineSlotAttached(RoomWaypoint point) => memoryState.NotifyMachineSlotAttached(point);

    public void NotifyMachineSlotReleased() => memoryState.NotifyMachineSlotReleased();

    public void NotifyMachineSlotReleasedTransient() => memoryState.NotifyMachineSlotReleasedTransient();

    public void NotifyMachineSlotReleasedFinal() => memoryState.NotifyMachineSlotReleasedFinal();

    public void SetDesiredMachineType(MachineType? machineType) => memoryState.SetDesiredMachineType(machineType);

}
