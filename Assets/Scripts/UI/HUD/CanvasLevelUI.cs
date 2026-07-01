using TMPro;
using UnityEngine;

/// <summary>
/// Keeps the legacy level label empty because run level numbers are not player-facing.
/// </summary>
public class CanvasLevelUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _levelText;

    private void Start()
    {
        UpdateLevelLabel();
    }

    /// <summary>
    /// Clears the legacy level label.
    /// </summary>
    public void UpdateLevelLabel()
    {
        if (_levelText == null)
        {
            Debug.LogError("CanvasLevelUI: Level text reference is missing.");
            return;
        }

        _levelText.text = string.Empty;
    }
}

