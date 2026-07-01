using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplaySceneAutoBootstrapper
{
    private const string SceneGameSetupResourcePath = "Prefabs/Map/SceneGameSetup";
    private const string LevelOneSceneName = "Level_1";
    private const string LaboratorySceneName = "Level_Laboratory";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureSetupForScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSetupForScene(scene);
    }

    private static void EnsureSetupForScene(Scene scene)
    {
        if (scene.name != LevelOneSceneName && scene.name != LaboratorySceneName)
            return;

        if (Object.FindFirstObjectByType<SceneBootstrapper>() != null)
            return;

        GameObject setupPrefab = Resources.Load<GameObject>(SceneGameSetupResourcePath);
        if (setupPrefab == null)
        {
            Debug.LogError($"GameplaySceneAutoBootstrapper: missing Resources/{SceneGameSetupResourcePath} prefab.");
            return;
        }

        Object.Instantiate(setupPrefab);
    }
}
