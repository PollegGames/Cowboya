using UnityEngine;

public class RobotRespawnService : MonoBehaviour, IRobotRespawnService
{
    private EnemiesSpawner spawner;

    public void Initialize(EnemiesSpawner spawner)
    {
        this.spawner = spawner;
    }

    public void RespawnWorker()
    {
        if (spawner != null)
            spawner.SpawnEnemyAtRandom();
        else
            Debug.LogError("[RobotRespawnService] Spawner reference is null.");
    }

    public void RespawnBoss()
    {
        if (spawner != null)
            spawner.SpawnBossAtRandom();
        else
            Debug.LogError("[RobotRespawnService] Spawner reference is null.");
    }
}
