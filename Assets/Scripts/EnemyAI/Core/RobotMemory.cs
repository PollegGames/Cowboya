using UnityEngine;

/// <summary>
/// Stores information remembered by the enemy such as the last known player position and received attacks.
/// </summary>
public class RobotMemory : MonoBehaviour, IRobotMemory, IPooledObject
{
    [Header("Player Memory")]
    public Vector3 LastKnownPlayerPosition { get; private set; }
    public float TimeSincePlayerLastSeen { get; private set; }

    [Header("Aggression Memory")]
    public bool WasRecentlyAttacked { get; private set; }
    public float TimeSinceLastAttack { get; private set; }

    private IRobotRespawnService respawnService;
    public RoomWaypoint LastVisitedPoint { get; private set; }

    private void Update()
    {
        // Automatically update timers
        if (LastKnownPlayerPosition != Vector3.zero)
            TimeSincePlayerLastSeen += Time.deltaTime;

        if (WasRecentlyAttacked)
            TimeSinceLastAttack += Time.deltaTime;
    }
    public void SetRespawnService(IRobotRespawnService service)
    {
        respawnService = service;
    }


    public void SetLastVisitedPoint(RoomWaypoint point)
    {
        LastVisitedPoint = point;
    }

    /// <summary>
    /// Called when EnemyController detects it’s “stuck.” 
    /// Memory will now tell the spawner to create a brand-new enemy, then destroy this one.
    /// </summary>
    public void OnStuck(EnemyWorkerController controller)
    {
        Debug.Log($"[EnemyMemory] Enemy stuck at {controller.transform.position}. Requesting respawn.");

        // Return the stuck enemy to the pool before requesting a respawn.
        ObjectPool.Instance.Release(controller.gameObject);

        if (respawnService == null)
        {
            Debug.LogError("[EnemyMemory] Cannot respawn: service is null!");
            return;
        }

        respawnService.RespawnWorker();
    }

    /// <summary>
    /// Called when EnemyController detects it’s “stuck.” 
    /// Memory will now tell the spawner to create a brand-new enemy, then destroy this one.
    /// </summary>
    public void OnBossStuck(EnemyController controller)
    {
        Debug.Log($"[EnemyMemory] Boss stuck at {controller.transform.position}. Requesting respawn.");

        // Return the stuck boss to the pool before requesting a respawn.
        ObjectPool.Instance.Release(controller.gameObject);

        if (respawnService == null)
        {
            Debug.LogError("[EnemyMemory] Cannot respawn: service is null!");
            return;
        }

        respawnService.RespawnBoss();
    }

    /// <summary>
    /// Updates the last known player position.
    /// </summary>
    /// <param name="playerPosition">Detected player position.</param>
    public void RememberPlayerPosition(Vector3 playerPosition)
    {
        LastKnownPlayerPosition = playerPosition;
        TimeSincePlayerLastSeen = 0f;
    }

    /// <summary>
    /// Clears the memory of the player's position.
    /// </summary>
    public void ClearPlayerPosition()
    {
        if (WasRecentlyAttacked)
            return;
        LastKnownPlayerPosition = Vector3.zero;
        TimeSincePlayerLastSeen = 0f;
    }

    /// <summary>
    /// Records that the enemy has just been attacked.
    /// </summary>
    public void RegisterAttack()
    {
        WasRecentlyAttacked = true;
        TimeSinceLastAttack = 0f;
    }

    /// <summary>
    /// Resets the aggression state after a certain period.
    /// </summary>
    public void ResetAttackMemory()
    {
        WasRecentlyAttacked = false;
        TimeSinceLastAttack = 0f;
    }

    /// <summary>
    /// Called when the object is released back to the pool.
    /// </summary>
    public void OnReleaseToPool()
    {
        LastKnownPlayerPosition = Vector3.zero;
        TimeSincePlayerLastSeen = 0f;
        WasRecentlyAttacked = false;
        TimeSinceLastAttack = 0f;
        LastVisitedPoint = null;
        respawnService = null;
    }

    /// <summary>
    /// Called when the object is taken from the pool.
    /// </summary>
    public void OnAcquireFromPool()
    {
        TimeSincePlayerLastSeen = 0f;
        TimeSinceLastAttack = 0f;
        if (respawnService == null)
        {
            respawnService = GetComponent<IRobotRespawnService>();
        }
    }
}
