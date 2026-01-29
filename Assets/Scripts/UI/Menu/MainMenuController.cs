// MainMenuController.cs (Refactored)
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    private VisualElement _menuRoot;
    private Button _playButton;
    private Button _sandboxButton;
    private Button _exitButton;
    [SerializeField] private RunProgressManager runProgressManager;

    private void Awake()
    {
        _menuRoot = GetComponent<UIDocument>().rootVisualElement;
        if (RunProgressManager.Instance == null && runProgressManager != null)
        {
            Instantiate(runProgressManager);
        }
        runProgressManager = RunProgressManager.Instance;

        AudioManager.Instance?.PlayMenuMusic(0.5f);
    }

    private void OnEnable()
    {
        _playButton = _menuRoot.Q<Button>("PlayBtn");
        _sandboxButton = _menuRoot.Q<Button>("SandboxBtn");
        _exitButton = _menuRoot.Q<Button>("ExitBtn");

        if (_playButton != null)
            _playButton.RegisterCallback<ClickEvent>(OnPlayClicked);
        
        if (_sandboxButton != null)
            _sandboxButton.RegisterCallback<ClickEvent>(OnSandboxClicked);

        if (_exitButton != null)
            _exitButton.RegisterCallback<ClickEvent>(OnExitClicked);
    }

    private void OnDisable()
    {
        if (_playButton != null)
            _playButton.UnregisterCallback<ClickEvent>(OnPlayClicked);
        
        if (_sandboxButton != null)
            _sandboxButton.UnregisterCallback<ClickEvent>(OnSandboxClicked);

        if (_exitButton != null)
            _exitButton.UnregisterCallback<ClickEvent>(OnExitClicked);
    }

    private void OnPlayClicked(ClickEvent evt)
    {
        AudioManager.Instance?.PlayUIClick();
        var saveService = FindFirstObjectByType<PlayerSaveService>();
        saveService?.ResetSaveData();
        runProgressManager.LoadFirstLevel();
    }

    private void OnSandboxClicked(ClickEvent evt)
    {
        AudioManager.Instance?.PlayUIClick();
        runProgressManager.LoadSandBox();
    }

    private void OnExitClicked(ClickEvent evt)
    {
        AudioManager.Instance?.PlayUIClick();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
