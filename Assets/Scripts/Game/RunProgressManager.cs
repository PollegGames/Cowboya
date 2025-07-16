using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunProgressManager : MonoBehaviour
{
    public static RunProgressManager Instance { get; private set; }

    [SerializeField] private List<RunMapConfigSO> mapConfigs = new List<RunMapConfigSO>();
    [SerializeField] private string runSceneName = "MapGeneration";

    private int currentLevelIndex = 0;

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
    }

    public void LoadNextLevel()
    {
        if (mapConfigs == null || mapConfigs.Count == 0) return;
        currentLevelIndex = Mathf.Clamp(currentLevelIndex + 1, 0, mapConfigs.Count - 1);
        SceneManager.LoadScene(runSceneName);
    }

    public void RestartRun()
    {
        currentLevelIndex = 0;
        SceneManager.LoadScene(runSceneName);
    }
}
