using System.Collections.Generic;
using System.Reflection;
using CowBoya.Robots;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CollectorRobotInteractionTests {
    private const string CollectorPrefabPath =
        "Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab";

    private readonly List<GameObject> createdObjects = new List<GameObject>();
    private RobotNewPipelineMode previousMode;
    private bool previousDriveGameplayInShadow;

    [SetUp]
    public void SetUp() {
        previousMode = RobotNewPipelineRuntime.Mode;
        previousDriveGameplayInShadow = RobotNewPipelineRuntime.DriveGameplayInShadow;
        RobotNewPipelineRuntime.Mode = RobotNewPipelineMode.NewOnly;
        RobotNewPipelineRuntime.DriveGameplayInShadow = true;
    }

    [TearDown]
    public void TearDown() {
        for (int i = createdObjects.Count - 1; i >= 0; i--) {
            if (createdObjects[i] != null) {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        RobotNewPipelineRuntime.Mode = previousMode;
        RobotNewPipelineRuntime.DriveGameplayInShadow = previousDriveGameplayInShadow;
    }

    [Test]
    public void CollectorPrefab_PlayerCanGrabFromChildCollider() {
        GameObject collector = InstantiateCollector();
        EnemyGrabbable grabbable = collector.GetComponent<EnemyGrabbable>();
        CollectorRobotBodyController body = collector.GetComponent<CollectorRobotBodyController>();
        SimplePuppetBinder binder = collector.GetComponent<SimplePuppetBinder>();

        Assert.IsNotNull(grabbable);
        Assert.IsNotNull(body);
        Assert.IsNotNull(binder);
        Collider2D bodyCollider = GetBodyCollider(collector);

        GameObject player = CreateObject("Collector Grab Test Player");
        CowboyGrabController grabController = player.AddComponent<CowboyGrabController>();
        Transform hand = new GameObject("Grab Hand").transform;
        hand.SetParent(player.transform, false);
        hand.position = bodyCollider.bounds.center;
        SetPrivateField(grabController, "leftHandGrabAnchor", hand);
        SetPrivateField(grabController, "leftHandHoldParent", hand);
        SetPrivateField(grabController, "grabRadius", 0.1f);
        Physics2D.SyncTransforms();

        Assert.IsTrue(grabController.TryGrab(CowboyArmSide.Left));
        Assert.AreSame(grabbable, grabController.GetHeldObject(CowboyArmSide.Left));
        Assert.IsFalse(body.enabled, "Collector flight must pause while the player holds it.");
        Assert.IsFalse(binder.enabled, "The puppet rotation binder must not fight the grab joint.");

        TargetJoint2D[] grabJoints = collector.GetComponentsInChildren<TargetJoint2D>(true);
        Assert.AreEqual(1, grabJoints.Length);
        Assert.IsTrue(grabJoints[0].enabled);

        grabController.Release(CowboyArmSide.Left, 0f);

        Assert.IsFalse(grabController.HasHeldObject(CowboyArmSide.Left));
        Assert.IsTrue(body.enabled);
        Assert.IsTrue(binder.enabled);
        Assert.IsEmpty(collector.GetComponentsInChildren<TargetJoint2D>(true));
    }

    [Test]
    public void CollectorPrefab_PlayerAttackReducesHealth() {
        GameObject collector = InstantiateCollector();
        RobotStateController state = collector.GetComponent<RobotStateController>();
        Collider2D targetCollider = GetBodyCollider(collector);
        AttackHitbox hitbox = CreatePlayerHitbox(5);
        float initialHealth = state.Stats.CurrentHealth;

        hitbox.Activate();
        InvokeTriggerEnter(hitbox, targetCollider);

        Assert.AreEqual(initialHealth - 5f, state.Stats.CurrentHealth);
        Assert.AreEqual(RobotState.Alive, state.CurrentState);
        Assert.IsFalse(hitbox.IsActive);
    }

    [Test]
    public void CollectorPrefab_LethalPlayerAttackStopsAndKillsCollector() {
        GameObject collector = InstantiateCollector();
        RobotStateController state = collector.GetComponent<RobotStateController>();
        RobotMemoryNew memory = collector.GetComponent<RobotMemoryNew>();
        CollectorFlightMotor2D motor = collector.GetComponent<CollectorFlightMotor2D>();
        CollectorRobotBodyController body = collector.GetComponent<CollectorRobotBodyController>();
        SimplePuppetBinder binder = collector.GetComponent<SimplePuppetBinder>();
        EnemyGrabbable grabbable = collector.GetComponent<EnemyGrabbable>();
        Collider2D targetCollider = GetBodyCollider(collector);

        motor.StartFlight(
            () => body.BodyRigidbody.position + Vector2.right,
            new CollectorFlightProfile());
        Assert.IsTrue(motor.IsFlightActive, "The death check needs an active flight command.");

        AttackHitbox hitbox = CreatePlayerHitbox(5);
        int requiredHits = Mathf.CeilToInt(state.Stats.CurrentHealth / hitbox.damage);
        for (int i = 0; i < requiredHits; i++) {
            hitbox.Activate();
            InvokeTriggerEnter(hitbox, targetCollider);
        }

        Assert.AreEqual(0f, state.Stats.CurrentHealth);
        Assert.AreEqual(RobotState.Dead, state.CurrentState);
        Assert.IsTrue(memory.Snapshot.IsDead);
        Assert.IsFalse(motor.IsFlightActive, "A dead Collector must stop applying flight forces.");
        Assert.IsFalse(binder.enabled, "A dead Collector must release puppet rotation control.");
        Assert.IsFalse(grabbable.CanBeGrabbed(null), "Dead enemies follow the existing alive-only grab policy.");
    }

    private GameObject InstantiateCollector() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CollectorPrefabPath);
        Assert.IsNotNull(prefab);

        GameObject collector = Object.Instantiate(prefab);
        createdObjects.Add(collector);
        InvokePrivateMethod(collector.GetComponent<RobotStateController>(), "Awake");
        Physics2D.SyncTransforms();
        return collector;
    }

    private AttackHitbox CreatePlayerHitbox(int damage) {
        GameObject player = CreateObject("Collector Damage Test Player");
        RobotStateController attacker = player.AddComponent<RobotStateController>();
        GameObject hitboxObject = new GameObject("Player Attack Hitbox");
        hitboxObject.transform.SetParent(player.transform, false);
        AttackHitbox hitbox = hitboxObject.AddComponent<AttackHitbox>();
        hitbox.damage = damage;
        SetPrivateField(hitbox, "attacker", attacker);
        return hitbox;
    }

    private GameObject CreateObject(string objectName) {
        GameObject created = new GameObject(objectName);
        createdObjects.Add(created);
        return created;
    }

    private static Collider2D GetBodyCollider(GameObject collector) {
        CollectorRobotBodyController body = collector.GetComponent<CollectorRobotBodyController>();
        Assert.IsNotNull(body);
        Assert.IsNotNull(body.BodyRigidbody);
        Collider2D collider = body.BodyRigidbody.GetComponent<Collider2D>();
        Assert.IsNotNull(collider);
        return collider;
    }

    private static void InvokeTriggerEnter(AttackHitbox hitbox, Collider2D targetCollider) {
        MethodInfo method = typeof(AttackHitbox).GetMethod(
            "OnTriggerEnter2D",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(hitbox, new object[] { targetCollider });
    }

    private static void InvokePrivateMethod(object owner, string methodName) {
        MethodInfo method = owner.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Expected private method '" + methodName + "'.");
        method.Invoke(owner, null);
    }

    private static void SetPrivateField(object owner, string fieldName, object value) {
        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Expected private field '" + fieldName + "'.");
        field.SetValue(owner, value);
    }
}
