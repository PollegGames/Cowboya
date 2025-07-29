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

        var joint = obj.GetComponent<TargetJoint2D>();
        Assert.IsTrue(joint.enabled);
        Assert.AreEqual((Vector2)hand.position, joint.target);
        Assert.IsFalse(badge.CanBeGrabbed());

        Vector2 throwForce = new Vector2(2f, 1f);
        badge.OnRelease(throwForce);

        Assert.IsFalse(joint.enabled);
        Assert.IsTrue(badge.CanBeGrabbed());
    }
}
