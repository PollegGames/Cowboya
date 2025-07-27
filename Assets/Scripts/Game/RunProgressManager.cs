using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunProgressManager : MonoBehaviour
{
    public static RunProgressManager Instance { get; private set; }

    [SerializeField] private List<RunMapConfigSO> mapConfigs = new List<RunMapConfigSO>();
    [SerializeField] private string runNormalSceneName = "MapGeneration";
    [SerializeField] private string runSandboxSceneName = "SetupSandbox";
    [SerializeField] private SceneController sceneControllerPrefab;

    private int currentLevelIndex = 1;

    public RunMapConfigSO CurrentConfig
    {
        get
        {
            if (mapConfigs == null || mapConfigs.Count == 0) return null;
            currentLevelIndex = Mathf.Clamp(currentLevelIndex, 0, mapConfigs.Count - 1);
            return mapConfigs[currentLevelIndex];
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
        if (SceneController.instance == null)
        {
            Instantiate(sceneControllerPrefab);
        }
    }
    public void LoadSandBox()
    {
        currentLevelIndex = 0;
        SceneController.instance.LoadScene(runSandboxSceneName);
    }
    public void LoadStressTestLevel()
    {
        currentLevelIndex = 0;
        SceneController.instance.LoadScene(runNormalSceneName);
    }
    //load first level
    public void LoadFirstLevel()
    {
        currentLevelIndex = 1;
        SceneController.instance.LoadScene(runNormalSceneName);
    }

    public void LoadNextLevel()
    {
        if (mapConfigs == null || mapConfigs.Count == 0) return;
        currentLevelIndex = Mathf.Clamp(currentLevelIndex + 1, 0, mapConfigs.Count - 1);
        SceneController.instance.LoadScene(runNormalSceneName);
    }

    public void RestartRun()
    {
        currentLevelIndex = 1;
        SceneController.instance.LoadScene(runNormalSceneName);
    }
}
