using UnityEngine;
using System.Collections.Generic;

public class EnemiesSpawner : MonoBehaviour
{
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform enemiesParent;
    // Expose enemies count via public property
    private MapManager mapManager;
    private IWaypointService waypointService;
    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private List<GameObject> spawnedboss = new List<GameObject>();
    private GameUIViewModel gameUIViewModel;

    private void Awake()
    {
        if (enemyPrefab == null)
        {
            Debug.LogError("EnemiesSpawner: enemyPrefab is not assigned.");
        }
    }

    public void InitMapManager(MapManager mapManager, IWaypointService waypointService, GameUIViewModel viewModel)
    {
        this.waypointService = waypointService;
        this.mapManager = mapManager;
        this.gameUIViewModel = viewModel;
        Debug.Log("EnemiesSpawner: MapManager and WaypointService initialized.");
    }

    // Create enemies and store them, but don't position them yet
    public void CreateEnemies(int enemiesToSpawn)
    {
        spawnedEnemies.Clear();
        for (int i = 0; i < enemiesToSpawn; i++)
        {
            var enemy = Instantiate(enemyPrefab,
                Vector3.zero,
                Quaternion.identity,
                enemiesParent);

            enemy.SetActive(false);
            spawnedEnemies.Add(enemy);
        }
        Debug.Log($"{enemiesToSpawn} enemies created.");
    }


    public void CreateBoss(int bossToSpawn)
    {
        spawnedboss.Clear();
        for (int i = 0; i < bossToSpawn; i++)
        {
            var boss = Instantiate(bossPrefab,
                Vector3.zero,
                Quaternion.identity,
                enemiesParent);

            boss.SetActive(false);
            spawnedboss.Add(boss);
        }
        Debug.Log($"{bossToSpawn} boss created.");
    }


    public void SpreadEnemies()
    {
        if (spawnedEnemies.Count == 0)
        {
            Debug.LogWarning("No enemies to spread. Call CreateEnemies first.");
            return;
        }

        //workers
        foreach (var enemy in spawnedEnemies)
        {
            // 1) pick & apply a real spawn position
            var spawnPos = waypointService.GetWorkOrRestPoint();
            enemy.transform.position = spawnPos.WorldPos;
            // 2) turn it on
            enemy.SetActive(true);

            // 3) NOW it’s in the world at the correct spot — initialize its AI
            var ec = enemy.GetComponent<EnemyController>();
            ec.Initialize(waypointService, waypointService, this);

            Debug.Log($"Enemy spread to {spawnPos} and initialized");
        }

        for (int i = 0; i < spawnedboss.Count; i++)
        {
            var enemy = spawnedboss[i];
            // 1) pick & apply a real spawn position
            RoomWaypoint spawnPos;
            if (i == 0)
            {
                spawnPos = waypointService.GetEndPoint();
            }
            else
            {
                spawnPos = waypointService.GetFirstFreeSecurityPoint();
            }
            enemy.transform.position = spawnPos.WorldPos;

            // 2) turn it on
            enemy.SetActive(true);

            // 3) NOW it’s in the world at the correct spot — initialize its AI
            var ec = enemy.GetComponent<EnemyBossController>();
            ec.Initialize(waypointService, waypointService, this);

            Debug.Log($"Boss spread to {spawnPos} and initialized");
        }
    }

    /// <summary>
    /// Instantiates exactly one new enemy prefab, places it at a random free spot, 
    /// calls Initialize on its EnemyController, and stores it in our list.
    /// </summary>
    public void SpawnEnemyAtRandom()
    {
        // 1) Create a fresh GameObject (no pooling; pure new instance).
        GameObject enemyGO = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity, enemiesParent);
        SpawnInstanceAtRandom(enemyGO);
    }
    public void SpawnBossAtRandom()
    {
        // 1) Create a fresh GameObject (no pooling; pure new instance).
        GameObject enemyGO = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity, enemiesParent);

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
        var ec = enemyGO.GetComponent<EnemyController>();
        ec.Initialize(waypointService, waypointService, this);

        // 5) Let its Memory know who the spawner is, so it can call back on “OnStuck”
        var mem = enemyGO.GetComponent<EnemyMemory>();

        // 6) Keep track of it
        spawnedEnemies.Add(enemyGO);

        Debug.Log($"[EnemiesSpawner] Spawned new enemy at {spawnPos}.");
    }

}

