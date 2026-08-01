using UnityEngine;
using UnityEngine.SceneManagement;

// Navigation services
public class SceneBootstrapper : MonoBehaviour
{
    [SerializeField] private SceneInitiator sceneInitiator;
    [SerializeField] private SceneBootstrapConfigSO config;
    private const string AudioManagerResourcePath = "Prefabs/Setup/AudioManager";

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogError("SceneBootstrapper missing config");
            return;
        }

        if (RunProgressManager.Instance == null)
        {
            Instantiate(config.runProgressManagerPrefab);
        }

        RunProgressManager.Instance?.EnsureRunContextForActiveScene(SceneManager.GetActiveScene().name);
        SceneSetupMode setupMode = RunProgressManager.Instance != null
            ? RunProgressManager.Instance.GetSetupModeForActiveScene(config.setupMode)
            : config.setupMode;

        if (AudioManager.Instance == null)
        {
            var audioPrefab = Resources.Load<GameObject>(AudioManagerResourcePath);
            if (audioPrefab != null)
            {
                Instantiate(audioPrefab);
            }
            else
            {
                Debug.LogWarning($"AudioManager prefab not found at Resources/{AudioManagerResourcePath}");
            }
        }

        var factory = Instantiate(config.factoryManagerPrefab);
        var playerSpawner = Instantiate(config.playerSpawnerPrefab);
        EnemiesSpawner enemiesSpawner = null;
        MapManager mapManager = null;
        WaypointService waypointService = null;
        RobotRespawnService respawnService = null;
        SecurityBadgeSpawner badgeSpawner = null;
        BatterySpawner batterySpawner = null;

        if (setupMode == SceneSetupMode.GeneratedMap)
        {
            enemiesSpawner = Instantiate(config.enemiesSpawnerPrefab);
            mapManager = Instantiate(config.mapManagerPrefab);
            var gridBuilder = mapManager.gameObject.AddComponent<GridBuilder>();
            var roomRenderer = mapManager.gameObject.AddComponent<RoomRenderer>();
            var roomProcessor = mapManager.gameObject.AddComponent<RoomProcessor>();
            mapManager.Construct(gridBuilder, roomRenderer, roomProcessor);
            waypointService = Instantiate(config.waypointServicePrefab);
            respawnService = Instantiate(config.respawnServicePrefab);
            badgeSpawner = Instantiate(config.badgeSpawnerPrefab);
            batterySpawner = Instantiate(config.batterySpawnerPrefab);
        }

        if (SceneController.instance == null)
        {
            Instantiate(config.sceneControllerPrefab);
        }
        var viewModel = Instantiate(config.gameUIViewModelPrefab);
        var saveService = FindFirstObjectByType<PlayerSaveService>();
        if (saveService == null)
        {
            saveService = Instantiate(config.saveServicePrefab);
        }

        var initiator = sceneInitiator;
        if (initiator != null)
        {
            initiator.Construct(
                factory,
                viewModel,
                playerSpawner,
                enemiesSpawner,
                mapManager,
                waypointService,
                respawnService,
                config.victorySetupPrefab,
                saveService,
                badgeSpawner,
                batterySpawner,
                setupMode
            );
        }

      
    }
}
