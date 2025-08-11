using NUnit.Framework;
using UnityEngine;

public class SecurityBadgePickupTests
{
    [Test]
    public void Badge_AttachAndReleaseChangesPhysics()
    {
        var handGO = new GameObject("hand");
        var inventory = handGO.AddComponent<Inventory>();
        var hand = handGO.transform;
        var obj = new GameObject("badge");
        var rb = obj.AddComponent<Rigidbody2D>();
        var badge = obj.AddComponent<SecurityBadgePickup>();

        Assert.IsTrue(badge.CanBeGrabbed(inventory));
        badge.OnGrab(hand);

        var joint = obj.GetComponent<TargetJoint2D>();
        Assert.IsTrue(joint.enabled);
        Assert.AreEqual((Vector2)hand.position, joint.target);
        Assert.IsTrue(badge.CanBeGrabbed(inventory));

        var otherInvGO = new GameObject("otherInv");
        var otherInventory = otherInvGO.AddComponent<Inventory>();
        otherInventory.SetItem(PickupType.SecurityBadge, new GameObject("otherBadge").AddComponent<SecurityBadgePickup>());
        Assert.IsFalse(badge.CanBeGrabbed(otherInventory));

        Vector2 throwForce = new Vector2(2f, 1f);
        badge.OnRelease(throwForce);

        Assert.IsFalse(joint.enabled);
        Assert.IsTrue(badge.CanBeGrabbed(inventory));
    }
}
