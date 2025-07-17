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
    private HUDMiniMap hudMiniMap;
    private VictorySetup victorySetup;
    private ISaveService saveService;
    private HUDMiniMap hudMiniMap;

    private SceneController sceneController;

    public void Construct(
        IFactoryManager factoryManager,
        GameUIViewModel gameUIViewModel,
        IPlayerSpawner playerInitiator,
        IEnemiesSpawner enemiesSpawner,
        MapManager mapManager,
        IWaypointService waypointService,
        IRobotRespawnService respawnService,
        VictorySetup victorySetup,
        ISaveService saveService)
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
        this.hudMiniMap = gameUIViewModel.GetComponent<HUDMiniMap>();
        if (this.hudMiniMap == null)
            this.hudMiniMap = gameUIViewModel.gameObject.AddComponent<HUDMiniMap>();

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
        if (hudMiniMap != null)
        {
            hudMiniMap.Setup(mapManager, waypointService, respawnService);
        }
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

        Debug.Log("Player initialized.");
    }

    private void InitializeEnemies()
    {
        enemiesSpawner?.SetDropContainer(factoryManager.transform);
        enemiesSpawner?.Initialize(mapManager, waypointService, gameUIViewModel, respawnService, factoryManager.SecurityManager );
        if (mapConfig != null)
        {
            enemiesSpawner?.CreateWorkers(mapConfig.workersCount);
            enemiesSpawner?.CreateWorkersSpawner(mapConfig.blockedCount);
            enemiesSpawner?.CreateEnemies(mapConfig.enemiesCount);
            enemiesSpawner?.CreateBoss();
        }
        enemiesSpawner?.SpreadEnemies();
    }

    private void InitializeSceneController()
    {
        if ( SceneController.instance != null)
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
        if (hudMiniMap != null)
        {
            hudMiniMap.Initialize(mapManager, factoryManager, waypointService, gameUIViewModel.miniMapPreview);
            gameUIViewModel.SetMiniMapTexture(hudMiniMap.MiniMapTexture);
        }
    }
}