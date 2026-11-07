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
    private readonly Dictionary<int, RunMapConfigSO> dynamicConfigsByPlayableLevel = new();
    [SerializeField] private RunMapConfigSO sandboxConfig;
    [SerializeField] private string runNormalSceneName = "MapGeneration";
    [SerializeField] private string firstStaticLevelSceneName = "Level_1";
    [SerializeField] private string secondStaticLevelSceneName = "Level_2";
    [SerializeField] private string thirdStaticLevelSceneName = "Level_3";
    [SerializeField] private string laboratorySceneName = "Level_Laboratory";
    [SerializeField] private string runSandboxSceneName = "SetupSandbox";
    [SerializeField] private SceneController sceneControllerPrefab;
    [SerializeField] private PlayerTemplate playerTemplate;
    [SerializeField] private PlayerRunStats runStats;
    [SerializeField] private LaboratoryProgress laboratoryProgress = new LaboratoryProgress();

    // Maximum allowed values for dynamically generated configurations
    private const int MaxGridSize = 10;      // Maximum grid width/height
    private const int MaxPoiCount = 20;      // Maximum number of points of interest
    private const int MaxEnemiesCount = 20;  // Maximum number of enemies
    private const int MaxWorkersCount = 40;  // Maximum number of workers

    private int currentLevelIndex = 1;
    private int currentRunStepIndex = -1;
    private bool hasActiveRun;

    public int CurrentLevelIndex => currentLevelIndex;
    public int CurrentRunStepIndex => currentRunStepIndex;
    public PlayerRunStats RunStats => runStats;
    public LaboratoryProgress LaboratoryProgress
    {
        get
        {
            if (laboratoryProgress == null)
            {
                laboratoryProgress = new LaboratoryProgress();
            }

            return laboratoryProgress;
        }
    }
    public bool HasActiveRun => hasActiveRun;

    /// <summary>
    /// True while playing a level launched from the sandbox generation menu. Sandbox levels
    /// never touch the normal run itinerary: finishing or restarting stays in the sandbox.
    /// </summary>
    public bool IsSandboxRun => currentLevelIndex == 0 && currentRunStepIndex < 0;
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

            return GetOrCreateDynamicConfig(currentLevelIndex);
        }
    }

    /// <summary>
    /// Returns the generated-map setup used by a playable level number.
    /// Static levels do not have a generated-map configuration and return null.
    /// </summary>
    public RunMapConfigSO GetConfigForPlayableLevel(int playableLevelIndex)
    {
        if (playableLevelIndex < 1)
            return null;

        EnsureDefaultRunSteps();
        int stepIndex = FindGeneratedStepForPlayableLevel(playableLevelIndex);
        if (stepIndex < 0)
            return null;

        RunStepDefinition step = ResolveStep(stepIndex);
        if (step != null && step.GeneratedConfig != null)
            return step.GeneratedConfig;

        return GetOrCreateDynamicConfig(playableLevelIndex);
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
        LaboratoryProgress.Reset();
        SceneController.instance.LoadScene(runSandboxSceneName);
    }
    /// <summary>
    /// Goes back to the sandbox generation menu after a sandbox level, keeping its settings.
    /// </summary>
    public void ReturnToSandboxMenu()
    {
        currentLevelIndex = 0;
        currentRunStepIndex = -1;
        hasActiveRun = false;
        SceneController.instance.LoadScene(runSandboxSceneName);
    }

    public void LoadStressTestLevel()
    {
        currentLevelIndex = 0;
        currentRunStepIndex = -1;
        hasActiveRun = true;
        playerTemplate.ResetStats();
        runStats.Reset();
        LaboratoryProgress.Reset();
        SceneController.instance.LoadScene(runNormalSceneName);
    }
    //load first level
    public void LoadFirstLevel()
    {
        DeleteRunAndResetTransientStats();
        ResetMapConfigs();
        currentRunStepIndex = -1;
        currentLevelIndex = 1;
        hasActiveRun = true;
        playerTemplate.ResetStats();
        runStats.Reset();
        LaboratoryProgress.Reset();
        LoadStep(0);
    }

    public void LoadNextLevel()
    {
        LoadNextStep();
    }

    public void LoadNextStep()
    {
        if (IsSandboxRun)
        {
            ReturnToSandboxMenu();
            return;
        }

        if (!hasActiveRun)
        {
            hasActiveRun = true;
        }

        LoadStep(currentRunStepIndex + 1);
    }

    public void LoadStep(int stepIndex)
    {
        LoadStep(stepIndex, true);
    }

    private void LoadStep(int stepIndex, bool saveCheckpoint)
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
        if (saveCheckpoint)
        {
            RunSaveStorage.SaveReachedStep(currentRunStepIndex, currentLevelIndex);
        }
        SceneController.instance.LoadScene(step.SceneName);
    }

    /// <summary>
    /// Reloads the currently reached level without creating a new run.
    /// </summary>
    public void RestartCurrentLevel()
    {
        if (IsSandboxRun)
        {
            SceneController.instance.LoadScene(runNormalSceneName);
            return;
        }

        if (!hasActiveRun || currentRunStepIndex < 0)
        {
            Debug.LogWarning("RunProgressManager: cannot restart because no run step is active.");
            return;
        }

        LoadStep(currentRunStepIndex, false);
    }

    /// <summary>
    /// Retained for existing callers; restarting now reloads only the current level.
    /// </summary>
    public void RestartRun()
    {
        RestartCurrentLevel();
    }

    /// <summary>
    /// Ends the current run after player death and clears its checkpoint.
    /// </summary>
    public void EndRunAfterDeath()
    {
        if (IsSandboxRun)
        {
            // A sandbox death must not erase the checkpoint of the normal run.
            hasActiveRun = false;
            return;
        }

        DeleteRunAndResetTransientStats();
        currentRunStepIndex = -1;
        currentLevelIndex = 1;
        hasActiveRun = false;
        playerTemplate.ResetStats();
        runStats.Reset();
        LaboratoryProgress.Reset();
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
        foreach (RunMapConfigSO dynamicConfig in dynamicConfigsByPlayableLevel.Values)
        {
            if (dynamicConfig != null)
                Destroy(dynamicConfig);
        }
        dynamicConfigsByPlayableLevel.Clear();
        mapConfigs.Clear();
        mapConfigs.AddRange(originalMapConfigs);
    }

    private static void DeleteRunAndResetTransientStats()
    {
        RunSaveStorage.DeleteRun();

        PlayerSaveService saveService = FindFirstObjectByType<PlayerSaveService>();
        if (saveService != null)
        {
            saveService.ResetRunProgress();
        }
        else
        {
            PlayerSaveService.ResetPersistedRunProgress();
        }
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
        runSteps.Add(new RunStepDefinition { Kind = RunStepKind.StaticLevel, SceneName = secondStaticLevelSceneName });
        runSteps.Add(new RunStepDefinition { Kind = RunStepKind.Laboratory, SceneName = laboratorySceneName });
        runSteps.Add(new RunStepDefinition { Kind = RunStepKind.StaticLevel, SceneName = thirdStaticLevelSceneName });
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

        return -1;
    }

    private int ResolvePlayableLevelIndex(int stepIndex, RunStepKind kind)
    {
        if (kind == RunStepKind.Laboratory && currentLevelIndex > 0)
            return currentLevelIndex;

        if (kind == RunStepKind.GeneratedLevel)
            return CountPlayableLevelsThroughStep(stepIndex);

        int staticLevelCount = 0;
        int max = Mathf.Min(stepIndex, runSteps.Count - 1);
        for (int i = 0; i <= max; i++)
        {
            if (runSteps[i] != null && runSteps[i].Kind == RunStepKind.StaticLevel)
                staticLevelCount++;
        }

        return Mathf.Max(1, staticLevelCount);
    }

    private int CountPlayableLevelsThroughStep(int stepIndex)
    {
        int playableLevelCount = 0;
        for (int i = 0; i <= stepIndex; i++)
        {
            RunStepKind stepKind = GetStepKindAtIndex(i);
            if (stepKind == RunStepKind.StaticLevel || stepKind == RunStepKind.GeneratedLevel)
            {
                playableLevelCount++;
            }
        }

        return Mathf.Max(1, playableLevelCount);
    }

    private int FindGeneratedStepForPlayableLevel(int playableLevelIndex)
    {
        // A generated level is always followed by a laboratory in the dynamic
        // part of the run, so this loop stays small even for stress-test levels.
        int maximumStepCount = runSteps.Count + playableLevelIndex * 2;
        for (int stepIndex = 0; stepIndex < maximumStepCount; stepIndex++)
        {
            if (GetStepKindAtIndex(stepIndex) == RunStepKind.GeneratedLevel
                && CountPlayableLevelsThroughStep(stepIndex) == playableLevelIndex)
            {
                return stepIndex;
            }
        }

        return -1;
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

    private RunMapConfigSO GetOrCreateDynamicConfig(int playableLevelIndex)
    {
        if (dynamicConfigsByPlayableLevel.TryGetValue(playableLevelIndex, out RunMapConfigSO cached))
            return cached;

        RunMapConfigSO baseConfig = null;
        if (dynamicConfigsByPlayableLevel.TryGetValue(playableLevelIndex - 1, out RunMapConfigSO previousDynamic))
            baseConfig = previousDynamic;

        if (baseConfig == null)
        {
            int stepIndex = FindGeneratedStepForPlayableLevel(playableLevelIndex);
            for (int i = stepIndex - 1; i >= 0; i--)
            {
                RunStepDefinition previousStep = ResolveStep(i);
                if (previousStep != null && previousStep.Kind == RunStepKind.GeneratedLevel
                    && previousStep.GeneratedConfig != null)
                {
                    baseConfig = previousStep.GeneratedConfig;
                    break;
                }
            }
        }

        if (baseConfig == null && mapConfigs != null && mapConfigs.Count > 0)
            baseConfig = mapConfigs[mapConfigs.Count - 1];
        if (baseConfig == null)
            return null;

        RunMapConfigSO created = CreateDynamicConfig(baseConfig, playableLevelIndex);
        dynamicConfigsByPlayableLevel[playableLevelIndex] = created;
        return created;
    }

    private static RunMapConfigSO CreateDynamicConfig(RunMapConfigSO baseConfig, int playableLevelIndex)
    {
        RunMapConfigSO newConfig = ScriptableObject.CreateInstance<RunMapConfigSO>();
        newConfig.gridWidth = baseConfig.gridWidth;
        newConfig.gridHeight = baseConfig.gridHeight;
        newConfig.poiCount = baseConfig.poiCount;
        newConfig.workersCount = baseConfig.workersCount;
        newConfig.workerCollectorsCount = baseConfig.workerCollectorsCount;
        newConfig.workerCollectorsRestAfterBatch = baseConfig.workerCollectorsRestAfterBatch;
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

        // Dynamic generation moved down from Level 7 to Level 6. Use the former
        // level number for growth cadence so new Level 6 receives the exact
        // progression step that old Level 7 used, new Level 7 gets old Level 8,
        // and so on.
        int formerPlayableLevelIndex = playableLevelIndex + 1;
        int increment = 1;
        if (formerPlayableLevelIndex % 2 == 0)
        {
            increment += 2;
        }

        newConfig.poiCount += increment;

        newConfig.workersCount += increment;
        newConfig.enemiesCount += increment;

        // Clamp values to their respective maximums
        newConfig.gridWidth = Mathf.Min(newConfig.gridWidth, MaxGridSize);
        newConfig.gridHeight = Mathf.Min(newConfig.gridHeight, MaxGridSize);
        newConfig.poiCount = Mathf.Min(newConfig.poiCount, MaxPoiCount);
        newConfig.enemiesCount = Mathf.Min(newConfig.enemiesCount, MaxEnemiesCount);
        newConfig.workersCount = Mathf.Min(newConfig.workersCount, MaxWorkersCount);

        return newConfig;
    }
}
