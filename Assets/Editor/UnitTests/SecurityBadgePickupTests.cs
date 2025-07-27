using NUnit.Framework;
using UnityEngine;

public class SecurityBadgePickupTests
{
    [Test]
    public void Badge_AttachAndReleaseChangesPhysics()
    {
        var hand = new GameObject("hand").transform;
        var obj = new GameObject("badge");
        var rb = obj.AddComponent<Rigidbody2D>();
        var badge = obj.AddComponent<SecurityBadgePickup>();

        Assert.IsTrue(badge.CanBeGrabbed());
        badge.OnGrab(hand);

        Assert.IsFalse(rb.simulated);
        Assert.AreEqual(Vector2.zero, rb.linearVelocity);
        Assert.AreEqual(hand, obj.transform.parent);
        Assert.IsFalse(badge.CanBeGrabbed());

        Vector2 throwForce = new Vector2(2f, 1f);
        badge.OnRelease(throwForce);

        Assert.IsTrue(rb.simulated);
        Assert.AreEqual(throwForce, rb.linearVelocity);
        Assert.IsNull(obj.transform.parent);
        Assert.IsTrue(badge.CanBeGrabbed());
    }
}
