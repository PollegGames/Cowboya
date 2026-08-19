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

/// <summary>
/// Optional transfer hook used when a grab controller gives up ownership without
/// invoking the normal release behaviour.
/// </summary>
public interface IGrabControllerDetachReceiver
{
    void OnDetachedFromGrabController();
}
