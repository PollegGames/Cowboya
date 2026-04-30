using NUnit.Framework;
using UnityEngine;

public class SecurityBadgePickupTests
{
    [Test]
    public void Badge_SetFollowTargetAttachesToTarget()
    {
        var anchorGO = new GameObject("anchor");
        anchorGO.transform.position = new Vector3(3f, 4f, 0f);

        var obj = new GameObject("badge");
        var rb = obj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 1f;
        var badge = obj.AddComponent<SecurityBadgePickup>();

        badge.SetFollowTarget(anchorGO.transform);

        var joint = obj.GetComponent<TargetJoint2D>();
        Assert.IsTrue(joint.enabled);
        Assert.AreEqual(RigidbodyType2D.Kinematic, rb.bodyType);
        Assert.AreEqual(0f, rb.gravityScale);
        Assert.AreEqual((Vector2)anchorGO.transform.position, joint.target);
        Assert.AreEqual(anchorGO.transform.position, obj.transform.position);

        badge.OnRelease(Vector2.zero);

        Assert.IsFalse(joint.enabled);
        Assert.AreEqual(RigidbodyType2D.Dynamic, rb.bodyType);
        Assert.AreEqual(1f, rb.gravityScale);
    }

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
