using UnityEngine;


public class SceneInitiator : GameInitiator
{
    private IFactoryManager factoryManager;
    private GameObject sceneControllerPrefab;
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

    public void Construct(
        IFactoryManager factoryManager,
        GameObject sceneControllerPrefab,
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
        this.sceneControllerPrefab = sceneControllerPrefab;
        this.gameUIViewModel = gameUIViewModel;
        this.playerInitiator = playerInitiator;
        this.enemiesSpawner = enemiesSpawner;
        this.mapManager = mapManager;
        this.waypointService = waypointService;
        this.respawnService = respawnService;
        this.victorySetup = victorySetup;
        this.saveService = saveService;

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
        enemiesSpawner?.Initialize(mapManager, waypointService, gameUIViewModel, respawnService, factoryManager.SecurityManager );
        if (mapConfig != null)
        {
            enemiesSpawner?.CreateWorkers(mapConfig.workersCount);
            enemiesSpawner?.CreateWorkersSpawner(mapConfig.blockedCount);
            enemiesSpawner?.CreateEnemies(mapConfig.enemiesCount);
        }
        enemiesSpawner?.SpreadEnemies();
    }

    private void InitializeSceneController()
    {
        if ( sceneControllerPrefab != null)
        {
            sceneController = sceneControllerPrefab.GetComponent<SceneController>();
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
}