using NUnit.Framework;
using System.Reflection;
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
    public void Badge_SetFollowTargetWithOffsetKeepsBadgeVisibleBesideTarget()
    {
        var anchorGO = new GameObject("anchor");
        anchorGO.transform.position = new Vector3(3f, 4f, 0f);
        anchorGO.transform.localScale = new Vector3(5f, 5f, 5f);

        var obj = new GameObject("badge");
        obj.AddComponent<Rigidbody2D>();
        var badge = obj.AddComponent<SecurityBadgePickup>();
        var localOffset = new Vector3(0.55f, 0.3f, 0f);

        badge.SetFollowTarget(anchorGO.transform, localOffset);

        Vector3 expectedPosition = anchorGO.transform.position + anchorGO.transform.rotation * localOffset;
        Assert.AreEqual(expectedPosition, obj.transform.position);
        Assert.AreEqual((Vector2)expectedPosition, obj.GetComponent<TargetJoint2D>().target);
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

    [Test]
    public void GrabSelection_LivingEnemyBadgeInCloseRange_WinsOverEnemyBody()
    {
        var setup = CreateEnemyWithBadge(new Vector2(20f, 0f), 0.15f, RobotState.Alive);

        IGrabbable detected = DetectGrabbable(setup.controller, setup.hand.position, setup.inventory);

        Assert.AreEqual(setup.badge, detected, "A close badge on a living enemy should be selected before its rigidbody.");
    }

    [Test]
    public void GrabSelection_LivingEnemyBadgeOutsideCloseRange_SelectsEnemyBody()
    {
        var setup = CreateEnemyWithBadge(new Vector2(40f, 0f), 0.6f, RobotState.Alive);

        IGrabbable detected = DetectGrabbable(setup.controller, setup.hand.position, setup.inventory);

        Assert.AreEqual(setup.enemy, detected, "Stealing from a living enemy should require close contact with the badge.");
    }

    [Test]
    public void GrabSelection_DeadEnemyBadgeUsesFullGrabRange_AndWinsOverBody()
    {
        var setup = CreateEnemyWithBadge(new Vector2(60f, 0f), 0.6f, RobotState.Dead);

        IGrabbable detected = DetectGrabbable(setup.controller, setup.hand.position, setup.inventory);

        Assert.AreEqual(setup.badge, detected, "A dead enemy's badge should remain easy to select within normal grab range.");
    }

    [Test]
    public void ReceptionBadge_CanOnlyBeGrabbedAfterReceptionRobotFaints()
    {
        var setup = CreateEnemyWithBadge(
            new Vector2(80f, 0f),
            0.15f,
            RobotState.Alive,
            RobotRole.SecurityReception);

        Assert.IsFalse(setup.badge.CanBeGrabbed(setup.inventory));

        setup.enemy.GetComponent<RobotStateController>().UpdateState(RobotState.Faint);

        Assert.IsTrue(setup.badge.CanBeGrabbed(setup.inventory));
    }

    [Test]
    public void GrabSelection_FaintReceptionBadge_WinsOverReceptionBody()
    {
        var setup = CreateEnemyWithBadge(
            new Vector2(100f, 0f),
            0.15f,
            RobotState.Faint,
            RobotRole.SecurityReception);

        IGrabbable detected = DetectGrabbable(setup.controller, setup.hand.position, setup.inventory);

        Assert.AreEqual(setup.badge, detected, "A faint reception robot's badge should be stealable by the player.");
    }

    private static (CowboyGrabController controller, Transform hand, Inventory inventory, EnemyGrabbable enemy, SecurityBadgePickup badge)
        CreateEnemyWithBadge(
            Vector2 position,
            float badgeOffset,
            RobotState state,
            RobotRole role = RobotRole.SecurityGuard)
    {
        var player = new GameObject("PlayerGrabber");
        player.transform.position = position;
        var inventory = player.AddComponent<Inventory>();
        var controller = player.AddComponent<CowboyGrabController>();
        var hand = new GameObject("GrabHand").transform;
        hand.SetParent(player.transform, false);
        SetPrivateField(controller, "leftHandGrabAnchor", hand);
        SetPrivateField(controller, "leftHandHoldParent", hand);
        SetPrivateField(controller, "grabRadius", 1f);
        SetPrivateField(controller, "livingEnemyBadgeGrabRadius", 0.25f);

        var enemyObject = new GameObject("EnemyWithBadge");
        enemyObject.transform.position = position;
        enemyObject.AddComponent<Rigidbody2D>();
        var enemyCollider = enemyObject.AddComponent<CircleCollider2D>();
        enemyCollider.radius = 0.05f;
        var enemy = enemyObject.AddComponent<EnemyGrabbable>();
        var heart = enemyObject.AddComponent<RobotHeartNew>();
        heart.ConfigureRole(role, resetStack: true);
        var stateController = enemyObject.AddComponent<RobotStateController>();
        if (state != RobotState.Alive)
            stateController.UpdateState(state);

        var badgeObject = new GameObject("AttachedBadge");
        badgeObject.transform.SetParent(enemyObject.transform, false);
        badgeObject.transform.localPosition = new Vector3(badgeOffset, 0f, 0f);
        badgeObject.AddComponent<Rigidbody2D>();
        var badgeCollider = badgeObject.AddComponent<CircleCollider2D>();
        badgeCollider.radius = 0.05f;
        var badge = badgeObject.AddComponent<SecurityBadgePickup>();

        return (controller, hand, inventory, enemy, badge);
    }

    private static IGrabbable DetectGrabbable(CowboyGrabController controller, Vector3 position, Inventory inventory)
    {
        Physics2D.SyncTransforms();
        MethodInfo method = typeof(CowboyGrabController).GetMethod(
            "DetectGrabbable",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method);

        object detection = method.Invoke(controller, new object[] { position, inventory });
        FieldInfo grabbableField = detection.GetType().GetField("Grabbable", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(grabbableField);
        return (IGrabbable)grabbableField.GetValue(detection);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}
