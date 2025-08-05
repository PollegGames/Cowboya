using UnityEngine;
using UnityEngine.UIElements;

public class PauseMenuUI : MonoBehaviour
{
    private UIDocument document;
    private VisualElement root;
    private Button resumeButton;
    private Button restartButton;
    private Button mainMenuButton;
    private void Awake()
    {
        document = GetComponent<UIDocument>();
        if (document == null)
        {
            document = gameObject.AddComponent<UIDocument>();
            var tree = Resources.Load<VisualTreeAsset>("UI/PauseMenu");
            if (tree != null)
            {
                document.visualTreeAsset = tree;
            }
        }
    }

    private void OnEnable()
    {
        root = document.rootVisualElement;

        resumeButton = root.Q<Button>("resumeButton");
        restartButton = root.Q<Button>("restartButton");
        mainMenuButton = root.Q<Button>("mainMenuButton");

        if (resumeButton != null)
        {
            resumeButton.clicked += Hide;
        }

        if (restartButton != null)
        {
            restartButton.clicked += OnRestart;
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.clicked += OnMainMenu;
        }

        Hide();
    }

    /// <summary>
    /// Shows the pause menu and pauses the game.
    /// </summary>
    public void Show()
    {
        Time.timeScale = 0f;
        root.style.display = DisplayStyle.Flex;
    }

    /// <summary>
    /// Hides the pause menu and resumes the game.
    /// </summary>
    public void Hide()
    {
        Time.timeScale = 1f;
        root.style.display = DisplayStyle.None;
    }

    private void OnRestart()
    {
        Time.timeScale = 1f;
        RunProgressManager.Instance.RestartRun();
    }

    private void OnMainMenu()
    {
        Time.timeScale = 1f;
        SceneController.instance.LoadScene("MenuScene");
    }
}
