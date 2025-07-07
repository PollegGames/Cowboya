using UnityEngine;

public class SceneBootstrapper : MonoBehaviour
{
    [SerializeField] private SceneBootstrapConfigSO config;

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogError("SceneBootstrapper missing config");
            return;
        }

        var factory = Instantiate(config.factoryManagerPrefab);
        var playerSpawner = Instantiate(config.playerSpawnerPrefab);
        var enemiesSpawner = Instantiate(config.enemiesSpawnerPrefab);
        var mapManager = Instantiate(config.mapManagerPrefab);
        var waypointService = Instantiate(config.waypointServicePrefab);
        var respawnService = Instantiate(config.respawnServicePrefab);
        var sceneController = Instantiate(config.sceneControllerPrefab);
        var viewModel = Instantiate(config.gameUIViewModelPrefab);
        var victory = Instantiate(config.victorySetupPrefab);
        var saveService = Instantiate(config.saveServicePrefab);

        var initiator = FindObjectOfType<SceneInitiator>();
        if (initiator != null)
        {
            initiator.Construct(
                factory,
                sceneController.gameObject,
                viewModel,
                playerSpawner,
                enemiesSpawner,
                mapManager,
                waypointService,
                respawnService,
                config.mapConfig,
                victory,
                saveService
            );
        }
    }
}
