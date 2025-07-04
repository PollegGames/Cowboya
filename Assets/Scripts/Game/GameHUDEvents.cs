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
    private void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject); // Prevent duplicates
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject); // Keep this object across scenes
    }
    private void Start()
    {
        var _document = GetComponent<UIDocument>().rootVisualElement;

        // Example HUD setup
        // _button = _document.Q<Button>("pauseButton");
        // _button.RegisterCallback<ClickEvent>(OnPauseButtonClick);
    }

    private void OnPauseButtonClick(ClickEvent evt)
    {
        Debug.Log("Pause Button Clicked!");
        // Pause game logic
    }

    public void UpdateHealth(int health)
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        var healthLabel = root.Q<Label>("healthLabel");
        healthLabel.text = $"Health: {health}";
    }
}
