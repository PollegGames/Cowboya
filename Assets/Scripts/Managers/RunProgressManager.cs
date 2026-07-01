using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum RunStepKind
{
    StaticLevel,
    Laboratory,
    GeneratedLevel
}

[System.Serializable]
public class RunStepDefinition
{
    public RunStepKind Kind;
    public string SceneName;
    public RunMapConfigSO GeneratedConfig;
}

public class RunProgressManager : MonoBehaviour
{
    public static RunProgressManager Instance { get; private set; }

    [SerializeField] private List<RunMapConfigSO> mapConfigs = new List<RunMapConfigSO>();
    private readonly List<RunMapConfigSO> originalMapConfigs = new List<RunMapConfigSO>();
    [SerializeField] private List<RunStepDefinition> runSteps = new List<RunStepDefinition>();
    [SerializeField] private RunMapConfigSO sandboxConfig;
    [SerializeField] private string runNormalSceneName = "MapGeneration";
    [SerializeField] private string firstStaticLevelSceneName = "Level_1";
    [SerializeField] private string laboratorySceneName = "Level_Laboratory";
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
    private int currentRunStepIndex = -1;
    private bool hasActiveRun;

    public int CurrentLevelIndex => currentLevelIndex;
    public int CurrentRunStepIndex => currentRunStepIndex;
    public PlayerRunStats RunStats => runStats;
    public bool HasActiveRun => hasActiveRun;
    public RunStepKind CurrentStepKind => GetCurrentStepKind();

    public RunMapConfigSO CurrentConfig
    {
        get
        {
            if (currentLevelIndex == 0)
            {
                return sandboxConfig;
            }

            if (CurrentStepKind != RunStepKind.GeneratedLevel)
            {
                return null;
            }

            RunStepDefinition step = GetCurrentStep();
            if (step != null && step.GeneratedConfig != null)
            {
                return step.GeneratedConfig;
            }

            if (mapConfigs == null || mapConfigs.Count == 0)
            {
                return null;
            }

            int index = GetGeneratedStepOrdinal(currentRunStepIndex);
            RunMapConfigSO cfg;
            if (index >= mapConfigs.Count)
            {
                cfg = CreateDynamicConfig(index + 1);
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
        EnsureDefaultRunSteps();
        if (SceneController.instance == null)
        {
            Instantiate(sceneControllerPrefab);
        }
    }
    public void LoadSandBox()
    {
        // Index 0 is reserved for the sandbox configuration
        currentLevelIndex = 0;
        currentRunStepIndex = -1;
        hasActiveRun = false;
        playerTemplate.ResetStats();
        runStats.Reset();
        SceneController.instance.LoadScene(runSandboxSceneName);
    }
    public void LoadStressTestLevel()
    {
        currentLevelIndex = 0;
        currentRunStepIndex = -1;
        hasActiveRun = true;
        playerTemplate.ResetStats();
        runStats.Reset();
        SceneController.instance.LoadScene(runNormalSceneName);
    }
    //load first level
    public void LoadFirstLevel()
    {
        ResetMapConfigs();
        currentRunStepIndex = -1;
        currentLevelIndex = 1;
        hasActiveRun = true;
        playerTemplate.ResetStats();
        runStats.Reset();
        LoadStep(0);
    }

    public void LoadNextLevel()
    {
        LoadNextStep();
    }

    public void LoadNextStep()
    {
        if (!hasActiveRun)
        {
            hasActiveRun = true;
        }

        LoadStep(currentRunStepIndex + 1);
    }

    public void LoadStep(int stepIndex)
    {
        EnsureDefaultRunSteps();
        RunStepDefinition step = ResolveStep(stepIndex);
        if (step == null)
        {
            Debug.LogError($"RunProgressManager: no run step configured for index {stepIndex}.");
            return;
        }

        if (!CanLoadScene(step.SceneName))
        {
            Debug.LogError($"RunProgressManager: configured scene '{step.SceneName}' is missing from Build Settings or cannot be loaded.");
            return;
        }

        currentRunStepIndex = stepIndex;
        currentLevelIndex = ResolvePlayableLevelIndex(stepIndex, step.Kind);
        hasActiveRun = true;
        SceneController.instance.LoadScene(step.SceneName);
    }

    public void RestartRun()
    {
        ResetMapConfigs();
        currentRunStepIndex = -1;
        currentLevelIndex = 1;
        hasActiveRun = true;
        playerTemplate.ResetStats();
        runStats.Reset();
        LoadStep(0);
    }

    public void EnsureRunContextForActiveScene(string sceneName)
    {
        if (hasActiveRun)
            return;

        EnsureDefaultRunSteps();
        int index = FindStepIndexForScene(sceneName);
        if (index < 0)
            return;

        currentRunStepIndex = index;
        currentLevelIndex = ResolvePlayableLevelIndex(index, runSteps[index].Kind);
        hasActiveRun = true;
        Debug.Log($"RunProgressManager: direct scene play context set to step {currentRunStepIndex} ({sceneName}).");
    }

    public SceneSetupMode GetSetupModeForActiveScene(SceneSetupMode fallback)
    {
        if (!hasActiveRun)
            return fallback;

        return CurrentStepKind switch
        {
            RunStepKind.StaticLevel => SceneSetupMode.StaticLevel,
            RunStepKind.Laboratory => SceneSetupMode.Laboratory,
            RunStepKind.GeneratedLevel => SceneSetupMode.GeneratedMap,
            _ => fallback
        };
    }

    private void ResetMapConfigs()
    {
        mapConfigs.Clear();
        mapConfigs.AddRange(originalMapConfigs);
    }

    private void EnsureDefaultRunSteps()
    {
        if (runSteps == null)
        {
            runSteps = new List<RunStepDefinition>();
        }

        if (runSteps.Count > 0)
            return;

        runSteps.Add(new RunStepDefinition { Kind = RunStepKind.StaticLevel, SceneName = firstStaticLevelSceneName });
        runSteps.Add(new RunStepDefinition { Kind = RunStepKind.Laboratory, SceneName = laboratorySceneName });
        runSteps.Add(new RunStepDefinition { Kind = RunStepKind.GeneratedLevel, SceneName = runNormalSceneName });
    }

    private RunStepDefinition ResolveStep(int stepIndex)
    {
        if (stepIndex < runSteps.Count)
            return runSteps[stepIndex];

        RunStepKind previousKind = GetStepKindAtIndex(stepIndex - 1);
        if (previousKind == RunStepKind.GeneratedLevel)
        {
            return new RunStepDefinition { Kind = RunStepKind.Laboratory, SceneName = laboratorySceneName };
        }

        if (previousKind == RunStepKind.Laboratory)
        {
            return new RunStepDefinition { Kind = RunStepKind.GeneratedLevel, SceneName = runNormalSceneName };
        }

        return null;
    }

    private RunStepDefinition GetCurrentStep()
    {
        EnsureDefaultRunSteps();
        if (currentRunStepIndex < 0)
            return null;

        return ResolveStep(currentRunStepIndex);
    }

    private RunStepKind GetCurrentStepKind()
    {
        if (currentRunStepIndex < 0)
            return RunStepKind.GeneratedLevel;

        return GetStepKindAtIndex(currentRunStepIndex);
    }

    private RunStepKind GetStepKindAtIndex(int stepIndex)
    {
        EnsureDefaultRunSteps();

        if (stepIndex < 0)
            return RunStepKind.GeneratedLevel;

        if (stepIndex < runSteps.Count && runSteps[stepIndex] != null)
            return runSteps[stepIndex].Kind;

        RunStepKind lastConfiguredKind = runSteps.Count > 0 && runSteps[runSteps.Count - 1] != null
            ? runSteps[runSteps.Count - 1].Kind
            : RunStepKind.GeneratedLevel;

        int dynamicOffset = stepIndex - runSteps.Count;
        bool useNextKind = dynamicOffset % 2 == 0;
        if (!useNextKind)
            return lastConfiguredKind;

        return lastConfiguredKind == RunStepKind.GeneratedLevel
            ? RunStepKind.Laboratory
            : RunStepKind.GeneratedLevel;
    }

    private int FindStepIndexForScene(string sceneName)
    {
        for (int i = 0; i < runSteps.Count; i++)
        {
            if (runSteps[i] != null && runSteps[i].SceneName == sceneName)
                return i;
        }

        if (sceneName == laboratorySceneName)
            return 1;

        if (sceneName == runNormalSceneName)
            return 2;

        if (sceneName == firstStaticLevelSceneName)
            return 0;

        return -1;
    }

    private int ResolvePlayableLevelIndex(int stepIndex, RunStepKind kind)
    {
        if (kind == RunStepKind.Laboratory && currentLevelIndex > 0)
            return currentLevelIndex;

        if (kind == RunStepKind.GeneratedLevel)
            return 6 + GetGeneratedStepOrdinal(stepIndex);

        int staticLevelCount = 0;
        int max = Mathf.Min(stepIndex, runSteps.Count - 1);
        for (int i = 0; i <= max; i++)
        {
            if (runSteps[i] != null && runSteps[i].Kind == RunStepKind.StaticLevel)
                staticLevelCount++;
        }

        return Mathf.Max(1, staticLevelCount);
    }

    private int GetGeneratedStepOrdinal(int stepIndex)
    {
        int generatedCount = 0;
        int max = stepIndex >= 0 ? stepIndex : currentRunStepIndex;
        for (int i = 0; i <= max; i++)
        {
            if (GetStepKindAtIndex(i) == RunStepKind.GeneratedLevel)
                generatedCount++;
        }

        return Mathf.Max(0, generatedCount - 1);
    }

    private bool CanLoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
            return false;

        if (Application.CanStreamedLevelBeLoaded(sceneName))
            return true;

        return SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{sceneName}.unity") >= 0;
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
