using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EnemyPunchAttackTests
{
    [Test]
    public void HandleAttackAccepted_ActivatesCorrectHorizontalHitbox()
    {
        GameObject enemy = new GameObject("Enemy");
        enemy.SetActive(false);

        enemy.AddComponent<EnergyBot>();
        enemy.AddComponent<HealthBot>();
        enemy.AddComponent<RobotStateController>();

        EnemyPunchAttack punchAttack = enemy.AddComponent<EnemyPunchAttack>();
        PunchHitboxEventRelay relay = enemy.AddComponent<PunchHitboxEventRelay>();

        EnemyPunchAttack punchAttackInstance = punchAttack;
        SetPrivateField(punchAttackInstance, "hitboxActiveDuration", 0f);

        AttackHitbox leftHitbox = CreateHitbox(enemy.transform, "LeftHitbox");
        AttackHitbox rightHitbox = CreateHitbox(enemy.transform, "RightHitbox");

        SetRelayField(relay, "leftArmHitbox", leftHitbox);
        SetRelayField(relay, "rightArmHitbox", rightHitbox);

        enemy.transform.localScale = Vector3.one;
        enemy.SetActive(true);

        punchAttackInstance.HandleAttackAccepted(new AttackRequest(Vector2.zero, AttackSector.Right, 0f));

        Assert.IsTrue(rightHitbox.IsActive, "Right hitbox should be armed when attacking to the right while facing right.");
        Assert.IsFalse(leftHitbox.IsActive, "Left hitbox should remain inactive when attacking to the right.");

        Object.DestroyImmediate(enemy);
    }

    [Test]
    public void HandleAttackAccepted_RespectsFacingDirection()
    {
        GameObject enemy = new GameObject("EnemyFacingLeft");
        enemy.SetActive(false);

        enemy.AddComponent<EnergyBot>();
        enemy.AddComponent<HealthBot>();
        enemy.AddComponent<RobotStateController>();

        EnemyPunchAttack punchAttack = enemy.AddComponent<EnemyPunchAttack>();
        PunchHitboxEventRelay relay = enemy.AddComponent<PunchHitboxEventRelay>();

        SetPrivateField(punchAttack, "hitboxActiveDuration", 0f);

        AttackHitbox leftHitbox = CreateHitbox(enemy.transform, "LeftHitbox");
        AttackHitbox rightHitbox = CreateHitbox(enemy.transform, "RightHitbox");

        SetRelayField(relay, "leftArmHitbox", leftHitbox);
        SetRelayField(relay, "rightArmHitbox", rightHitbox);

        enemy.transform.localScale = new Vector3(-1f, 1f, 1f);
        enemy.SetActive(true);

        punchAttack.HandleAttackAccepted(new AttackRequest(Vector2.zero, AttackSector.Right, 0f));

        Assert.IsTrue(leftHitbox.IsActive, "Left hitbox should be armed when facing left and attacking right.");
        Assert.IsFalse(rightHitbox.IsActive, "Right hitbox should remain inactive when facing left and attacking right.");

        Object.DestroyImmediate(enemy);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    private static void SetRelayField(PunchHitboxEventRelay relay, string fieldName, AttackHitbox hitbox)
    {
        FieldInfo field = typeof(PunchHitboxEventRelay).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(relay, hitbox);
    }

    private static AttackHitbox CreateHitbox(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent);
        return go.AddComponent<AttackHitbox>();
    }
}
