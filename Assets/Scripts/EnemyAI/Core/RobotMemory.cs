using UnityEngine;

/// <summary>
/// Stores information remembered by the enemy such as the last known player position and received attacks.
/// </summary>
public class RobotMemory : MonoBehaviour, IRobotMemory, IPooledObject
{
    [Header("Player Memory")]
    public Vector3 LastKnownPlayerPosition => memoryState.LastKnownPlayerPosition;
    public float TimeSincePlayerLastSeen => memoryState.TimeSincePlayerLastSeen;

    [Header("Aggression Memory")]
    public bool WasRecentlyAttacked => memoryState.WasRecentlyAttacked;
    public float TimeSinceLastAttack => memoryState.TimeSinceLastAttack;

    public RoomWaypoint LastVisitedPoint => memoryState.LastVisitedPoint;
    public RobotMemoryState Snapshot => memoryState;

    private readonly RobotMemoryState memoryState = new RobotMemoryState();

    private void Update()
    {
        memoryState.Tick(Time.deltaTime);
    }

    public void SetRespawnService(IRobotRespawnService service)
    {
        memoryState.SetRespawnService(service);
    }

    public void SetLastVisitedPoint(RoomWaypoint point)
    {
        memoryState.SetLastVisitedPoint(point);
    }

    /// <summary>
    /// Updates the last known player position.
    /// </summary>
    /// <param name="playerPosition">Detected player position.</param>
    public void RememberPlayerPosition(Vector3 playerPosition)
    {
        memoryState.RememberPlayerPosition(playerPosition);
    }

    /// <summary>
    /// Clears the memory of the player's position.
    /// </summary>
    public void ClearPlayerPosition()
    {
        memoryState.ClearPlayerPosition();
    }

    /// <summary>
    /// Records that the enemy has just been attacked.
    /// </summary>
    public void RegisterAttack()
    {
        memoryState.RegisterAttack();
    }

    /// <summary>
    /// Resets the aggression state after a certain period.
    /// </summary>
    public void ResetAttackMemory()
    {
        memoryState.ResetAttackMemory();
    }

    /// <summary>
    /// Called when the object is released back to the pool.
    /// </summary>
    public void OnReleaseToPool()
    {
        memoryState.ResetAll();
    }

    /// <summary>
    /// Called when the object is taken from the pool.
    /// </summary>
    public void OnAcquireFromPool()
    {
        memoryState.ResetAll();
        if (memoryState.RespawnService == null)
        {
            var respawn = GetComponent<IRobotRespawnService>();
            if (respawn != null)
                memoryState.SetRespawnService(respawn);
        }
    }
}
