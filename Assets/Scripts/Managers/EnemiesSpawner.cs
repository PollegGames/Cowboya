using UnityEngine;
using System.Collections.Generic;
using System;

public class EnemiesSpawner : MonoBehaviour, IEnemiesSpawner, IDropHost
{
    [SerializeField] private GameObject workerPrefab;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform enemiesParent;
    private Transform dropContainer;
    // Expose enemies count via public property
    private MapManager mapManager;
    private IWaypointService waypointService;
    private IRobotRespawnService respawnService;
    private MachineSecurityManager securityManager;
    private List<GameObject> spawnedWorkers = new List<GameObject>();
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private List<GameObject> spawnedWorkerSpawners = new List<GameObject>();
    private GameObject boosInstance;
    private GameUIViewModel gameUIViewModel;

    public Transform DropContainer => dropContainer;
    private SecurityBadgeSpawner securityBadgeSpawner;

    public void SetDropContainer(Transform container)
    {
        dropContainer = container;
    }

    public void Initialize(
        MapManager mapManager,
        IWaypointService waypointService,
        GameUIViewModel viewModel,
        IRobotRespawnService respawnService,
        MachineSecurityManager securityManager,
        SecurityBadgeSpawner securityBadgeSpawner)
    {
        this.waypointService = waypointService;
        this.mapManager = mapManager;
        this.gameUIViewModel = viewModel;
        this.respawnService = respawnService;
        this.securityManager = securityManager;
        this.securityBadgeSpawner = securityBadgeSpawner;

        if (respawnService is RobotRespawnService service)
            service.Initialize(this);

        Debug.Log("EnemiesSpawner: services initialized.");
    }

    // Create enemies and store them, but don't position them yet
    public void CreateWorkers(int workersToSpawn)
    {
        WorkerRobotFactory workerRobotFactory = new WorkerRobotFactory();
        spawnedWorkers.Clear();
        for (int i = 0; i < workersToSpawn; i++)
        {
            var worker = ObjectPool.Instance.Get(workerPrefab, enemiesParent);
            var robotState = worker.GetComponent<RobotStateController>();
            robotState.Stats = workerRobotFactory.CreateRobot();
            robotState.Stats.RobotName = $"Worker {i + 1}";
            worker.SetActive(false);
            spawnedWorkers.Add(worker);
        }
        Debug.Log($"{workersToSpawn} workers created.");
    }

    public void CreateEnemies(int enemiesToSpawn)
    {
        EnemyRobotFactory enemyRobotFactory = new EnemyRobotFactory();

        spawnedEnemies.Clear();
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            var enemy = ObjectPool.Instance.Get(enemyPrefab, enemiesParent);

            var robotState = enemy.GetComponent<RobotStateController>();
            robotState.Stats = enemyRobotFactory.CreateRobot();
            robotState.Stats.RobotName = $"Enemy {i + 1}";
            enemy.SetActive(false);
            spawnedEnemies.Add(enemy);
        }
        Debug.Log($"{enemiesToSpawn} enemies created.");
    }
    public void CreateBoss()
    {
        EnemyRobotFactory enemyRobotFactory = new EnemyRobotFactory();

        boosInstance = ObjectPool.Instance.Get(bossPrefab, enemiesParent);

        var robotState = this.boosInstance.GetComponent<RobotStateController>();
        robotState.Stats = enemyRobotFactory.CreateRobot();
        robotState.Stats.RobotName = "BOSS 1";

        // Position the boss at the end room center if available
        RoomWaypoint endPoint = waypointService.GetEndPoint();
        if (endPoint != null)
        {
            boosInstance.transform.position = endPoint.WorldPos;
        }
        else
        {
            Debug.LogWarning("[EnemiesSpawner] No end room found for boss spawn.");
        }

        // Activate and initialise the boss AI
        boosInstance.SetActive(true);
        var ec = boosInstance.GetComponent<EnemyController>();
        ec.Initialize(waypointService, waypointService, respawnService, dropContainer,securityBadgeSpawner);
        ec.SetSecurityGuardState();
        if (endPoint != null)
            ec.memory.SetLastVisitedPoint(endPoint);

        Debug.Log("Boss created.");
    }

    public void CreateWorkersSpawner(int workersToSpawn)
    {
        WorkerRobotFactory workerRobotFactory = new WorkerRobotFactory();
        spawnedWorkerSpawners.Clear();
        for (int i = 0; i < workersToSpawn; i++)
        {
            var worker = Instantiate(workerPrefab,
                Vector3.zero,
                Quaternion.identity,
                enemiesParent);
            var robotState = worker.GetComponent<RobotStateController>();
            robotState.Stats = workerRobotFactory.CreateRobot();
            robotState.Stats.RobotName = $"WorkerSpawner {i + 1}";
            worker.SetActive(false);
            spawnedWorkerSpawners.Add(worker);
        }
        Debug.Log($"{workersToSpawn} workers created.");
    }
    public GameObject CreateAngGetFollowerGuard()
    {
        EnemyRobotFactory enemyRobotFactory = new EnemyRobotFactory();

        var follower = Instantiate(bossPrefab,
                Vector3.zero,
                Quaternion.identity,
                enemiesParent);

        var robotState = follower.GetComponent<RobotStateController>();
        robotState.Stats = enemyRobotFactory.CreateRobot();
        robotState.Stats.RobotName = "Follower Guard";
        follower.SetActive(false);

        // 3) NOW it’s in the world at the correct spot — initialize its AI
        var ec = follower.GetComponent<EnemyController>();
        ec.Initialize(waypointService, waypointService, respawnService, dropContainer,securityBadgeSpawner);

        Debug.Log($"Follower guard created.");
        return follower;
    }
    public void SpreadEnemies()
    {
        //workers
        foreach (var worker in spawnedWorkers)
        {
            // 1) pick & apply a real spawn position
            RoomWaypoint spawnPos = waypointService.GetWorkOrRestPoint();
            worker.transform.position = spawnPos.WorldPos;
            // 2) turn it on
            worker.SetActive(true);

            // 3) NOW it’s in the world at the correct spot — initialize its AI
            var ec = worker.GetComponent<EnemyWorkerController>();
            ec.Initialize(waypointService, waypointService, respawnService);

            ec.memory.SetLastVisitedPoint(spawnPos);
            Debug.Log($"Worker spread to {spawnPos.WorldPos} and initialized");
        }

        //workers spawners
        foreach (var workerSpawner in spawnedWorkerSpawners)
        {
            // 1) pick & apply a real spawn position
            RoomWaypoint spawnPos = waypointService.GetBlockedRoomCenter();
            workerSpawner.transform.position = spawnPos.WorldPos;
            // 2) turn it on
            workerSpawner.SetActive(true);

            // 3) NOW it’s in the world at the correct spot — initialize its AI
            var ec = workerSpawner.GetComponent<EnemyWorkerController>();
            ec.SetWorkerSpawnerState();
            ec.Initialize(waypointService, waypointService, respawnService);
            ec.memory.SetLastVisitedPoint(spawnPos);
            Debug.Log($"Worker spread to {spawnPos.WorldPos} and initialized");
        }

        //enemies
        for (int i = 0; i < spawnedEnemies.Count; i++)
        {
            var enemy = spawnedEnemies[i];
            // 1) pick & apply a real spawn position
            RoomWaypoint spawnPos = waypointService.GetSecurityOrRestPoint();

            enemy.transform.position = spawnPos.WorldPos;

            // 2) turn it on
            enemy.SetActive(true);

            // 3) NOW it’s in the world at the correct spot — initialize its AI
            var ec = enemy.GetComponent<EnemyController>();
            ec.Initialize(waypointService, waypointService, respawnService, dropContainer,securityBadgeSpawner);
            ec.SetSecurityGuardState();
            ec.memory.SetLastVisitedPoint(spawnPos);
            var guardAI = enemy.GetComponent<ReactiveMachineAI>();
            guardAI?.Initialize(waypointService, securityManager);

            // release the reservation so the point can be reused later
            waypointService.ReleasePOI(spawnPos);

            Debug.Log($"Enemy spread to {spawnPos} and initialized");
        }

        //boss
        if (boosInstance != null)
        {
            RoomWaypoint spawnPos = waypointService.GetEndPoint();
            boosInstance.transform.position = spawnPos.WorldPos;
            boosInstance.SetActive(true);

            // 3) NOW it’s in the world at the correct spot — initialize its AI
            var ec = boosInstance.GetComponent<EnemyController>();
            ec.Initialize(waypointService, waypointService, respawnService, dropContainer,securityBadgeSpawner);
            ec.SetSecurityGuardState();
            ec.memory.SetLastVisitedPoint(spawnPos);

            Debug.Log($"Boss spread to {spawnPos} and initialized");
        }
    }

    /// <summary>
    /// Instantiates exactly one new enemy prefab, places it at a random free spot, 
    /// calls Initialize on its EnemyController, and stores it in our list.
    /// </summary>
    public void SpawnEnemyAtRandom()
    {
        // 1) Grab a worker from the pool.
        GameObject workerGO = ObjectPool.Instance.Get(workerPrefab, enemiesParent);
        SpawnInstanceAtRandom(workerGO);
    }
    public void SpawnBossAtRandom()
    {
        // 1) Create a fresh GameObject (no pooling; pure new instance).
        GameObject enemyGO = Instantiate(workerPrefab, Vector3.zero, Quaternion.identity, enemiesParent);

        SpawnInstanceAtRandom(enemyGO);
    }

    public void SpawnInstanceAtRandom(GameObject enemyGO)
    {
        // 2) Pick a random empty position
        Vector3 spawnPos = mapManager.GetRandomEmptyPosition();
        enemyGO.transform.position = spawnPos;

        // 3) Activate it
        enemyGO.SetActive(true);

        // 4) Initialize its AI (waypoint service, etc.)
        var ec = enemyGO.GetComponent<EnemyWorkerController>();
        ec.Initialize(waypointService, waypointService, respawnService);
        var guardAI = enemyGO.GetComponent<ReactiveMachineAI>();
        guardAI?.Initialize(waypointService, securityManager);

        // 5) Keep track of it
        spawnedWorkers.Add(enemyGO);

        Debug.Log($"[EnemiesSpawner] Spawned new enemy at {spawnPos}.");
    }

}

