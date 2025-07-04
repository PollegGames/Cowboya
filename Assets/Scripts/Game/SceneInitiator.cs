using TMPro;
using UnityEngine;

public class SceneInitiator : GameInitiator
{
    [SerializeField] private FactoryManager factoryManager;
    [SerializeField] private GameObject sceneControllerPrefab;
    [SerializeField] private GameUIViewModel gameUIViewModel;
    [SerializeField] private PlayerInitiator playerInitiator;
    [SerializeField] private EnemiesSpawner enemiesSpawner;
    [SerializeField] private MapManager mapManager;
    [SerializeField] private WaypointService waypointService;
    [SerializeField] private RunMapConfigSO mapConfig;
    [SerializeField] private VictorySetup victorySetup;

     private SceneController sceneController;

    private void Start()
    {
        BindOfObjects();
        InitializeSceneSpecificObjects();
    }
    private void BindOfObjects()
    {
        factoryManager = Instantiate(factoryManager);
        sceneControllerPrefab = Instantiate(sceneControllerPrefab);
        gameUIViewModel = Instantiate(gameUIViewModel);
        playerInitiator = Instantiate(playerInitiator);
        enemiesSpawner = Instantiate(enemiesSpawner);
        mapManager = Instantiate(mapManager);
        waypointService = Instantiate(waypointService);
    }

    protected override void InitializeSceneSpecificObjects()
    {
        InitializeFactory();
        InitializeSceneController();
        InitializePlayer();
        InitializeEnemies();
        InitializeVictorySetup();
    }

    private void InitializeFactory()
    {
        mapManager.BuildFromConfig(mapConfig);
        factoryManager.Initialize(mapManager, waypointService, victorySetup);
        Debug.Log("FactoryManager initialized.");
    }

    private void InitializePlayer()
    {
        Vector3 startPos = factoryManager.GetStartCellWorldPosition();

        playerInitiator.SetPlayerStartPosition(startPos);

        playerInitiator.InitializePlayer();

        factoryManager.SetPlayerInstanceHead(playerInitiator.playerInstance, playerInitiator.playerHeadTransform);

        gameUIViewModel?.SetPlayer(playerInitiator.playerRobotBehaviour);
        SetCinemachineTarget(playerInitiator.playerHeadTransform);

        Debug.Log("Player initialized.");
    }

    private void InitializeEnemies()
    {
        enemiesSpawner?.InitMapManager(mapManager, waypointService, gameUIViewModel);
        enemiesSpawner?.CreateEnemies(mapConfig.enemyCount);
        enemiesSpawner?.CreateBoss(mapConfig.bossCount);
        enemiesSpawner?.SpreadEnemies();
    }

    private void InitializeSceneController()
    {
        if ( sceneControllerPrefab != null)
        {
            sceneController = sceneControllerPrefab.GetComponent<SceneController>();
            sceneController.Initialize(factoryManager,gameUIViewModel);
        }
    }

    private void InitializeVictorySetup()
    {
        if (victorySetup != null)
        {
            victorySetup.robotsSavedTarget = mapConfig.enemyCount;
            victorySetup.robotsKilledTarget = mapConfig.bossCount;
        }
        else
        {
            Debug.LogWarning("VictorySetup is not assigned.");
        }
    }
}