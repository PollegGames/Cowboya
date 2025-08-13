using TMPro;
using UnityEngine;

/// <summary>
/// Displays the current level number on the canvas.
/// </summary>
public class CanvasLevelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _levelText;

    private void Start()
    {
        UpdateLevelLabel();
    }

    /// <summary>
    /// Refreshes the level label text based on RunProgressManager.
    /// </summary>
    public void UpdateLevelLabel()
    {
        if (_levelText == null)
        {
            Debug.LogError("CanvasLevelUI: Level text reference is missing.");
            return;
        }

        int index = RunProgressManager.Instance != null ? RunProgressManager.Instance.CurrentLevelIndex : 0;
        int realLevel = index - 1;
        string label = index <= 1 ? "Level Tutorial" : $"Level {realLevel}";
        _levelText.text = label;
    }
}

