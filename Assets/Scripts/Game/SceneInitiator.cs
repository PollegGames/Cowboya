using TMPro;
using UnityEngine;

public class SceneInitiator : GameInitiator
{
    [SerializeField] private FactoryManager factoryManager;
    [SerializeField] private GameObject sceneControllerPrefab;
    [SerializeField] private GameUIViewModel gameUIViewModel;
    [SerializeField] private PlayerSpawner playerInitiator;
    [SerializeField] private EnemiesSpawner enemiesSpawner;
    [SerializeField] private MapManager mapManager;
    [SerializeField] private WaypointService waypointService;
    [SerializeField] private RobotRespawnService respawnService;
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
        if (factoryManager == null) factoryManager = FindObjectOfType<FactoryManager>();
        if (sceneControllerPrefab == null)
        {
            var sc = FindObjectOfType<SceneController>();
            sceneControllerPrefab = sc != null ? sc.gameObject : null;
        }
        if (gameUIViewModel == null) gameUIViewModel = FindObjectOfType<GameUIViewModel>();
        if (playerInitiator == null) playerInitiator = FindObjectOfType<PlayerSpawner>();
        if (enemiesSpawner == null) enemiesSpawner = FindObjectOfType<EnemiesSpawner>();
        if (mapManager == null) mapManager = FindObjectOfType<MapManager>();
        if (waypointService == null) waypointService = FindObjectOfType<WaypointService>();
        if (respawnService == null) respawnService = FindObjectOfType<RobotRespawnService>();
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
        enemiesSpawner?.Initialize(mapManager, waypointService, gameUIViewModel, respawnService);
        enemiesSpawner?.CreateWorkers(mapConfig.enemyCount);
        enemiesSpawner?.CreateEnemy(mapConfig.bossCount);
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