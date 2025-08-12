using UnityEngine;
using System.Collections.Generic;
using System;

public class EnemiesSpawner : MonoBehaviour, IEnemiesSpawner, IDropHost
{
    [Header("Prefabs")]
    [SerializeField] private GameObject workerPrefab;
    [SerializeField] private GameObject workerSpawnerPrefab;   // NEW (Worker that acts as a spawner)
    [SerializeField] private GameObject followerGuardPrefab;   // NEW (Follower AI variant)
    [SerializeField] private GameObject securityGuardPrefab;   // NEW (Standard security guard)
    [SerializeField] private GameObject bossPrefab;

    [Header("Hierarchy")]
    [SerializeField] private Transform enemiesParent;
    private Transform dropContainer;

    // Services
    private MapManager mapManager;
    private IWaypointService waypointService;
    private IRobotRespawnService respawnService;
    private MachineSecurityManager securityManager;
    private SecurityBadgeSpawner securityBadgeSpawner;
    private BatterySpawner batterySpawner;
    private GameUIViewModel gameUIViewModel;

    // Spawned instances
    private readonly List<GameObject> spawnedWorkers = new();
    private readonly List<GameObject> spawnedWorkerSpawners = new();
    private readonly List<GameObject> spawnedSecurityGuards = new();
    private readonly List<GameObject> spawnedFollowers = new();
    private GameObject bossInstance;

    public Transform DropContainer => dropContainer;

    public void SetDropContainer(Transform container) => dropContainer = container;

    public void Initialize(
        MapManager mapManager,
        IWaypointService waypointService,
        GameUIViewModel viewModel,
        IRobotRespawnService respawnService,
        MachineSecurityManager securityManager,
        SecurityBadgeSpawner securityBadgeSpawner,
        BatterySpawner batterySpawner)
    {
        this.mapManager = mapManager;
        this.waypointService = waypointService;
        this.gameUIViewModel = viewModel;
        this.respawnService = respawnService;
        this.securityManager = securityManager;
        this.securityBadgeSpawner = securityBadgeSpawner;
        this.batterySpawner = batterySpawner;

        if (this.securityManager != null)
            this.securityManager.OnAllMachinesOff += HandleAllMachinesOff;

        if (respawnService is RobotRespawnService service)
            service.Initialize(this);

        Debug.Log("EnemiesSpawner: services initialized.");
    }

    // ------------------------------------------------------------------------
    // CREATE (allocate but NOT placed/activated)
    // ------------------------------------------------------------------------

    public void CreateWorkers(int count)
    {
        var factory = new WorkerRobotFactory();
        spawnedWorkers.Clear();

        for (int i = 0; i < count; i++)
        {
            var go = PoolGet(workerPrefab);
            var state = go.GetComponent<RobotStateController>();
            state.Stats = factory.CreateRobot();
            state.Stats.RobotName = $"Worker {i + 1}";
            go.SetActive(false);
            spawnedWorkers.Add(go);
        }

        Debug.Log($"{count} workers created.");
    }

    public void CreateWorkerSpawners(int count)
    {
        var factory = new WorkerRobotFactory();
        spawnedWorkerSpawners.Clear();

        for (int i = 0; i < count; i++)
        {
            var go = PoolGet(workerSpawnerPrefab);
            var state = go.GetComponent<RobotStateController>();
            state.Stats = factory.CreateRobot();
            state.Stats.RobotName = $"WorkerSpawner {i + 1}";
            go.SetActive(false);
            spawnedWorkerSpawners.Add(go);
        }

        Debug.Log($"{count} worker spawners created.");
    }

    public void CreateSecurityGuards(int count)
    {
        var factory = new EnemyRobotFactory(2);
        spawnedSecurityGuards.Clear();

        for (int i = 0; i < count; i++)
        {
            var go = PoolGet(securityGuardPrefab);
            var state = go.GetComponent<RobotStateController>();
            state.Stats = factory.CreateRobot();
            state.Stats.RobotName = $"Security Guard {i + 1}";
            go.SetActive(false);
            spawnedSecurityGuards.Add(go);
        }

        Debug.Log($"{count} security guards created.");
    }

    public void CreateBoss()
    {
        var factory = new EnemyRobotFactory(3);
        bossInstance = PoolGet(bossPrefab);

        var locomotion = bossInstance.GetComponent<RobotLocomotionController>();
        if (locomotion != null) locomotion.isPlayerControlled = false;

        var state = bossInstance.GetComponent<RobotStateController>();
        state.Stats = factory.CreateRobot();
        state.Stats.RobotName = "BOSS 1";

        // Place at END if available (kept here so we can immediately show it during Spread)
        RoomWaypoint end = waypointService.GetEndPoint();
        bossInstance.transform.position = (end != null) ? end.WorldPos : Vector3.zero;

        bossInstance.SetActive(false);
        Debug.Log("Boss created.");
    }
    public void CreateAndSpawnSecurityGuard(RoomWaypoint spawnPos, SecurityMachine machine)
    {
        // Use the dedicated prefab if assigned; fallback to securityGuardPrefab’s existing value.
        var prefab = securityGuardPrefab != null ? securityGuardPrefab : bossPrefab; // last-ditch fallback
        if (prefab == null)
        {
            Debug.LogError("[EnemiesSpawner] No security guard prefab assigned.");
            return;
        }

        // Pool, place, init
        var guard = PoolGet(prefab);
        guard.transform.position = spawnPos.WorldPos;
        PrepareSkeleton(guard);
        guard.SetActive(true);

        // Initialize controller + state
        var ec = guard.GetComponent<EnemyController>();
        if (ec != null)
        {
            ec.Initialize(waypointService, waypointService, respawnService, dropContainer, securityBadgeSpawner, batterySpawner);
            ec.SetSecurityGuardState();
            ec.memory.SetLastVisitedPoint(spawnPos);
        }

        // Reactive machine hookup
        var guardAI = guard.GetComponent<ReactiveMachineAI>();
        guardAI?.Initialize(waypointService, securityManager);
        guardAI?.ReactivateSecurityMachine(machine);

        // Track it alongside pre-created guards list so spread/cleanup logic remains coherent
        spawnedSecurityGuards.Add(guard);

        Debug.Log("[EnemiesSpawner] Security guard created & spawned (legacy API).");
    }

    // Followers are typically created and spawned at once (on alarm/reactive events).
    public void CreateAndSpawnFollowerGuard(RoomWaypoint spawnPos, FactoryAlarmStatus alarmStatus)
    {
        var factory = new EnemyRobotFactory(1);

        var go = PoolGet(followerGuardPrefab);
        var state = go.GetComponent<RobotStateController>();
        state.Stats = factory.CreateRobot();
        state.Stats.RobotName = $"Follower {spawnedFollowers.Count + 1}";

        PositionAndWake(go, spawnPos.WorldPos);
        InitEnemyController(go);
        go.GetComponent<EnemyController>()?.SetFollowerState(alarmStatus);
        go.GetComponent<EnemyController>()?.memory.SetLastVisitedPoint(spawnPos);

        var ai = go.GetComponent<ReactiveMachineAI>();
        ai?.Initialize(waypointService, securityManager);

        spawnedFollowers.Add(go);
        Debug.Log("Follower guard created & spawned.");
    }

    // ------------------------------------------------------------------------
    // SPREAD (place, activate, initialize)
    // ------------------------------------------------------------------------

    public void SpreadEnemies()
    {
        // Workers
        foreach (var w in spawnedWorkers)
        {
            var p = waypointService.GetWorkOrRestPoint();
            PositionAndWake(w, p.WorldPos);
            var c = w.GetComponent<EnemyWorkerController>();
            c.Initialize(waypointService, waypointService, respawnService);
            c.memory.SetLastVisitedPoint(p);
            Debug.Log($"Worker spread to {p.WorldPos} and initialized");
        }

        // Worker spawners
        foreach (var ws in spawnedWorkerSpawners)
        {
            var p = waypointService.GetBlockedRoomCenter();
            PositionAndWake(ws, p.WorldPos);
            var c = ws.GetComponent<EnemyWorkerController>();
            c.SetWorkerSpawnerState();
            c.Initialize(waypointService, waypointService, respawnService);
            c.memory.SetLastVisitedPoint(p);
            Debug.Log($"Worker spawner spread to {p.WorldPos} and initialized");
        }

        // Security guards
        foreach (var eg in spawnedSecurityGuards)
        {
            var p = waypointService.GetSecurityOrRestPoint();
            PositionAndWake(eg, p.WorldPos);

            InitEnemyController(eg);
            var ai = eg.GetComponent<ReactiveMachineAI>();
            ai?.Initialize(waypointService, securityManager);

            // Release reservation for later reuse
            waypointService.ReleasePOI(p);

            eg.GetComponent<EnemyController>()?.SetSecurityGuardState();
            eg.GetComponent<EnemyController>()?.memory.SetLastVisitedPoint(p);

            Debug.Log($"Security guard spread to {p.WorldPos} and initialized");
        }

        // Boss
        if (bossInstance != null)
        {
            var p = waypointService.GetEndPoint();
            PositionAndWake(bossInstance, (p != null) ? p.WorldPos : bossInstance.transform.position);
            InitEnemyController(bossInstance);
            bossInstance.GetComponent<EnemyController>()?.SetBossState();
            bossInstance.GetComponent<EnemyController>()?.memory.SetLastVisitedPoint(p);
            Debug.Log("Boss spread and initialized");
        }
    }

    // ------------------------------------------------------------------------
    // ON-DEMAND / RANDOM SPAWN (utility)
    // ------------------------------------------------------------------------

    public void SpawnEnemyAtRandom()
    {
        // Example: spawn a NEW worker at a random work cell
        var go = PoolGet(workerPrefab);
        var pos = mapManager.GetRandomWorkPosition();

        PrepareSkeleton(go);
        PositionAndWake(go, pos);

        var c = go.GetComponent<EnemyWorkerController>();
        c.Initialize(waypointService, waypointService, respawnService);

        var ai = go.GetComponent<ReactiveMachineAI>();
        ai?.Initialize(waypointService, securityManager);

        spawnedWorkers.Add(go);
        Debug.Log($"[EnemiesSpawner] Spawned worker at {pos}.");
    }

    public void SpawnBossAtRandom()
    {
        var go = PoolGet(bossPrefab);
        var pos = mapManager.GetRandomWorkPosition();

        PrepareSkeleton(go);
        PositionAndWake(go, pos);
        InitEnemyController(go);
        go.GetComponent<EnemyController>()?.SetBossState();

        bossInstance = go;
        Debug.Log($"[EnemiesSpawner] Spawned boss at {pos}.");
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    private GameObject PoolGet(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[EnemiesSpawner] Missing prefab reference.");
            return null;
        }
        return ObjectPool.Instance.Get(prefab, enemiesParent);
    }

    private void InitEnemyController(GameObject go)
    {
        var ec = go.GetComponent<EnemyController>();
        if (ec != null)
            ec.Initialize(waypointService, waypointService, respawnService, dropContainer, securityBadgeSpawner, batterySpawner);
    }

    private void PositionAndWake(GameObject go, Vector3 worldPos)
    {
        if (go == null) return;
        go.transform.position = worldPos;
        PrepareSkeleton(go);
        go.SetActive(true);
    }

    /// <summary>
    /// Reset joints/limiters for pooled ragdolls before activation.
    /// </summary>
    private void PrepareSkeleton(GameObject go)
    {
        go.GetComponent<JointBreaker>()?.RestoreAll();

        var bodyLimiter = go.GetComponent<BodyJointLimiter>();
        if (bodyLimiter != null)
        {
            bodyLimiter.RefreshJoints();
            bodyLimiter.enabled = true;
        }

        var legLimiter = go.GetComponent<LegJointLimiter>();
        if (legLimiter != null)
        {
            legLimiter.RefreshJoints();
            legLimiter.enabled = true;
        }
    }

    private void HandleAllMachinesOff()
    {
        var enemies = FindObjectsOfType<EnemyController>();
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.IsBoss)
            {
                enemy.Faint();
                break;
            }
        }
    }

    private void OnDestroy()
    {
        if (securityManager != null)
            securityManager.OnAllMachinesOff -= HandleAllMachinesOff;
    }
}
