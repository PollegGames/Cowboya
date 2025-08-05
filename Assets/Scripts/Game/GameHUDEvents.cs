using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class GameHUD_UI : MonoBehaviour
{
    private UIDocument _document;
    private Button _button;
    private List<Button> _othersButtons = new List<Button>();
    private AudioSource _audioSource;
    private static GameHUD_UI _instance;

    [SerializeField] private PauseMenuUI pauseMenuUI;
    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject); // Prevent duplicates
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject); // Keep this object across scenes

        _document = GetComponent<UIDocument>();
        if (_document == null)
        {
            _document = gameObject.AddComponent<UIDocument>();
            var tree = Resources.Load<VisualTreeAsset>("UI/GameHUDVisualTree");
            if (tree != null)
            {
                _document.visualTreeAsset = tree;
            }
        }
    }
    private void Start()
    {
        var root = _document.rootVisualElement;

        _button = root.Q<Button>("pauseButton");
        if (_button != null)
        {
            _button.clicked += OnPauseButtonClick;
            _button.style.display = DisplayStyle.Flex;
        }
    }

    private void OnPauseButtonClick()
    {
        if (pauseMenuUI != null)
        {
            pauseMenuUI.Show();
        }
    }

    /// <summary>
    /// Updates the health label in the HUD.
    /// </summary>
    /// <param name="health">Current player health.</param>
    public void UpdateHealth(int health)
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        var healthLabel = root.Q<Label>("healthLabel");
        healthLabel.text = $"Health: {health}";
    }
}
