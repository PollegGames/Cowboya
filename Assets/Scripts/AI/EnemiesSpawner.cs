using System.Collections.Generic;
using UnityEngine;

public class EnemiesSpawner : MonoBehaviour, IEnemiesSpawner, IDropHost
{
    [Header("Prefabs")]
    [SerializeField] private GameObject workerPrefab;
    [SerializeField] private GameObject workerSpawnerPrefab;
    [SerializeField] private GameObject followerGuardPrefab;
    [SerializeField] private GameObject securityGuardPrefab;
    [SerializeField] private GameObject bossPrefab;

    [Header("Hierarchy")]
    [SerializeField] private Transform enemiesParent;
    private Transform dropContainer;

    private MapManager mapManager;
    private IWaypointService waypointService;
    private IRobotRespawnService respawnService;
    private MachineSecurityManager securityManager;
    private SecurityBadgeSpawner securityBadgeSpawner;
    private BatterySpawner batterySpawner;
    private GameUIViewModel gameUIViewModel;

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
    }

    public void CreateWorkers(int count)
    {
        var factory = new WorkerRobotFactory();
        spawnedWorkers.Clear();

        for (int i = 0; i < count; i++)
        {
            var go = PoolGet(workerPrefab);
            if (go == null)
                continue;

            var state = go.GetComponent<RobotStateController>();
            if (state == null)
            {
                ObjectPool.Instance.Release(go);
                continue;
            }

            state.Stats = factory.CreateRobot();
            state.Stats.RobotName = $"Worker {i + 1}";
            go.SetActive(false);
            spawnedWorkers.Add(go);
        }
    }

    public void CreateWorkerSpawners(int count)
    {
        var factory = new WorkerRobotFactory();
        spawnedWorkerSpawners.Clear();

        for (int i = 0; i < count; i++)
        {
            var go = PoolGet(workerSpawnerPrefab);
            if (go == null)
                continue;

            var state = go.GetComponent<RobotStateController>();
            if (state == null)
            {
                ObjectPool.Instance.Release(go);
                continue;
            }

            state.Stats = factory.CreateRobot();
            state.Stats.RobotName = $"WorkerSpawner {i + 1}";
            go.SetActive(false);
            spawnedWorkerSpawners.Add(go);
        }
    }

    public void CreateSecurityGuards(int count)
    {
        var factory = new EnemyRobotFactory(2);
        spawnedSecurityGuards.Clear();

        for (int i = 0; i < count; i++)
        {
            var go = PoolGet(securityGuardPrefab);
            var state = go.GetComponent<RobotStateController>();
            if (state != null)
            {
                state.Stats = factory.CreateRobot();
                state.Stats.RobotName = $"Security Guard {i + 1}";
            }
            go.SetActive(false);
            spawnedSecurityGuards.Add(go);
        }
    }

    public void CreateBoss()
    {
        var factory = new EnemyRobotFactory(3);
        bossInstance = PoolGet(bossPrefab);

        var locomotion = bossInstance.GetComponent<RobotLocomotionController>();
        if (locomotion != null) locomotion.isPlayerControlled = false;

        var state = bossInstance.GetComponent<RobotStateController>();
        if (state != null)
        {
            state.Stats = factory.CreateRobot();
            state.Stats.RobotName = "BOSS 1";
        }

        RoomWaypoint end = waypointService.GetEndPoint();
        bossInstance.transform.position = (end != null) ? end.WorldPos : Vector3.zero;
        bossInstance.SetActive(false);
    }

    public void CreateAndSpawnSecurityGuard(RoomWaypoint spawnPos, SecurityMachine machine)
    {
        var prefab = securityGuardPrefab != null ? securityGuardPrefab : bossPrefab;
        if (prefab == null)
        {
            Debug.LogError("[EnemiesSpawner] No security guard prefab assigned.");
            return;
        }

        var guard = PoolGet(prefab);
        guard.transform.position = spawnPos.WorldPos;
        PrepareSkeleton(guard);
        InitializeRobot(guard, RobotRole.SecurityGuard, spawnPos);
        guard.SetActive(true);
        spawnedSecurityGuards.Add(guard);
    }

    public void CreateAndSpawnFollowerGuard(RoomWaypoint spawnPos, FactoryAlarmStatus alarmStatus)
    {
        var factory = new EnemyRobotFactory(1);

        var go = PoolGet(followerGuardPrefab);
        if (go == null)
            return;

        if (spawnPos == null)
        {
            Debug.LogWarning("[EnemiesSpawner] Cannot spawn follower: spawn position is null.");
            return;
        }

        var state = go.GetComponent<RobotStateController>();
        if (state != null)
        {
            state.Stats = factory.CreateRobot();
            state.Stats.RobotName = $"Follower {spawnedFollowers.Count + 1}";
        }

        PositionAndWake(go, spawnPos.WorldPos);
        InitializeRobot(go, RobotRole.Follower, spawnPos);

        var brain = go.GetComponent<RobotBrain>();
        if (brain != null)
        {
            Vector3 targetPosition = alarmStatus != null ? alarmStatus.LastPlayerPosition : Vector3.zero;
            if (targetPosition != Vector3.zero)
            {
                brain.Memory?.RememberPlayerPosition(targetPosition);
                brain.PushExplicitTask(RobotTaskType.ChasePlayer, targetPosition);
            }
            else if (waypointService != null && waypointService.ClosestWaypointToPlayer != null)
            {
                var playerWaypoint = waypointService.ClosestWaypointToPlayer;
                brain.Memory?.RememberPlayerPosition(playerWaypoint.WorldPos);
                brain.PushExplicitTask(RobotTaskType.ChasePlayer, playerWaypoint);
            }
            else
            {
                brain.PushExplicitTask(RobotTaskType.Patrol, spawnPos);
            }
        }

        spawnedFollowers.Add(go);
    }

    public void SpreadEnemies()
    {
        foreach (var w in spawnedWorkers)
        {
            if (w == null)
                continue;

            var p = waypointService.GetWorkOrRestPoint();
            PositionAndWake(w, p.WorldPos);
            InitializeRobot(w, RobotRole.Worker, p);
        }

        foreach (var ws in spawnedWorkerSpawners)
        {
            if (ws == null)
                continue;

            var p = waypointService.GetBlockedRoomSecuritySpawning();
            PositionAndWake(ws, p.WorldPos);
            InitializeRobot(ws, RobotRole.WorkerSpawner, p);
        }

        foreach (var eg in spawnedSecurityGuards)
        {
            var p = waypointService.GetSecurityOrRestPoint();
            PositionAndWake(eg, p.WorldPos);
            waypointService.ReleasePOI(p);
            InitializeRobot(eg, RobotRole.SecurityGuard, p);
        }

        if (bossInstance != null)
        {
            var p = waypointService.GetEndPoint();
            PositionAndWake(bossInstance, (p != null) ? p.WorldPos : bossInstance.transform.position);
            InitializeRobot(bossInstance, RobotRole.Boss, p);
        }
    }

    public void SpawnEnemyAtRandom()
    {
        var go = PoolGet(workerPrefab);
        if (go == null)
            return;

        var pos = mapManager.GetRandomWorkPosition();
        PrepareSkeleton(go);
        PositionAndWake(go, pos);
        InitializeRobot(go, RobotRole.Worker);

        spawnedWorkers.Add(go);
    }

    public void SpawnBossAtRandom()
    {
        var go = PoolGet(bossPrefab);
        var pos = mapManager.GetRandomWorkPosition();

        PrepareSkeleton(go);
        PositionAndWake(go, pos);
        InitializeRobot(go, RobotRole.Boss);

        bossInstance = go;
    }

    private GameObject PoolGet(GameObject prefab)
    {
        if (prefab == null)
            return null;
        return ObjectPool.Instance.Get(prefab, enemiesParent);
    }

    private void PositionAndWake(GameObject go, Vector3 worldPos)
    {
        if (go == null) return;
        go.transform.position = worldPos;
        PrepareSkeleton(go);
        go.SetActive(true);
    }

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
        var hearts = FindObjectsByType<RobotHeart>(FindObjectsSortMode.None);
        foreach (var heart in hearts)
        {
            if (heart != null && heart.Role == RobotRole.Boss)
            {
                var state = heart.GetComponent<RobotStateController>();
                if (state != null)
                    state.UpdateState(RobotState.Faint);
                break;
            }
        }
    }

    private void OnDestroy()
    {
        if (securityManager != null)
            securityManager.OnAllMachinesOff -= HandleAllMachinesOff;
    }

    private void InitializeRobot(GameObject go, RobotRole role, RoomWaypoint lastVisited = null)
    {
        if (go == null)
            return;

        var brain = go.GetComponent<RobotBrain>();
        if (brain != null)
        {
            brain.InitializeServices(waypointService, respawnService);
            if (role == RobotRole.SecurityGuard && securityManager != null)
                securityManager.RegisterGuard(brain);
        }

        var bodyController = go.GetComponent<RobotBodyController>();
        if (bodyController != null)
            bodyController.SetIsBoss(role == RobotRole.Boss);

        var maintenance = go.GetComponent<RobotBodyMaintenance>();
        if (maintenance != null && respawnService != null)
            maintenance.SetRespawnService(respawnService);

        var heart = go.GetComponent<RobotHeart>();
        if (heart != null && heart.Role != role)
        {
            Debug.LogWarning($"[EnemiesSpawner] RobotHeart on {go.name} has role {heart.Role} but was initialized as {role}.");
        }

        var memory = go.GetComponent<RobotMemory>();
        if (memory != null && lastVisited != null)
            memory.SetLastVisitedPoint(lastVisited);

        // Attach a security badge to guards so it can be stolen later.
        if (role == RobotRole.SecurityGuard && securityBadgeSpawner != null)
        {
            Transform anchor = go.transform;
            var body = go.GetComponent<RobotBodyController>();
            if (body != null && body.BodyReference != null)
                anchor = body.BodyReference;

            var badge = securityBadgeSpawner.SpawnBadge(anchor);
            var inventory = go.GetComponent<Inventory>();
            if (badge != null && inventory != null)
                inventory.SetItem(PickupType.SecurityBadge, badge);
        }
    }

}
