using UnityEngine;

public interface IEnemiesSpawner
{
    void Initialize(
        MapManager mapManager,
        IWaypointService waypointService,
        GameUIViewModel viewModel,
        IRobotRespawnService respawnService,
        MachineSecurityManager securityManager,
        SecurityBadgeSpawner securityBadgeSpawner);
    void SetDropContainer(Transform container);
    void CreateWorkers(int workersToSpawn);
    void CreateEnemies(int enemiesToSpawn);
    void CreateBoss();
    void CreateAndSpawnFollowerGuard(RoomWaypoint spawnPos, FactoryAlarmStatus factoryAlarmStatus);
    void CreateAndSpawnSecurityGuard(RoomWaypoint spawnPos, SecurityMachine machine);
    void CreateWorkersSpawner(int workersToSpawn);
    void SpreadEnemies();
    void SpawnEnemyAtRandom();
    void SpawnBossAtRandom();
}
