using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneController : MonoBehaviour
{
    public static SceneController instance;
    [SerializeField] private IFactoryManager factoryManager;

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
    public void Initialize(IFactoryManager factoryManager)
    {
        this.factoryManager = factoryManager;
        Debug.Log("SceneController initialized with factory manager.");
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
    }

    public void RobotSaved()
    {
        factoryManager.OnRobotSaved();
    }

}
