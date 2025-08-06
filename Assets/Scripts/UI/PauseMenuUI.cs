using System;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PauseMenuUI : MonoBehaviour
{
    public event Action OnResumeClicked;
    public event Action OnRestartClicked;
    public event Action OnMainMenuClicked;

    [SerializeField] VisualTreeAsset layout;  // drag in your UXML asset

    UIDocument _uiDoc;
    VisualElement _root;

    void Awake()
    {
        _uiDoc = GetComponent<UIDocument>();
        if (_uiDoc == null)
        {
            Debug.LogError("PauseMenuUI: UIDocument component is missing.");
            enabled = false;
            return;
        }

        if (layout == null)
        {
            Debug.LogError("PauseMenuUI: Layout is not assigned.");
            enabled = false;
            return;
        }

        _uiDoc.visualTreeAsset = layout;
        _root = _uiDoc.rootVisualElement;

        var resumeButton = _root.Q<Button>("resumeButton");
        if (resumeButton != null)
            resumeButton.clicked += () => OnResumeClicked?.Invoke();
        else
            Debug.LogError("PauseMenuUI: 'resumeButton' not found in layout.");

        var restartButton = _root.Q<Button>("restartButton");
        if (restartButton != null)
            restartButton.clicked += () => OnRestartClicked?.Invoke();
        else
            Debug.LogError("PauseMenuUI: 'restartButton' not found in layout.");

        var mainMenuButton = _root.Q<Button>("mainMenuButton");
        if (mainMenuButton != null)
            mainMenuButton.clicked += () => OnMainMenuClicked?.Invoke();
        else
            Debug.LogError("PauseMenuUI: 'mainMenuButton' not found in layout.");

        gameObject.SetActive(false);  // hide at start
    }

    public void Show()  => gameObject.SetActive(true);
    public void Hide()  => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
}
