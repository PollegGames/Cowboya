using UnityEngine;

public class ToggleBox : MonoBehaviour
{
    private bool isActive = false;
    public bool IsActive => isActive;   
    public float ToggleCost = 1f;

    public void Activate() => isActive = true;

    public void Deactivate() => isActive = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;

        ToggleButton toggleButton = other.GetComponent<ToggleButton>();
        if (toggleButton != null)
        {
            toggleButton.Toggle();
            isActive = false; // Only toggle once per activation, like AttackHitbox
        }
    }
}
