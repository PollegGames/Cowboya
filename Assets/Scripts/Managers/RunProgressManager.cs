using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunProgressManager : MonoBehaviour
{
    public static RunProgressManager Instance { get; private set; }

    [SerializeField] private List<RunMapConfigSO> mapConfigs = new List<RunMapConfigSO>();
    private readonly List<RunMapConfigSO> originalMapConfigs = new List<RunMapConfigSO>();
    [SerializeField] private RunMapConfigSO sandboxConfig;
    [SerializeField] private string runNormalSceneName = "MapGeneration";
    [SerializeField] private string runSandboxSceneName = "SetupSandbox";
    [SerializeField] private SceneController sceneControllerPrefab;
    [SerializeField] private PlayerTemplate playerTemplate;
    [SerializeField] private PlayerRunStats runStats;

    // Maximum allowed values for dynamically generated configurations
    private const int MaxGridSize = 10;      // Maximum grid width/height
    private const int MaxPoiCount = 20;      // Maximum number of points of interest
    private const int MaxBlockedCount = 10;  // Maximum number of blocked cells
    private const int MaxEnemiesCount = 20;  // Maximum number of enemies
    private const int MaxWorkersCount = 40;  // Maximum number of workers

    private int currentLevelIndex = 1;

    public int CurrentLevelIndex => currentLevelIndex;
    public PlayerRunStats RunStats => runStats;

    public RunMapConfigSO CurrentConfig
    {
        get
        {
            if (currentLevelIndex == 0)
            {
                return sandboxConfig;
            }

            if (mapConfigs == null || mapConfigs.Count == 0)
            {
                return null;
            }

            int index = currentLevelIndex - 1;
            RunMapConfigSO cfg;
            if (index >= mapConfigs.Count)
            {
                cfg = CreateDynamicConfig(currentLevelIndex);
            }
            else
            {
                cfg = mapConfigs[index];
            }
            return cfg;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        originalMapConfigs.AddRange(mapConfigs);
        if (SceneController.instance == null)
        {
            Instantiate(sceneControllerPrefab);
        }
    }
    public void LoadSandBox()
    {
        // Index 0 is reserved for the sandbox configuration
        currentLevelIndex = 0;
        playerTemplate.ResetStats();
        runStats.Reset();
        SceneController.instance.LoadScene(runSandboxSceneName);
    }
    public void LoadStressTestLevel()
    {
        currentLevelIndex = 0;
        playerTemplate.ResetStats();
        runStats.Reset();
        SceneController.instance.LoadScene(runNormalSceneName);
    }
    //load first level
    public void LoadFirstLevel()
    {
        ResetMapConfigs();
        currentLevelIndex = 1;
        playerTemplate.ResetStats();
        runStats.Reset();
        SceneController.instance.LoadScene(runNormalSceneName);
    }

    public void LoadNextLevel()
    {
        if (mapConfigs == null || mapConfigs.Count == 0) return;
        currentLevelIndex++;
        SceneController.instance.LoadScene(runNormalSceneName);
    }

    public void RestartRun()
    {
        ResetMapConfigs();
        currentLevelIndex = 1;
        playerTemplate.ResetStats();
        runStats.Reset();
        SceneController.instance.LoadScene(runNormalSceneName);
    }

    private void ResetMapConfigs()
    {
        mapConfigs.Clear();
        mapConfigs.AddRange(originalMapConfigs);
    }

    private RunMapConfigSO CreateDynamicConfig(int levelIndex)
    {
        RunMapConfigSO baseConfig = mapConfigs[mapConfigs.Count - 1];
        RunMapConfigSO newConfig = ScriptableObject.CreateInstance<RunMapConfigSO>();
        newConfig.gridWidth = baseConfig.gridWidth;
        newConfig.gridHeight = baseConfig.gridHeight;
        newConfig.poiCount = baseConfig.poiCount;
        newConfig.blockedCount = baseConfig.blockedCount;
        newConfig.workersCount = baseConfig.workersCount;
        newConfig.enemiesCount = baseConfig.enemiesCount;

        newConfig.GenerateRandomSeed();
        if (Random.value > 0.5f)
        {
            newConfig.gridWidth += 1;
        }
        else
        {
            newConfig.gridHeight += 1;
        }

        int increment = 1;
        if (levelIndex % 2 == 0 && levelIndex >= 4)
        {
            increment += 2;
        }

        if (Random.value > 0.5f)
        {
            newConfig.poiCount += increment;
        }
        else
        {
            newConfig.blockedCount += increment;
        }

        newConfig.workersCount += increment;
        newConfig.enemiesCount += increment;

        // Clamp values to their respective maximums
        newConfig.gridWidth = Mathf.Min(newConfig.gridWidth, MaxGridSize);
        newConfig.gridHeight = Mathf.Min(newConfig.gridHeight, MaxGridSize);
        newConfig.poiCount = Mathf.Min(newConfig.poiCount, MaxPoiCount);
        newConfig.blockedCount = Mathf.Min(newConfig.blockedCount, MaxBlockedCount);
        newConfig.enemiesCount = Mathf.Min(newConfig.enemiesCount, MaxEnemiesCount);
        newConfig.workersCount = Mathf.Min(newConfig.workersCount, MaxWorkersCount);

        mapConfigs.Add(newConfig);
        return newConfig;
    }
}
