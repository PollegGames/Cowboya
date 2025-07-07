using UnityEngine;
using System.Collections.Generic;

public class EnemiesSpawner : MonoBehaviour
{
    [SerializeField] private GameObject workerPrefab;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform enemiesParent;
    // Expose enemies count via public property
    private MapManager mapManager;
    private IWaypointService waypointService;
    private IRobotRespawnService respawnService;
    private List<GameObject> spawnedWorkers = new List<GameObject>();
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private GameUIViewModel gameUIViewModel;

    public void Initialize(MapManager mapManager, IWaypointService waypointService, GameUIViewModel viewModel, IRobotRespawnService respawnService)
    {
        this.waypointService = waypointService;
        this.mapManager = mapManager;
        this.gameUIViewModel = viewModel;
        this.respawnService = respawnService;

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
            var worker = Instantiate(workerPrefab,
                Vector3.zero,
                Quaternion.identity,
                enemiesParent);
            var robotState = worker.GetComponent<RobotStateController>();
            robotState.Stats = workerRobotFactory.CreateRobot();
            robotState.Stats.RobotName = $"Worker {i + 1}";
            worker.SetActive(false);
            spawnedWorkers.Add(worker);
        }
        Debug.Log($"{workersToSpawn} enemies created.");
    }


    public void CreateEnemy(int enemiesToSpawn)
    {
        EnemyRobotFactory enemyRobotFactory = new EnemyRobotFactory();

        spawnedEnemies.Clear();
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            var enemy = Instantiate(enemyPrefab,
                Vector3.zero,
                Quaternion.identity,
                enemiesParent);

            var robotState = enemy.GetComponent<RobotStateController>();
            robotState.Stats = enemyRobotFactory.CreateRobot();
            robotState.Stats.RobotName = $"Worker {i + 1}";
            enemy.SetActive(false);
            spawnedEnemies.Add(enemy);
        }
        Debug.Log($"{enemiesToSpawn} boss created.");
    }


    public void SpreadEnemies()
    {
        if (spawnedWorkers.Count == 0)
        {
            Debug.LogWarning("No enemies to spread. Call CreateEnemies first.");
            return;
        }

        //workers
        foreach (var worker in spawnedWorkers)
        {
            // 1) pick & apply a real spawn position
            var spawnPos = waypointService.GetWorkOrRestPoint();
            worker.transform.position = spawnPos.WorldPos;
            // 2) turn it on
            worker.SetActive(true);

            // 3) NOW it’s in the world at the correct spot — initialize its AI
            var ec = worker.GetComponent<EnemyWorkerController>();
            ec.Initialize(waypointService, waypointService, respawnService);

            Debug.Log($"Worker spread to {spawnPos} and initialized");
        }

        //enemies
        for (int i = 0; i < spawnedEnemies.Count; i++)
        {
            var enemy = spawnedEnemies[i];
            // 1) pick & apply a real spawn position
            RoomWaypoint spawnPos;
            if (i == 0)
            {
                //the boss always spawns at the end point
                spawnPos = waypointService.GetEndPoint();
            }
            else
            {
                //other enemies spawn at the first free security point
                spawnPos = waypointService.GetFirstFreeSecurityPoint();
            }
            enemy.transform.position = spawnPos.WorldPos;

            // 2) turn it on
            enemy.SetActive(true);

            // 3) NOW it’s in the world at the correct spot — initialize its AI
            var ec = enemy.GetComponent<EnemyController>();
            ec.Initialize(waypointService, waypointService, respawnService);

            Debug.Log($"Enemy spread to {spawnPos} and initialized");
        }
    }

    /// <summary>
    /// Instantiates exactly one new enemy prefab, places it at a random free spot, 
    /// calls Initialize on its EnemyController, and stores it in our list.
    /// </summary>
    public void SpawnEnemyAtRandom()
    {
        // 1) Create a fresh GameObject (no pooling; pure new instance).
        GameObject workerGO = Instantiate(workerPrefab, Vector3.zero, Quaternion.identity, enemiesParent);
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

        // 5) Keep track of it
        spawnedWorkers.Add(enemyGO);

        Debug.Log($"[EnemiesSpawner] Spawned new enemy at {spawnPos}.");
    }

}

