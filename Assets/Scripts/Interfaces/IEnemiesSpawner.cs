using UnityEngine;

public interface IEnemiesSpawner
{
    void Initialize(
        MapManager mapManager,
        IWaypointService waypointService,
        GameUIViewModel viewModel,
        IRobotRespawnService respawnService,
        MachineSecurityManager securityManager);
    void SetDropContainer(Transform container);
    void CreateWorkers(int workersToSpawn);
    void CreateEnemies(int enemiesToSpawn);
    void CreateBoss();
    GameObject CreateAngGetFollowerGuard();
    void CreateWorkersSpawner(int workersToSpawn);
    void SpreadEnemies();
    void SpawnEnemyAtRandom();
    void SpawnBossAtRandom();
}
