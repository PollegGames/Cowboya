using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PauseController : MonoBehaviour
{
    public static PauseController Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private PauseMenuUI _pauseUI;          // assigné dans l’inspecteur
    [SerializeField] private InputActionReference _pauseActionRef;
    [SerializeField] private GameObject sceneControllerPrefab;
    private InputAction _pauseAction;
    private bool _isPaused;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (SceneController.instance == null && sceneControllerPrefab)
            Instantiate(sceneControllerPrefab);


        _pauseAction = _pauseActionRef.action;
        _pauseAction.performed += OnPausePerformed;
        _pauseAction.Enable();

        _pauseUI.OnResumeClicked += TogglePause;
    }


    private void OnDestroy()
    {
        if (_pauseAction != null)
        {
            _pauseAction.performed -= OnPausePerformed;
            _pauseAction.Disable();
        }

        _pauseUI.OnResumeClicked -= TogglePause;
        _pauseUI.OnRestartClicked -= RestartRun;
        _pauseUI.OnMainMenuClicked -= GoToMainMenu;
    }   

    private void OnPausePerformed(InputAction.CallbackContext _) => TogglePause();

    public void TogglePause()
    {
        _isPaused = !_isPaused;
        // Time.timeScale = _isPaused ? 0 : 1;
        if (_isPaused)
            _pauseUI.Show();
        else
            _pauseUI.Hide();
    }

    private void RestartRun()
    {
        Debug.Log("PauseController: RestartRun called");
        // Time.timeScale = 1;
        RunProgressManager.Instance.RestartRun();
    }

    private void GoToMainMenu()
    {
        Debug.Log("PauseController: GoToMainMenu called");
        // Time.timeScale = 1;
        SceneController.instance.LoadScene("MenuScene");
    }
}
