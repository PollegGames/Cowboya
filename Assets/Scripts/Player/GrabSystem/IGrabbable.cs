using UnityEngine;

public interface IGrabbable
{
    bool CanBeGrabbed();
    void OnGrab(Transform grabParent);
    void OnRelease(Vector2 throwForce);
    void OnAttract(Vector2 attractPoint);
}
