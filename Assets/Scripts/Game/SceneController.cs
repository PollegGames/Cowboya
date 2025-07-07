using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController instance;
    [SerializeField] private IFactoryManager factoryManager;
    [SerializeField] private GameUIViewModel gameUIViewModel;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate instances
        }
    }
    public void Initialize(IFactoryManager factoryManager, GameUIViewModel gameUIViewModel)
    {
        this.factoryManager = factoryManager;
        this.gameUIViewModel = gameUIViewModel;
    }

    public void LoadScene(string sceneName)
    {
        Debug.Log($"Loading scene: {sceneName}");
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(sceneName);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Find and destroy menu UI
        var menuUI = GameObject.Find("MainMenuCanvas"); // Replace with your actual UI GameObject name
        if (menuUI != null) Destroy(menuUI);

        // Unsubscribe so this only happens once
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    public void RobotKilled()
    {
        factoryManager.OnRobotKilled();
        gameUIViewModel.UpdateVictoryStatsUI();
    }

    public void RobotSaved()
    {
        factoryManager.OnRobotSaved();
        gameUIViewModel.UpdateVictoryStatsUI();
    }

}
