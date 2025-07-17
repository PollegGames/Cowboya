using UnityEngine;
using UnityEngine.Events;

public class HoldButton : MonoBehaviour, IGrabbable
{
    public UnityEvent OnPressed;
    public UnityEvent OnReleased;
    private bool isHeld;

    public bool CanBeGrabbed()
    {
        return true;
    }

    public void OnGrab(Transform grabParent)
    {
        if (isHeld) return;
        isHeld = true;
        OnPressed?.Invoke();
    }

    public void OnRelease(Vector2 throwForce)
    {
        if (!isHeld) return;
        isHeld = false;
        OnReleased?.Invoke();
    }

    public void OnAttract(Vector2 attractPoint)
    {
    }
}
