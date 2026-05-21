using UnityEngine;

public interface IGrabbable
{
    bool CanBeGrabbed(Inventory inventory);
    void OnGrab(Transform grabParent);
    void OnRelease(Vector2 throwForce);
    void OnAttract(Vector2 attractPoint);
}

public interface IGrabContextReceiver
{
    void SetGrabContext(Collider2D sourceCollider, Vector2 grabOrigin);
}
