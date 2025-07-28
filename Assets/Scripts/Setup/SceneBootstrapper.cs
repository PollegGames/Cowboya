using UnityEngine;

// Navigation services
public class SceneBootstrapper : MonoBehaviour
{
    [SerializeField] private SceneInitiator sceneInitiator;
    [SerializeField] private SceneBootstrapConfigSO config;

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

        var factory = Instantiate(config.factoryManagerPrefab);
        var playerSpawner = Instantiate(config.playerSpawnerPrefab);
        var enemiesSpawner = Instantiate(config.enemiesSpawnerPrefab);
        var mapManager = Instantiate(config.mapManagerPrefab);
        var gridBuilder = mapManager.gameObject.AddComponent<GridBuilder>();
        var roomRenderer = mapManager.gameObject.AddComponent<RoomRenderer>();
        var roomProcessor = mapManager.gameObject.AddComponent<RoomProcessor>();
        mapManager.Construct(gridBuilder, roomRenderer, roomProcessor);
        var waypointService = Instantiate(config.waypointServicePrefab);
        var respawnService = Instantiate(config.respawnServicePrefab);
        var badgeSpawner = Instantiate(config.badgeSpawnerPrefab);
        if (SceneController.instance == null)
        {
            Instantiate(config.sceneControllerPrefab);
        }
        var viewModel = Instantiate(config.gameUIViewModelPrefab);
        var saveService = Instantiate(config.saveServicePrefab);

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
                badgeSpawner
            );
        }

        if (RunProgressManager.Instance != null && RunProgressManager.Instance.CurrentLevelIndex == 0)
        {
            new GameObject("TutorialManager").AddComponent<TutorialManager>();
        }
    }
}
