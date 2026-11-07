using UnityEngine;
using System.Collections;


public class SceneInitiator : GameInitiator
{
    private IFactoryManager factoryManager;
    private GameUIViewModel gameUIViewModel;
    private IPlayerSpawner playerInitiator;
    private IEnemiesSpawner enemiesSpawner;
    private MapManager mapManager;
    private IWaypointService waypointService;
    private IRobotRespawnService respawnService;
    private RunMapConfigSO mapConfig;
    private VictorySetup victorySetup;
    private ISaveService saveService;
    private SceneController sceneController;
    private SecurityBadgeSpawner securityBadgeSpawner;
    private BatterySpawner batterySpawner;
    private SceneSetupMode setupMode;

    public void Construct(
        IFactoryManager factoryManager,
        GameUIViewModel gameUIViewModel,
        IPlayerSpawner playerInitiator,
        IEnemiesSpawner enemiesSpawner,
        MapManager mapManager,
        IWaypointService waypointService,
        IRobotRespawnService respawnService,
        VictorySetup victorySetup,
        ISaveService saveService,
        SecurityBadgeSpawner securityBadgeSpawner,
        BatterySpawner batterySpawner,
        SceneSetupMode setupMode = SceneSetupMode.GeneratedMap)
    {
        this.factoryManager = factoryManager;
        this.gameUIViewModel = gameUIViewModel;
        this.playerInitiator = playerInitiator;
        this.enemiesSpawner = enemiesSpawner;
        this.mapManager = mapManager;
        this.waypointService = waypointService;
        this.respawnService = respawnService;
        this.victorySetup = victorySetup;
        this.saveService = saveService;
        this.securityBadgeSpawner = securityBadgeSpawner;
        this.batterySpawner = batterySpawner;
        this.setupMode = setupMode;

        if (RunProgressManager.Instance != null)
        {
            this.mapConfig = RunProgressManager.Instance.CurrentConfig;
        }
        else if (this.mapConfig == null)
        {
            // Provide a lightweight default so edit-mode tests do not require
            // the global RunProgressManager singleton.
            this.mapConfig = ScriptableObject.CreateInstance<RunMapConfigSO>();
        }

        InitializeSceneSpecificObjects();
    }

    protected override void InitializeSceneSpecificObjects()
    {
        InitializeSharedObjects();
        InitializeVictorySetup();
        if (setupMode == SceneSetupMode.GeneratedMap)
        {
            InitializeFactory();
        }
        else
        {
            InitializeStaticFactory();
        }
        InitializeSceneController();
        if (setupMode == SceneSetupMode.GeneratedMap)
        {
            InitializePlayer();
            InitializeEnemies();
            InitializeMiniMap();
        }
        else
        {
            InitializeStaticPlayer();
        }
    }

    private void InitializeFactory()
    {
        if (factoryManager == null)
        {
            Debug.LogWarning("SceneInitiator: FactoryManager is not assigned; skipping factory initialization.");
            return;
        }

        RobotDomainEventAdapter.EnsureInScene();

        if (mapManager != null)
        {
            if (mapConfig != null)
            {
                mapManager.BuildFromConfig(mapConfig);
            }
            else
            {
                Debug.LogWarning("SceneInitiator: Map config is null; skipping map build.");
            }
        }
        else
        {
            Debug.LogWarning("SceneInitiator: MapManager is not assigned; skipping map build.");
        }

        factoryManager.Initialize(mapManager, waypointService, victorySetup, enemiesSpawner);
        Debug.Log("FactoryManager initialized.");
    }

    private void InitializeStaticFactory()
    {
        if (factoryManager == null)
        {
            Debug.LogWarning("SceneInitiator: FactoryManager is not assigned; skipping static factory initialization.");
            return;
        }

        RobotDomainEventAdapter.EnsureInScene();
        factoryManager.InitializeStatic(victorySetup, waypointService);
        InitializeStaticNavigationGraph();
        Debug.Log("Static FactoryManager initialized.");
    }

    private void InitializePlayer()
    {
        if (playerInitiator == null || factoryManager == null)
        {
            Debug.LogWarning("SceneInitiator: Player dependencies are missing; skipping player initialization.");
            return;
        }

        Vector3 startPos = factoryManager.GetStartCellWorldPosition();

        playerInitiator.SetPlayerStartPosition(startPos);

        playerInitiator.InitializePlayer(saveService);

        factoryManager.SetPlayerInstanceHead(playerInitiator.playerInstance, playerInitiator.playerHeadTransform);

        gameUIViewModel?.SetPlayer(playerInitiator.playerRobotBehaviour);
        SetCinemachineTarget(playerInitiator.playerHeadTransform);

        Debug.Log("Player initialized.");
    }

    private void InitializeStaticPlayer()
    {
        if (playerInitiator == null || factoryManager == null)
        {
            Debug.LogWarning("SceneInitiator: Player dependencies are missing; skipping static player initialization.");
            return;
        }

        Vector3 startPos = ResolveStaticPlayerStartPosition();
        playerInitiator.SetPlayerStartPosition(startPos);
        playerInitiator.InitializePlayer(saveService);
        factoryManager.SetPlayerInstanceHead(playerInitiator.playerInstance, playerInitiator.playerHeadTransform);
        InitializeStaticRooms();
        InitializeStaticReceptionDefenders();
        InitializeStaticRobotNavigation();
        StartCoroutine(SpawnStaticWorkerCollectorWhenReady());

        gameUIViewModel?.SetPlayer(playerInitiator.playerRobotBehaviour);
        SetCinemachineTarget(playerInitiator.playerHeadTransform);
        ApplyStaticStartState();
        InitializeStaticMiniMap();

        Debug.Log("Static player initialized.");
    }

    private void InitializeStaticReceptionDefenders()
    {
        if (enemiesSpawner == null || factoryManager == null)
        {
            Debug.LogWarning("SceneInitiator: Reception defender setup dependencies are missing; skipping static spawn.");
            return;
        }

        enemiesSpawner.SetDropContainer(factoryManager is MonoBehaviour managerBehaviour
            ? managerBehaviour.transform
            : null);
        enemiesSpawner.Initialize(
            mapManager,
            waypointService,
            gameUIViewModel,
            respawnService,
            factoryManager,
            factoryManager.SecurityManager,
            securityBadgeSpawner,
            batterySpawner);
        enemiesSpawner.SpawnSecurityReceptionGuards();

        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Level_3")
            enemiesSpawner.SpawnBossAtEnd();
    }

    private IEnumerator SpawnStaticWorkerCollectorWhenReady()
    {
        const int maximumAttempts = 120;
        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "Level_2" && sceneName != "Level_3")
            yield break;

        for (int attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            GameObject collector = WorkerCollectorMissionService.SpawnStaticSceneCollector();
            if (collector != null)
            {
                Debug.Log(
                    $"[WorkerCollectorDiagnostics] Static collector spawned scene={sceneName} attempt={attempt}.",
                    collector);
                yield break;
            }

            yield return null;
        }

        Debug.LogError(
            $"[WorkerCollectorDiagnostics] Static collector failed to spawn in {sceneName} "
            + $"after {maximumAttempts} attempts. Verify active Garage, Conveyor, and collector prefab providers.",
            this);
    }

    private void InitializeStaticRooms()
    {
        RoomManager[] rooms = FindObjectsByType<RoomManager>(FindObjectsSortMode.None);
        factoryManager.RegisterStaticRooms(rooms, playerInitiator.playerHeadTransform);
    }

    private void InitializeStaticNavigationGraph()
    {
        if (waypointService == null)
        {
            Debug.LogError("[WorkerCollectorDiagnostics] Static graph initialization skipped: waypointService=null.");
            return;
        }

        System.Collections.Generic.List<StaticLevelPath> paths = StaticLevelPath.FindEnabledInScene();
        if (paths.Count == 0 && setupMode == SceneSetupMode.StaticLevel)
        {
            bool generatedInitialized = StaticRobotNavigationInitializer.TryInitializeGeneratedSceneGraph(
                waypointService,
                FindObjectsByType<RoomManager>(FindObjectsSortMode.None));
            Debug.Log($"[WorkerCollectorDiagnostics] Static generated graph initialized={generatedInitialized}, "
                + $"paths=0, waypoints={waypointService.GetAllWaypoints().Count}.");
            return;
        }
        if (paths.Count != 1)
        {
            Debug.LogError($"Static navigation expected one enabled {nameof(StaticLevelPath)}, but found {paths.Count}.");
            return;
        }

        bool initialized = paths[0].TryInitializeNavigation(waypointService);
        Debug.Log($"[WorkerCollectorDiagnostics] Static graph initialized={initialized}, paths={paths.Count}, "
            + $"waypoints={waypointService.GetAllWaypoints().Count}.");
        if (initialized)
            StaticRobotNavigationInitializer.SetSharedService(waypointService);
    }

    private void InitializeStaticRobotNavigation()
    {
        if (waypointService == null)
            return;

        if (waypointService is WaypointService concreteService
            && concreteService.GraphMode == WaypointGraphMode.StaticExplicit)
        {
            System.Collections.Generic.List<StaticLevelPath> paths = StaticLevelPath.FindEnabledInScene();
            if (paths.Count != 1 || !paths[0].IsNavigationReady)
                return;
        }
        else if (waypointService.GetAllWaypoints().Count == 0)
        {
            return;
        }

        StaticRobotNavigationInitializer.InitializeSceneRobots(waypointService);
    }

    private Vector3 ResolveStaticPlayerStartPosition()
    {
        StaticLevelSpawnPoint spawnPoint = FindFirstObjectByType<StaticLevelSpawnPoint>();
        if (spawnPoint != null)
            return spawnPoint.transform.position;

        string fallbackRoomName = setupMode switch
        {
            SceneSetupMode.Laboratory => "ROOM_Laboratory_1",
            SceneSetupMode.Conveyor => "ROOM_Conveyor",
            _ => "ROOM_Deads"
        }; GameObject fallbackRoom = GameObject.Find(fallbackRoomName);
        if (fallbackRoom != null)
            return fallbackRoom.transform.position;

        Debug.LogWarning($"SceneInitiator: no StaticLevelSpawnPoint or {fallbackRoomName} found; using world origin.");
        return Vector3.zero;
    }

    private void ApplyStaticStartState()
    {
        if (setupMode != SceneSetupMode.StaticLevel || playerInitiator?.playerInstance == null)
            return;

        string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (sceneName != "Level_1")
            return;

        var energyBot = playerInitiator.playerInstance.GetComponent<EnergyBot>();
        var movement = playerInitiator.playerInstance.GetComponent<PlayerMovementController>();
        RobotStateController stateController = playerInitiator.playerRobotBehaviour;
        if (energyBot == null || stateController == null || movement == null)
        {
            Debug.LogWarning("SceneInitiator: Level_1 faint start skipped because player energy/state/input components are missing.");
            return;
        }

        energyBot.SetAutoRecharge(false);
        energyBot.SetCurrentEnergy(0f);
        stateController.UpdateState(RobotState.Faint);

        var gate = playerInitiator.playerInstance.AddComponent<FirstMovementRechargeGate>();
        gate.Configure(energyBot, movement.Input);
    }

    private void InitializeEnemies()
    {
        if (enemiesSpawner == null || factoryManager == null)
        {
            Debug.LogWarning("SceneInitiator: Enemy setup dependencies are missing; skipping enemy initialization.");
            return;
        }

        Transform dropContainer = mapManager != null ? mapManager.transform : null;
        enemiesSpawner.SetDropContainer(dropContainer);
        enemiesSpawner.Initialize(
            mapManager,
            waypointService,
            gameUIViewModel,
            respawnService,
            factoryManager,
            factoryManager.SecurityManager,
            securityBadgeSpawner,
            batterySpawner);
        if (mapConfig != null)
        {
            // The sandbox uses the menu values as-is so behaviours can be stress-tested;
            // the run places one guard per Security room.
            bool isSandbox = RunProgressManager.Instance != null && RunProgressManager.Instance.IsSandboxRun;
            int securityGuardsCount = isSandbox
                ? Mathf.Max(0, mapConfig.securityGuardsCount)
                : mapManager != null
                    ? mapManager.GetPOIRoomCount(POIType.Security)
                    : 0;
            WorkerPopulation workerPopulation = ResolveWorkerPopulation();
            Debug.Log($"[WorkerPopulation] Level="
                + $"{(RunProgressManager.Instance != null ? RunProgressManager.Instance.CurrentLevelIndex : 0)} "
                + $"total={workerPopulation.Workers + workerPopulation.WorkerCollectors} "
                + $"normal={workerPopulation.Workers} collectors={workerPopulation.WorkerCollectors}.", this);
            enemiesSpawner.CreateWorkers(workerPopulation.Workers);
            int spawningRoomCount = mapManager != null ? mapManager.GetPOIRoomCount(POIType.Spawning) : 0;
            enemiesSpawner.CreateWorkerSpawners(spawningRoomCount);
            enemiesSpawner.CreateSecurityGuards(securityGuardsCount);
            enemiesSpawner.CreateBoss();
        }
        enemiesSpawner.SpreadEnemies();
        WorkerPopulation dedicatedPopulation = ResolveWorkerPopulation();
        enemiesSpawner.SpawnDedicatedRoomRobots(
            dedicatedPopulation.WorkerCollectors,
            dedicatedPopulation.WorkerCollectorsRestAfterBatch);
        if (RobotNewPipelineRuntime.EnableProbeSummaryOnSceneInit)
            StartCoroutine(DumpProbeSummaryEndOfFrame());
        if (RobotNewPipelineRuntime.IsWorkerCycleValidationEnabled && RobotNewPipelineRuntime.EnableEcosystemProbe)
            StartCoroutine(DumpWorkerProbeSummaryIntervals());
    }

    private WorkerPopulation ResolveWorkerPopulation()
    {
        RunProgressManager runProgress = RunProgressManager.Instance;
        if (runProgress != null && runProgress.CurrentLevelIndex >= 6)
        {
            RoomManager[] rooms = FindObjectsByType<RoomManager>(FindObjectsSortMode.None);
            return GeneratedWorkerPopulation.Calculate(rooms);
        }

        int totalWorkers = mapConfig != null ? Mathf.Max(0, mapConfig.workersCount) : 0;
        bool supportsCollectors = mapManager != null
            && mapManager.GetPOIRoomCount(POIType.Resting) > 0
            && mapManager.GetPOIRoomCount(POIType.Garage) > 0
            && mapManager.GetPOIRoomCount(POIType.Conveyor) > 0;
        bool isCustomSetup = runProgress != null && runProgress.CurrentLevelIndex == 0;
        int collectors = 0;
        if (supportsCollectors)
        {
            collectors = isCustomSetup
                ? totalWorkers / 2
                : mapConfig != null
                    ? Mathf.Clamp(mapConfig.workerCollectorsCount, 0, totalWorkers)
                    : 0;
        }
        return new WorkerPopulation(
            totalWorkers - collectors,
            collectors,
            mapConfig == null || mapConfig.workerCollectorsRestAfterBatch);
    }

    private IEnumerator DumpProbeSummaryEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        RobotEcosystemProbe.DumpSummary("SceneInitiator.InitializeEnemies");
    }

    private IEnumerator DumpWorkerProbeSummaryIntervals()
    {
        yield return new WaitForSeconds(10f);
        RobotEcosystemProbe.DumpWorkerSummary("SceneInitiator.WorkerCycle.t+10s");

        yield return new WaitForSeconds(20f);
        RobotEcosystemProbe.DumpWorkerSummary("SceneInitiator.WorkerCycle.t+30s");
    }

    private void InitializeSceneController()
    {
        if (SceneController.instance != null && factoryManager != null)
        {
            sceneController = SceneController.instance;
            sceneController.Initialize(factoryManager);
        }
    }

    private void InitializeVictorySetup()
    {
        if (victorySetup != null)
        {
            RunProgressManager runProgress = RunProgressManager.Instance;
            if (runProgress != null && runProgress.CurrentLevelIndex > 0)
            {
                float morality = runProgress.RunStats != null ? runProgress.RunStats.Morality : 0f;
                victorySetup.ConfigureForLevel(runProgress.CurrentLevelIndex, morality);
            }
            else
            {
                // Directly opening a gameplay scene in the Editor has no active run.
                // Treat it as an early level instead of leaking population counts into victory goals.
                victorySetup.ConfigureForLevel(1, 0f);
            }
            victorySetup.currentSaved = 0;
            victorySetup.currentKilled = 0;
        }
        else
        {
            Debug.LogWarning("VictorySetup is not assigned.");
        }
    }

    private void InitializeMiniMap()
    {
        if (gameUIViewModel != null && mapManager != null)
        {
            gameUIViewModel.SetMiniMapTexture(mapManager);
        }
    }

    private void InitializeStaticMiniMap()
    {
        gameUIViewModel?.SetMiniMapTextureFromScene();
    }
}
