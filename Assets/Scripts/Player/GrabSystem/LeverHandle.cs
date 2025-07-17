using UnityEngine;
using UnityEngine.Events;

public class LeverHandle : MonoBehaviour, IGrabbable
{
    public float activationDistance = 1f;
    public UnityEvent OnActivated;

    private bool isGrabbed;
    private Vector2 grabStart;

    public bool CanBeGrabbed()
    {
        return true;
    }

    public void OnGrab(Transform grabParent)
    {
        isGrabbed = true;
        grabStart = transform.position;
    }

    public void OnRelease(Vector2 throwForce)
    {
        isGrabbed = false;
    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (!isGrabbed) return;
        float dist = Vector2.Distance(grabStart, attractPoint);
        if (dist >= activationDistance)
        {
            OnActivated?.Invoke();
            isGrabbed = false;
        }
    }
}
