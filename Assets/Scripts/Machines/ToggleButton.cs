using UnityEngine;
using UnityEngine.Events;

public class ToggleButton : MonoBehaviour
{
    public UnityEvent OnToggle;

    public void Toggle()
    {
        Debug.Log($"ToggleButton pressed on {gameObject.name}");
        OnToggle?.Invoke();
    }
}