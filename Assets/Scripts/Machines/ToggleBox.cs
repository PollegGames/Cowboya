using UnityEngine;

public class ToggleBox : MonoBehaviour
{
    private bool isActive = false;
    public float ToggleCost = 1f;

    public void Activate() => isActive = true;
    public void Deactivate() => isActive = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;

        Debug.Log($"ToggleBox activated by {other.name}");
        // Get ToggleButton on the same GameObject as the collider
        ToggleButton toggleButton = other.GetComponent<ToggleButton>();
        if (toggleButton != null)
        {
            toggleButton.Toggle();
            isActive = false; // Only toggle once per activation, like AttackHitbox
        }
    }
}