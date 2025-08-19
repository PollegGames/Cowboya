using UnityEngine;


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
    private HintManager hintManager;

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
        HintManager hintManager)
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
        this.hintManager = hintManager;

        if (RunProgressManager.Instance != null)
        {
            this.mapConfig = RunProgressManager.Instance.CurrentConfig;
        }
        else
        {
            Debug.LogError("RunProgressManager instance not found.");
        }

        InitializeSceneSpecificObjects();
    }

    protected override void InitializeSceneSpecificObjects()
    {
        InitializeSharedObjects();
        InitializeFactory();
        InitializeSceneController();
        InitializePlayer();
        InitializeEnemies();
        InitializeVictorySetup();
        InitializeMiniMap();
    }

    private void InitializeFactory()
    {
        if (mapConfig != null)
        {
            mapManager.BuildFromConfig(mapConfig);
        }
        else
        {
            Debug.LogError("SceneInitiator: Map config is null.");
        }
        factoryManager.Initialize(mapManager, waypointService, victorySetup, enemiesSpawner);
        Debug.Log("FactoryManager initialized.");
    }

    private void InitializePlayer()
    {
        Vector3 startPos = factoryManager.GetStartCellWorldPosition();

        playerInitiator.SetPlayerStartPosition(startPos);

        playerInitiator.InitializePlayer(saveService);

        factoryManager.SetPlayerInstanceHead(playerInitiator.playerInstance, playerInitiator.playerHeadTransform);

        gameUIViewModel?.SetPlayer(playerInitiator.playerRobotBehaviour);
        SetCinemachineTarget(playerInitiator.playerHeadTransform);

        InitializeHintManager();

        Debug.Log("Player initialized.");
    }

    private void InitializeHintManager()
    {
         // --- Setup HintManager here ---
        if (hintManager != null && playerInitiator.playerInstance != null)
        {
            var playerState = playerInitiator.playerInstance.GetComponent<PlayerMovementController>();
            var inputSource = playerState.Input;
            var health = playerInitiator.playerInstance.GetComponent<HealthBot>();
            var inventory = playerInitiator.playerInstance.GetComponent<Inventory>();
            hintManager.Setup(inputSource, health, inventory);
        }
    }

    private void InitializeEnemies()
    {
        enemiesSpawner?.SetDropContainer(mapManager.transform);
        enemiesSpawner?.Initialize(
            mapManager,
             waypointService,
             gameUIViewModel,
             respawnService,
             factoryManager.SecurityManager,
             securityBadgeSpawner,
             batterySpawner);
        if (mapConfig != null)
        {
            enemiesSpawner?.CreateWorkers(mapConfig.workersCount);
            enemiesSpawner?.CreateWorkerSpawners(mapConfig.blockedCount);
            enemiesSpawner?.CreateSecurityGuards(mapConfig.enemiesCount);
            enemiesSpawner?.CreateBoss();
        }
        enemiesSpawner?.SpreadEnemies();
    }

    private void InitializeSceneController()
    {
        if (SceneController.instance != null)
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
                victorySetup.robotsSavedTarget = mapConfig.workersCount;
                victorySetup.robotsKilledTarget = mapConfig.enemiesCount;
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
}