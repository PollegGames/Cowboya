using UnityEngine;

/// <summary>
/// Stocke les informations mémorisées par l'ennemi, comme la dernière position connue du joueur, les agressions subies, etc.
/// </summary>
public class RobotMemory : MonoBehaviour
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
        // Mise à jour automatique des timers
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
    /// Met à jour la dernière position connue du joueur.
    /// </summary>
    /// <param name="playerPosition">Position du joueur détecté.</param>
    public void RememberPlayerPosition(Vector3 playerPosition)
    {
        LastKnownPlayerPosition = playerPosition;
        TimeSincePlayerLastSeen = 0f;
    }

    /// <summary>
    /// Efface la mémoire de la position du joueur.
    /// </summary>
    public void ClearPlayerPosition()
    {
        LastKnownPlayerPosition = Vector3.zero;
        TimeSincePlayerLastSeen = 0f;
    }

    /// <summary>
    /// Enregistre que l'ennemi vient de subir une agression.
    /// </summary>
    public void RegisterAttack()
    {
        WasRecentlyAttacked = true;
        TimeSinceLastAttack = 0f;
    }

    /// <summary>
    /// Réinitialise l'état d'agression après une certaine période.
    /// </summary>
    public void ResetAttackMemory()
    {
        WasRecentlyAttacked = false;
        TimeSinceLastAttack = 0f;
    }
}
