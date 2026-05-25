using System.Collections.Generic;
using System.Linq;
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
    private IFactoryManager factoryManager;
    private MachineSecurityManager securityManager;
    private SecurityBadgeSpawner securityBadgeSpawner;
    private BatterySpawner batterySpawner;
    private GameUIViewModel gameUIViewModel;
    private FactoryAlarmStatus followerAlarmStatus;

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
        IFactoryManager factoryManager,
        MachineSecurityManager securityManager,
        SecurityBadgeSpawner securityBadgeSpawner,
        BatterySpawner batterySpawner)
    {
        this.mapManager = mapManager;
        if (this.waypointService != null)
            this.waypointService.OnClosestWaypointToPlayerChanged -= HandleClosestWaypointToPlayerChanged;

        this.waypointService = waypointService;
        this.gameUIViewModel = viewModel;
        this.respawnService = respawnService;
        if (this.factoryManager != null)
            this.factoryManager.OnFactoryAllMachinesOff -= HandleAllMachinesOff;

        this.factoryManager = factoryManager;
        this.securityManager = securityManager;
        this.securityBadgeSpawner = securityBadgeSpawner;
        this.batterySpawner = batterySpawner;

        if (this.factoryManager != null)
            this.factoryManager.OnFactoryAllMachinesOff += HandleAllMachinesOff;
        if (this.waypointService != null)
            this.waypointService.OnClosestWaypointToPlayerChanged += HandleClosestWaypointToPlayerChanged;

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
        _ = machine;
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

        PositionForSpawn(go, spawnPos.WorldPos);
        InitializeRobot(go, RobotRole.Follower, spawnPos);
        Wake(go);

        var brain = go.GetComponent<RobotBrainNew>();
        if (brain != null)
        {
            followerAlarmStatus = alarmStatus;
            Vector3 targetPosition = alarmStatus != null ? alarmStatus.LastPlayerPosition : Vector3.zero;
            if (targetPosition != Vector3.zero)
                waypointService?.UpdateClosestWaypointToPlayer(targetPosition);

            RoomWaypoint playerWaypoint = waypointService != null ? waypointService.ClosestWaypointToPlayer : null;
            if (playerWaypoint != null)
            {
                Vector3 dispatchPosition = targetPosition != Vector3.zero ? targetPosition : playerWaypoint.WorldPos;
                DispatchFollowerPerception(brain, playerWaypoint, dispatchPosition, "spawn_initial");
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
            if (p == null)
            {
                Debug.LogWarning("[EnemiesSpawner] Cannot spawn worker: no work/rest waypoint available.", this);
                continue;
            }

            PositionForSpawn(w, p.WorldPos);
            InitializeRobot(w, RobotRole.Worker, p);
            Wake(w);
        }

        foreach (var ws in spawnedWorkerSpawners)
        {
            if (ws == null)
                continue;

            var p = waypointService.GetBlockedRoomSecuritySpawning();
            if (p == null)
            {
                Debug.LogWarning("[EnemiesSpawner] Cannot spawn worker spawner: no blocked-room spawn waypoint available.", this);
                continue;
            }

            PositionForSpawn(ws, p.WorldPos);
            InitializeRobot(ws, RobotRole.WorkerSpawner, p);
            Wake(ws);
        }

        foreach (var eg in spawnedSecurityGuards)
        {
            var p = waypointService.GetSecurityOrRestPoint();
            RobotEcosystemProbe.RecordSpawnReservationDecision(
                this,
                RobotRole.SecurityGuard,
                p,
                p != null ? "reserved_for_initial_guard_spawn" : "no_security_or_rest_spawn_point");
            if (p == null)
            {
                Debug.LogWarning("[EnemiesSpawner] Cannot spawn security guard: no security/rest waypoint available.", this);
                continue;
            }

            PositionForSpawn(eg, p.WorldPos);
            InitializeRobot(eg, RobotRole.SecurityGuard, p);
            Wake(eg);
        }

        if (bossInstance != null)
        {
            var p = waypointService.GetEndPoint();
            PositionForSpawn(bossInstance, (p != null) ? p.WorldPos : bossInstance.transform.position);
            InitializeRobot(bossInstance, RobotRole.Boss, p);
            Wake(bossInstance);
        }
    }

    public void SpawnEnemyAtRandom()
    {
        var go = PoolGet(workerPrefab);
        if (go == null)
            return;

        var pos = mapManager.GetRandomWorkPosition();
        PositionForSpawn(go, pos);
        InitializeRobot(go, RobotRole.Worker);
        Wake(go);

        spawnedWorkers.Add(go);
    }

    public void SpawnBossAtRandom()
    {
        var go = PoolGet(bossPrefab);
        var pos = mapManager.GetRandomWorkPosition();

        PositionForSpawn(go, pos);
        InitializeRobot(go, RobotRole.Boss);
        Wake(go);

        bossInstance = go;
    }

    private GameObject PoolGet(GameObject prefab)
    {
        if (prefab == null)
            return null;
        var go = ObjectPool.Instance.Get(prefab, enemiesParent);
        EnsureNewPipelineComponents(go);
        return go;
    }

    private void PositionForSpawn(GameObject go, Vector3 worldPos)
    {
        if (go == null) return;
        go.SetActive(false);
        go.transform.position = worldPos;
        PrepareSkeleton(go);
    }

    private static void Wake(GameObject go)
    {
        if (go == null)
            return;
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
        var hearts = FindObjectsByType<RobotHeartNew>(FindObjectsSortMode.None);
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
        if (factoryManager != null)
            factoryManager.OnFactoryAllMachinesOff -= HandleAllMachinesOff;
        if (waypointService != null)
            waypointService.OnClosestWaypointToPlayerChanged -= HandleClosestWaypointToPlayerChanged;
    }

    private void HandleClosestWaypointToPlayerChanged(RoomWaypoint playerWaypoint, Vector2 playerPosition)
    {
        if (playerWaypoint == null || spawnedFollowers.Count == 0)
            return;
        if (followerAlarmStatus != null && followerAlarmStatus.CurrentAlarmState == AlarmState.Normal)
            return;

        for (int i = spawnedFollowers.Count - 1; i >= 0; i--)
        {
            var follower = spawnedFollowers[i];
            if (follower == null)
            {
                spawnedFollowers.RemoveAt(i);
                continue;
            }

            if (!follower.activeInHierarchy)
                continue;

            var brain = follower.GetComponent<RobotBrainNew>();
            if (brain == null || brain.Heart == null || brain.Heart.Role != RobotRole.Follower)
                continue;
            if (brain.Memory != null && brain.Memory.IsDead)
                continue;

            DispatchFollowerPerception(brain, playerWaypoint, playerPosition, "player_waypoint_changed");
        }
    }

    private void DispatchFollowerPerception(
        RobotBrainNew brain,
        RoomWaypoint playerWaypoint,
        Vector3 playerPosition,
        string source)
    {
        if (brain == null || playerWaypoint == null)
            return;

        RobotEcosystemProbe.RecordWaypointDecision(
            this,
            "FollowerPerception.PlayerWaypointDispatch",
            null,
            playerWaypoint,
            "source=" + source
                + " follower=" + brain.name
                + " playerPosition=" + playerPosition.ToString("F2"));

        RobotDomainEventBus.PublishPerceptionDispatch(
            brain,
            playerInDetectZone: true,
            playerInAttackZone: false,
            playerPosition: playerPosition,
            hasKnownPosition: true,
            playerWaypoint: playerWaypoint);
    }

    private void InitializeRobot(GameObject go, RobotRole role, RoomWaypoint lastVisited = null)
    {
        if (go == null)
            return;

        MonoBehaviour owner = go.GetComponent<RobotBrainNew>();
        if (owner == null)
            owner = go.GetComponent<RobotHeartNew>();
        if (owner == null)
            owner = go.GetComponent<MonoBehaviour>();
        RobotEcosystemProbe.RecordSpawn(owner, role, lastVisited);

        var heart = go.GetComponent<RobotHeartNew>();
        if (heart != null)
        {
            heart.ConfigureRole(role, resetStack: true);
            if (heart.Role != role)
                Debug.LogWarning($"[EnemiesSpawner] RobotHeartNew on {go.name} failed role configuration. Expected={role} Actual={heart.Role}");
        }

        var brain = go.GetComponent<RobotBrainNew>();
        if (brain != null)
        {
            if (role == RobotRole.SecurityGuard && securityManager != null)
                securityManager.RegisterGuard(brain);
        }

        var bodyController = go.GetComponent<RobotBodyController>();
        if (bodyController != null)
        {
            if (waypointService != null)
            {
                bodyController.Initialize(
                    waypointService,
                    waypointService,
                    respawnService);
            }
            bodyController.SetIsBoss(role == RobotRole.Boss);
        }

        var maintenance = go.GetComponent<RobotBodyMaintenance>();
        if (maintenance != null && respawnService != null)
            maintenance.SetRespawnService(respawnService);

        var memory = go.GetComponent<RobotMemoryNew>();
        if (memory != null && lastVisited != null)
            memory.SetLastVisitedPoint(lastVisited);

        var memoryNew = go.GetComponent<RobotMemoryNew>();
        if (memoryNew != null)
        {
            var seedWaypoints = waypointService != null ? waypointService.GetAllWaypoints() : null;
            memoryNew.InitializeWaypointAvailability(seedWaypoints);
            // Spawn-time bootstrap: force an initial navigable map so BrainNew can start
            // the first work/rest cycle even if RoomWaypoint.IsAvailable defaults to false.
            if (seedWaypoints != null)
            {
                foreach (var waypoint in seedWaypoints)
                {
                    if (waypoint == null)
                        continue;
                    memoryNew.SetRoomWaypointAvailability(waypoint, true);
                }
            }
            if (lastVisited != null)
                memoryNew.SetLastVisitedPoint(lastVisited);

            if (role == RobotRole.Worker
                && lastVisited != null
                && (lastVisited.type == WaypointType.Work || lastVisited.type == WaypointType.Rest))
            {
                // Spawned directly in a machine room: start as connected, then Heart task completion
                // will release connection and trigger next cycle target.
                memoryNew.ChangeConnectionToMachine(true);
            }
        }

        // Attach a security badge to guards and the boss so it can be stolen later.
        if ((role == RobotRole.SecurityGuard || role == RobotRole.Boss) && securityBadgeSpawner != null)
        {
            Transform anchor = go.transform;
            var body = go.GetComponent<RobotBodyController>();
            if (body != null && body.BodyReference != null)
                anchor = body.BodyReference;

            var badge = securityBadgeSpawner.SpawnBadge(anchor);
            var inventory = go.GetComponent<Inventory>();
            if (badge != null && inventory != null)
            {
                inventory.SetItem(PickupType.SecurityBadge, badge);
                if (role == RobotRole.Boss)
                    Debug.Log($"[Boss] Security badge attached to '{go.name}' anchor='{anchor.name}'.", go);
            }
            else if (role == RobotRole.Boss)
            {
                Debug.LogWarning(
                    $"[Boss] Failed to attach security badge to '{go.name}' badge={(badge != null ? badge.name : "null")} inventory={(inventory != null ? inventory.name : "null")}.",
                    go);
            }
        }
    }

    private static void EnsureNewPipelineComponents(GameObject go)
    {
        if (go == null)
            return;

        if (go.GetComponent<RobotMemoryNew>() == null)
            go.AddComponent<RobotMemoryNew>();
        if (go.GetComponent<RobotHeartNew>() == null)
            go.AddComponent<RobotHeartNew>();
        if (go.GetComponent<RobotBrainNew>() == null)
            go.AddComponent<RobotBrainNew>();
    }

}

