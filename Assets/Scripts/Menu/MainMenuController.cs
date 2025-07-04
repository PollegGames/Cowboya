// MainMenuController.cs (Refactored)
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuController : MonoBehaviour
{
    private VisualElement _menuRoot;
    private Button _playButton;
    private Button _exitButton;
    private AudioSource _audioSource;

    private void Awake()
    {
        _menuRoot = GetComponent<UIDocument>().rootVisualElement;
        _audioSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        _playButton = _menuRoot.Q<Button>("PlayBtn");
        _exitButton = _menuRoot.Q<Button>("ExitBtn");

        if (_playButton != null)
            _playButton.RegisterCallback<ClickEvent>(OnPlayClicked);

        if (_exitButton != null)
            _exitButton.RegisterCallback<ClickEvent>(OnExitClicked);
    }

    private void OnDisable()
    {
        if (_playButton != null)
            _playButton.UnregisterCallback<ClickEvent>(OnPlayClicked);

        if (_exitButton != null)
            _exitButton.UnregisterCallback<ClickEvent>(OnExitClicked);
    }

    private void OnPlayClicked(ClickEvent evt)
    {
        _audioSource?.Play();
        SceneController.instance.LoadScene("RunSetupScene");
    }

    private void OnExitClicked(ClickEvent evt)
    {
        _audioSource?.Play();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}