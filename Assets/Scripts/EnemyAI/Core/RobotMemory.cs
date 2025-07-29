using UnityEngine;

/// <summary>
/// Stores information remembered by the enemy such as the last known player position and received attacks.
/// </summary>
public class RobotMemory : MonoBehaviour, IRobotMemory
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

        if (respawnService != null)
        {
            respawnService.RespawnWorker();
        }
        else
        {
            Debug.LogError("[EnemyMemory] Cannot respawn: service is null!");
        }

        // Finally, destroy the stuck enemy’s GameObject:
        Destroy(controller.gameObject);
    }

    /// <summary>
    /// Called when EnemyController detects it’s “stuck.” 
    /// Memory will now tell the spawner to create a brand-new enemy, then destroy this one.
    /// </summary>
    public void OnBossStuck(EnemyController controller)
    {
        Debug.Log($"[EnemyMemory] Boss stuck at {controller.transform.position}. Requesting respawn.");

        if (respawnService != null)
        {
            respawnService.RespawnBoss();
        }
        else
        {
            Debug.LogError("[EnemyMemory] Cannot respawn: service is null!");
        }

        // Finally, destroy the stuck enemy’s GameObject:
        Destroy(controller.gameObject);
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
}
