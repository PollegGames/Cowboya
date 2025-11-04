using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PunchHitboxEventRelayTests
{
    [Test]
    public void ForceDeactivatesHitboxesWhenAttackAborts()
    {
        var root = new GameObject("Robot");
        var attackController = root.AddComponent<AttackRequestController>();
        var relay = root.AddComponent<PunchHitboxEventRelay>();

        var hitboxObject = new GameObject("LeftHitbox");
        hitboxObject.transform.SetParent(root.transform);
        var leftHitbox = hitboxObject.AddComponent<AttackHitbox>();

        SetPrivateField(relay, "leftArmHitbox", leftHitbox);

        leftHitbox.Activate();

        LogAssert.Expect(LogType.Warning, "[PunchHitboxEventRelay] Hitbox 'LeftHitbox' remained active during forced deactivation.");

        attackController.AbortActiveAttack();

        Assert.IsFalse(leftHitbox.IsActive, "Hitbox should be inactive after abort.");

        Object.DestroyImmediate(root);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType()}.");
        field.SetValue(target, value);
    }
}
