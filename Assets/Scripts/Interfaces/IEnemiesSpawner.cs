using UnityEngine;

public interface IEnemiesSpawner
{
    void Initialize(
        MapManager mapManager,
        IWaypointService waypointService,
        GameUIViewModel viewModel,
        IRobotRespawnService respawnService,
        MachineSecurityManager securityManager);
    void CreateWorkers(int workersToSpawn);
    void CreateEnemy(int enemiesToSpawn);
    void SpreadEnemies();
    void SpawnEnemyAtRandom();
    void SpawnBossAtRandom();
}
