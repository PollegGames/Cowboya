using NUnit.Framework;
using UnityEngine;

public class PickupBoxTests
{
    [Test]
    public void PickupBox_GrabAndReleaseAppliesVelocity()
    {
        var hand = new GameObject("hand").transform;
        var obj = new GameObject("box");
        var rb = obj.AddComponent<Rigidbody2D>();
        var box = obj.AddComponent<PickupBox>();

        box.OnGrab(hand);
        Assert.AreEqual(RigidbodyType2D.Kinematic, rb.bodyType);
        Assert.AreEqual(hand, obj.transform.parent);

        Vector2 force = new Vector2(3f, 2f);
        box.OnRelease(force);

        Assert.AreEqual(RigidbodyType2D.Dynamic, rb.bodyType);
        Assert.AreEqual(force, rb.linearVelocity);
        Assert.IsNull(obj.transform.parent);
    }
}
