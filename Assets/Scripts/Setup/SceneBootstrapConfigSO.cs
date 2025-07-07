using UnityEngine;

[CreateAssetMenu(fileName = "SceneBootstrapConfig", menuName = "Setup/Scene Bootstrap Config")]
public class SceneBootstrapConfigSO : ScriptableObject
{
    public FactoryManager factoryManagerPrefab;
    public PlayerSpawner playerSpawnerPrefab;
    public EnemiesSpawner enemiesSpawnerPrefab;
    public MapManager mapManagerPrefab;
    public WaypointService waypointServicePrefab;
    public RobotRespawnService respawnServicePrefab;
    public SceneController sceneControllerPrefab;
    public GameUIViewModel gameUIViewModelPrefab;
    public VictorySetup victorySetupPrefab;
    public RunMapConfigSO mapConfig;
}
