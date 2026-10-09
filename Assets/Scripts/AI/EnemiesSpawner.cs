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
    [SerializeField] private GameObject securityReceptionPrefab;
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
    private readonly List<GameObject> spawnedSecurityReceptionGuards = new();
    private readonly Dictionary<RoomWaypoint, GameObject> receptionGuardByPost = new();
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

        if (bossInstance == null)
        {
            Debug.LogError("[EnemiesSpawner] No boss prefab assigned.", this);
            return;
        }

        var locomotion = bossInstance.GetComponent<RobotLocomotionController>();
        if (locomotion != null) locomotion.isPlayerControlled = false;

        var state = bossInstance.GetComponent<RobotStateController>();
        if (state != null)
        {
            state.Stats = factory.CreateRobot();
            state.Stats.RobotName = "BOSS 1";
        }

        RoomWaypoint end = FindEndRoomWaypoint();
        bossInstance.transform.position = (end != null) ? end.WorldPos : Vector3.zero;
        bossInstance.SetActive(false);
    }

    /// <summary>
    /// Creates the boss at the authored end-room waypoint and activates it in place.
    /// </summary>
    public void SpawnBossAtEnd()
    {
        CreateBoss();
        if (bossInstance == null)
            return;

        RoomWaypoint end = FindEndRoomWaypoint();
        if (end == null)
        {
            Debug.LogError("[EnemiesSpawner] Cannot spawn boss: no end-room center waypoint was found.", this);
            return;
        }

        PositionForSpawn(bossInstance, end.WorldPos);
        InitializeRobot(bossInstance, RobotRole.Boss, end);
        Wake(bossInstance);
        Debug.Log($"[EnemiesSpawner] Spawned boss at '{end.name}' in room '{end.parentRoom.name}'.", bossInstance);
    }

    private RoomWaypoint FindEndRoomWaypoint()
    {
        RoomWaypoint end = waypointService != null ? waypointService.GetEndPoint() : null;
        if (end != null)
            return end;

        return FindObjectsByType<RoomManager>(FindObjectsSortMode.None)
            .Where(room => room != null
                && room.roomProperties != null
                && room.roomProperties.usageType == UsageType.End)
            .SelectMany(room => room.GetWaypoints())
            .FirstOrDefault(waypoint => waypoint != null && waypoint.type == WaypointType.Center);
    }

    /// <summary>
    /// Spawns one stationary defender at the authored Work waypoint in every reception room.
    /// </summary>
    public void SpawnSecurityReceptionGuards()
    {
        spawnedSecurityReceptionGuards.RemoveAll(guard => guard == null);

        if (securityReceptionPrefab == null)
        {
            Debug.LogError("[EnemiesSpawner] No security reception prefab assigned.", this);
            return;
        }

        IEnumerable<RoomWaypoint> availableWaypoints = waypointService != null
            ? waypointService.GetAllWaypoints()
            : FindObjectsByType<RoomManager>(FindObjectsSortMode.None)
                .Where(room => room != null)
                .SelectMany(room => room.GetWaypoints());
        List<RoomWaypoint> receptionPosts = FindReceptionWorkWaypoints(availableWaypoints);
        if (receptionPosts.Count == 0)
        {
            Debug.LogWarning(
                "[EnemiesSpawner] No Reception room with exactly one Work waypoint was found.",
                this);
            return;
        }
        foreach (RoomWaypoint post in receptionPosts)
        {
            if (receptionGuardByPost.TryGetValue(post, out GameObject existing) && existing != null)
                continue;

            GameObject guard = PoolGet(securityReceptionPrefab);
            if (guard == null)
                continue;

            PositionForSpawn(guard, post.WorldPos);
            InitializeRobot(guard, RobotRole.SecurityReception, post);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Level_3")
            {
                SecurityReceptionFaintLock faintLock = guard.GetComponent<SecurityReceptionFaintLock>();
                if (faintLock == null)
                    faintLock = guard.AddComponent<SecurityReceptionFaintLock>();
                faintLock.Configure();
            }
            Wake(guard);
            spawnedSecurityReceptionGuards.Add(guard);
            receptionGuardByPost[post] = guard;
            Debug.Log(
                $"[EnemiesSpawner] Spawned SecurityReception at '{post.name}' in room '{post.parentRoom.name}'.",
                guard);
        }
    }

    /// <summary>Populates robots whose existence is derived from generated room identities.</summary>
    public void SpawnDedicatedRoomRobots()
    {
        SpawnSecurityReceptionGuards();

        RoomManager[] rooms = FindObjectsByType<RoomManager>(FindObjectsSortMode.None);
        RoomManager primaryGarage = rooms.FirstOrDefault(room => room != null
            && room.roomProperties != null
            && room.roomProperties.usageType == UsageType.POI
            && room.roomProperties.poiType == POIType.Garage
            && room.roomProperties.POISlot == 6);
        bool hasPairedConveyor = rooms.Any(room => room != null
            && room.roomProperties != null
            && room.roomProperties.usageType == UsageType.POI
            && room.roomProperties.poiType == POIType.Conveyor
            && room.roomProperties.POISlot == 7);

        if (primaryGarage == null || !hasPairedConveyor)
            return;

        WorkerCollectorGarageSpawnProvider provider =
            primaryGarage.GetComponentInChildren<WorkerCollectorGarageSpawnProvider>(true);
        if (provider == null)
        {
            Debug.LogError($"DedicatedRoomRobotSkipped room={primaryGarage.name} reason=MissingGarageSpawnProvider",
                primaryGarage);
            return;
        }

        provider.TrySpawnCollector();
        Debug.Log($"DedicatedRoomPopulation receptions={receptionGuardByPost.Count} garages=1", this);
    }

    /// <summary>
    /// Returns the unique Work waypoint authored in each reception room.
    /// </summary>
    public static List<RoomWaypoint> FindReceptionWorkWaypoints(IEnumerable<RoomWaypoint> waypoints)
    {
        if (waypoints == null)
            return new List<RoomWaypoint>();

        var receptionGroups = waypoints
            .Where(waypoint => waypoint != null
                && waypoint.parentRoom != null
                && waypoint.parentRoom.roomProperties != null
                && waypoint.parentRoom.roomProperties.usageType == UsageType.POI
                && waypoint.parentRoom.roomProperties.poiType == POIType.Reception)
            .GroupBy(waypoint => waypoint.parentRoom);

        var posts = new List<RoomWaypoint>();
        foreach (var group in receptionGroups)
        {
            List<RoomWaypoint> workPoints = group
                .Where(waypoint => waypoint.type == WaypointType.Work)
                .ToList();
            if (workPoints.Count != 1)
            {
                Debug.LogError(
                    $"[EnemiesSpawner] Reception room '{group.Key.name}' requires exactly one Work waypoint, found {workPoints.Count}.",
                    group.Key);
                continue;
            }

            posts.Add(workPoints[0]);
        }

        return posts;
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

        if (role == RobotRole.Worker
            && go.GetComponent<WorkerWorkAnimationController>() == null)
        {
            go.AddComponent<WorkerWorkAnimationController>();
        }

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

        // Attach a security badge to security robots and the boss so it can be stolen later.
        if ((role == RobotRole.SecurityGuard
                || role == RobotRole.SecurityReception
                || role == RobotRole.Boss)
            && securityBadgeSpawner != null)
        {
            Transform anchor = go.transform;
            var body = go.GetComponent<RobotBodyController>();
            if (body != null && body.BodyReference != null)
                anchor = body.BodyReference;

            Vector3 badgeOffset = role == RobotRole.SecurityReception
                ? new Vector3(0.55f, 0.3f, 0f)
                : Vector3.zero;
            var badge = securityBadgeSpawner.SpawnBadge(anchor, badgeOffset);
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

/// <summary>
/// Plays the normal worker's arm-only animation while it is attached to a work machine.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerWorkAnimationController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private RobotHeartNew heart;
    [SerializeField] private RobotMemoryNew memory;
    [SerializeField] private RobotStateController stateController;
    [SerializeField, Min(0f)] private float transitionDuration = 0.15f;

    private static readonly int WorkStateHash = Animator.StringToHash("Base Layer.WorkerArmsWork");
    private static readonly int IdleStateHash = Animator.StringToHash("Base Layer.Idle_Master");

    private bool isPlayingWorkAnimation;
    private bool missingStateReported;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        isPlayingWorkAnimation = false;
    }

    private void Update()
    {
        bool shouldPlay = ShouldPlayWorkAnimation();
        if (shouldPlay == isPlayingWorkAnimation || animator == null)
            return;

        int targetState = shouldPlay ? WorkStateHash : IdleStateHash;
        if (!animator.HasState(0, targetState))
        {
            if (!missingStateReported)
            {
                Debug.LogWarning(
                    $"[WorkerWorkAnimation] Animator on '{name}' is missing the requested work or idle state.",
                    this);
                missingStateReported = true;
            }
            return;
        }

        animator.CrossFade(targetState, transitionDuration, 0);
        isPlayingWorkAnimation = shouldPlay;
    }

    private bool ShouldPlayWorkAnimation()
    {
        if (heart == null || heart.Role != RobotRole.Worker)
            return false;
        if (memory == null || !memory.IsConnectedToMachine)
            return false;
        if (stateController == null || stateController.CurrentState != RobotState.Alive)
            return false;

        RobotTask currentTask = heart.CurrentTask;
        return currentTask != null && currentTask.Type == RobotTaskType.WorkAtMachine;
    }

    private void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
        if (heart == null)
            heart = GetComponent<RobotHeartNew>();
        if (memory == null)
            memory = GetComponent<RobotMemoryNew>();
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();
    }
}

/// <summary>
/// Keeps the Level 3 reception defender faint without blocking damage or death.
/// </summary>
[DisallowMultipleComponent]
public class SecurityReceptionFaintLock : MonoBehaviour
{
    private RobotStateController stateController;
    private EnergyBot energyBot;
    private RobotBodyController bodyController;
    private RobotAttackController attackController;
    private RobotBrainNew brain;
    private FollowPlayerTriggerHandler[] perceptionHandlers;
    private bool configured;
    private bool triggered;
    private bool dead;

    public bool IsLocked => configured && triggered && !dead;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        if (configured && triggered && !dead)
            ApplyFaintLock();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>
    /// Arms this robot to faint permanently when the player is first detected.
    /// </summary>
    public void Configure()
    {
        configured = true;
        triggered = false;
        dead = false;
        ResolveReferences();
        Subscribe();
        energyBot?.SetStats(stateController != null ? stateController.Stats : null);
        energyBot?.SetAutoRecharge(false);
        bodyController?.StopMovement();
        attackController?.StopAttacking();
    }

    /// <summary>
    /// Depletes the robot's energy and locks it in the faint state until death.
    /// </summary>
    public void TriggerFaint()
    {
        if (!configured || triggered || dead)
            return;

        triggered = true;
        ApplyFaintLock();
    }

    private void HandleStateChanged(RobotState state)
    {
        if (state == RobotState.Dead)
        {
            dead = true;
            attackController?.StopAttacking();
            return;
        }

        if (configured && triggered && !dead && state != RobotState.Faint)
            ApplyFaintLock();
    }

    private void ApplyFaintLock()
    {
        if (stateController == null || stateController.CurrentState == RobotState.Dead)
        {
            dead = stateController != null && stateController.CurrentState == RobotState.Dead;
            return;
        }

        if (energyBot != null)
        {
            energyBot.SetStats(stateController.Stats);
            energyBot.SetAutoRecharge(false);
            energyBot.SetCurrentEnergy(0f);
        }
        else if (stateController.Stats != null)
        {
            stateController.Stats.CurrentEnergy = 0f;
        }
        bodyController?.StopMovement();
        attackController?.StopAttacking();

        if (brain != null)
            brain.enabled = false;

        if (perceptionHandlers != null)
        {
            foreach (FollowPlayerTriggerHandler handler in perceptionHandlers)
            {
                if (handler != null)
                    handler.enabled = false;
            }
        }

        if (stateController.CurrentState != RobotState.Faint)
            stateController.UpdateState(RobotState.Faint);

        stateController.ApplyEnemyFaintPose();
    }

    private void ResolveReferences()
    {
        stateController ??= GetComponent<RobotStateController>();
        energyBot ??= GetComponent<EnergyBot>();
        bodyController ??= GetComponent<RobotBodyController>();
        attackController ??= GetComponent<RobotAttackController>();
        brain ??= GetComponent<RobotBrainNew>();
        perceptionHandlers ??= GetComponentsInChildren<FollowPlayerTriggerHandler>(true);
    }

    private void Subscribe()
    {
        if (stateController == null)
            return;

        stateController.OnStateChanged -= HandleStateChanged;
        stateController.OnStateChanged += HandleStateChanged;
    }

    private void Unsubscribe()
    {
        if (stateController != null)
            stateController.OnStateChanged -= HandleStateChanged;
    }
}

