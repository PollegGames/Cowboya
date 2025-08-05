using UnityEngine;
using UnityEngine.InputSystem;

public class PauseController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PauseMenuUI _pauseUI;
    [SerializeField] private InputActionAsset _actions;            
    [SerializeField] private string _pauseActionName = "UI/Pause"; 

    private InputAction _pauseAction;
    private bool _isPaused = false;

    void OnEnable()
    {
        // 1) Grab & enable the Pause action
        _pauseAction = _actions.FindAction(_pauseActionName);
        if (_pauseAction == null)
        {
            Debug.LogError($"PauseController: action '{_pauseActionName}' not found in InputActionAsset.");
            return;
        }
        _pauseAction.performed += OnPausePerformed;
        _pauseAction.Enable();

        // 2) Wire UI callbacks
        _pauseUI.OnResumeClicked   += TogglePause;
        _pauseUI.OnRestartClicked  += RestartRun;
        _pauseUI.OnMainMenuClicked += GoToMainMenu;
    }

    void OnDisable()
    {
        if (_pauseAction != null)
        {
            _pauseAction.performed -= OnPausePerformed;
            _pauseAction.Disable();
        }

        _pauseUI.OnResumeClicked   -= TogglePause;
        _pauseUI.OnRestartClicked  -= RestartRun;
        _pauseUI.OnMainMenuClicked -= GoToMainMenu;
    }

    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        TogglePause();
    }

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        Time.timeScale = _isPaused ? 0f : 1f;

        if (_isPaused)
            _pauseUI.Show();
        else
            _pauseUI.Hide();
    }

    private void RestartRun()
    {
        Time.timeScale = 1f;
        RunProgressManager.Instance.RestartRun();
    }

    private void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneController.instance.LoadScene("MenuScene");
    }
}
