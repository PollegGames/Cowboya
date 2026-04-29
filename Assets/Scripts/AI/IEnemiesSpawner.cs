using UnityEngine;

public interface IEnemiesSpawner
{
    void Initialize(
        MapManager mapManager,
        IWaypointService waypointService,
        GameUIViewModel viewModel,
        IRobotRespawnService respawnService,
        IFactoryManager factoryManager,
        MachineSecurityManager securityManager,
        SecurityBadgeSpawner securityBadgeSpawner,
        BatterySpawner batterySpawner);
    void SetDropContainer(Transform container);
    void CreateWorkers(int workersToSpawn);
    void CreateSecurityGuards(int enemiesToSpawn);
    void CreateBoss();
    void CreateAndSpawnFollowerGuard(RoomWaypoint spawnPos, FactoryAlarmStatus factoryAlarmStatus);
    void CreateAndSpawnSecurityGuard(RoomWaypoint spawnPos, SecurityMachine machine);
    void CreateWorkerSpawners(int workersToSpawn);
    void SpreadEnemies();
    void SpawnEnemyAtRandom();
    void SpawnBossAtRandom();
}
