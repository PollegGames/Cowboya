using UnityEngine;
using UnityEngine.Events;

public class LeverHandle : MonoBehaviour, IGrabbable
{
    [Header("Path Settings")]
    public Transform startPoint;
    public Transform endPoint;
    [Range(0f, 1f)]
    public float activationThreshold = 0.9f;

    [Header("Events")]
    public UnityEvent OnActivated;

    private bool _isGrabbed;
    private float _currentT;

    public bool CanBeGrabbed()
    {
        return true;
    }

    public void OnGrab(Transform grabParent)
    {
        _isGrabbed = true;
        UpdatePosition(grabParent.position);
    }

    public void OnRelease(Vector2 throwForce)
    {
        _isGrabbed = false;
        if (_currentT >= activationThreshold)
            OnActivated?.Invoke();
    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (!_isGrabbed) return;
        UpdatePosition(attractPoint);
    }

    private void UpdatePosition(Vector2 target)
    {
        if (startPoint == null || endPoint == null) return;

        Vector2 start = startPoint.position;
        Vector2 end = endPoint.position;
        Vector2 dir = end - start;
        float len = dir.magnitude;
        if (len <= 0.0001f) return;

        float t = Vector2.Dot(target - start, dir.normalized) / len;
        _currentT = Mathf.Clamp01(t);
        transform.position = start + dir * _currentT;
    }
}
