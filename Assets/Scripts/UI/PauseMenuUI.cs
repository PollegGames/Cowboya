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
        _uiDoc.visualTreeAsset = layout;
        _root = _uiDoc.rootVisualElement;

        _root.Q<Button>("resumeButton").clicked += () => OnResumeClicked?.Invoke();
        _root.Q<Button>("restartButton").clicked += () => OnRestartClicked?.Invoke();
        _root.Q<Button>("mainMenuButton").clicked += () => OnMainMenuClicked?.Invoke();

        gameObject.SetActive(false);  // hide at start
    }

    public void Show() => gameObject.SetActive(true);
    public void Hide() => gameObject.SetActive(false);
    public void Toggle() => gameObject.SetActive(!gameObject.activeSelf);
}
