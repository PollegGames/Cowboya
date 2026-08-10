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
        factoryManager.InitializeStatic(victorySetup);
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

        gameUIViewModel?.SetPlayer(playerInitiator.playerRobotBehaviour);
        SetCinemachineTarget(playerInitiator.playerHeadTransform);
        ApplyStaticStartState();
        InitializeStaticMiniMap();

        Debug.Log("Static player initialized.");
    }

    private void InitializeStaticRooms()
    {
        RoomManager[] rooms = FindObjectsByType<RoomManager>(FindObjectsSortMode.None);
        factoryManager.RegisterStaticRooms(rooms, playerInitiator.playerHeadTransform);
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
            int securityGuardsCount = mapConfig.securityGuardsCount;
            enemiesSpawner.CreateWorkers(mapConfig.workersCount);
            enemiesSpawner.CreateWorkerSpawners(mapConfig.blockedCount);
            enemiesSpawner.CreateSecurityGuards(securityGuardsCount);
            enemiesSpawner.CreateBoss();
        }
        enemiesSpawner.SpreadEnemies();
        if (RobotNewPipelineRuntime.EnableProbeSummaryOnSceneInit)
            StartCoroutine(DumpProbeSummaryEndOfFrame());
        if (RobotNewPipelineRuntime.IsWorkerCycleValidationEnabled && RobotNewPipelineRuntime.EnableEcosystemProbe)
            StartCoroutine(DumpWorkerProbeSummaryIntervals());
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
            if (mapConfig != null)
            {
                int securityGuardsCount = mapConfig.securityGuardsCount;
                victorySetup.robotsSavedTarget = mapConfig.workersCount;
                victorySetup.robotsKilledTarget = securityGuardsCount;
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
