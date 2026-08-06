using System.Collections.Generic;
using System.Reflection;
using CowBoya.Robots;
using NUnit.Framework;
using UnityEngine;

public class CollectorRobotPoolLifecycleTests {
    private readonly List<GameObject> createdObjects = new();
    private RobotNewPipelineMode previousMode;
    private bool previousDriveInShadow;

    [SetUp]
    public void SetUp() {
        previousMode = RobotNewPipelineRuntime.Mode;
        previousDriveInShadow = RobotNewPipelineRuntime.DriveGameplayInShadow;
        RobotNewPipelineRuntime.Mode = RobotNewPipelineMode.NewOnly;
        RobotNewPipelineRuntime.DriveGameplayInShadow = true;
    }

    [TearDown]
    public void TearDown() {
        RobotNewPipelineRuntime.Mode = previousMode;
        RobotNewPipelineRuntime.DriveGameplayInShadow = previousDriveInShadow;
        for (int i = createdObjects.Count - 1; i >= 0; i--) {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }
        createdObjects.Clear();
        ResetObjectPoolSingletonState();
    }

    [Test]
    public void ObjectPoolReuse_ClearsMissionClaimPhysicsAndIntentBeforeNextAcquire() {
        SpawnRobotCollectorController machine = CreateMachine();
        DeadRobotCollectable target = CreateCollectableTarget();
        Assert.IsTrue(target.TryClaim(1, machine, out CollectorTargetClaim claim));
        CollectorMissionAssignment assignment = new CollectorMissionAssignment(1, machine, target, claim);

        GameObject collectorPrefab = CreateCollectorPrefab();
        GameObject poolObject = CreateObject("Collector Test Pool");
        ObjectPool pool = poolObject.AddComponent<ObjectPool>();
        GameObject collector = pool.Get(collectorPrefab, null);
        createdObjects.Add(collector);

        RobotBrainNew brain = collector.GetComponent<RobotBrainNew>();
        RobotHeartNew heart = collector.GetComponent<RobotHeartNew>();
        RobotMemoryNew memory = collector.GetComponent<RobotMemoryNew>();
        RobotStateController state = collector.GetComponent<RobotStateController>();
        CollectorRobotBodyController body = collector.GetComponent<CollectorRobotBodyController>();
        CollectorPoolLifecycle lifecycle = collector.GetComponent<CollectorPoolLifecycle>();
        InvokePrivate(memory, "Awake");
        InvokePrivate(brain, "Awake");
        InvokePrivate(heart, "Awake");
        Assert.IsTrue(brain.OnCollectorMissionAssigned(assignment));

        collector.SetActive(true);
        InvokePrivate(heart, "OnEnable");
        InvokePrivate(brain, "OnEnable");
        Assert.AreEqual(RobotTaskType.CollectorLaunch, heart.CurrentTask.Type);
        Assert.AreSame(assignment, body.CurrentAssignment);
        RobotStats firstStats = state.Stats;
        int previousCommandToken = body.CurrentCommandToken;
        body.BodyRigidbody.linearVelocity = new Vector2(3f, -2f);
        body.MagnetRigidbody.angularVelocity = 4f;

        lifecycle.PrepareForPoolRelease("test_release");
        InvokePrivate(heart, "OnDisable");
        InvokePrivate(brain, "OnDisable");
        collector.SetActive(false);
        pool.Release(collector);

        Assert.IsFalse(target.IsClaimValid(claim));
        Assert.IsNull(memory.Snapshot.Collector.Assignment);
        Assert.IsNull(heart.CurrentTask);
        Assert.IsNull(body.CurrentAssignment);
        Assert.AreEqual(Vector2.zero, body.BodyRigidbody.linearVelocity);
        Assert.AreEqual(0f, body.MagnetRigidbody.angularVelocity);

        GameObject reused = pool.Get(collectorPrefab, null);
        Assert.AreSame(collector, reused);
        Assert.AreEqual(RobotRole.Collector, heart.Role);
        Assert.AreEqual(RobotTaskType.CollectorStandby, heart.CurrentTask.Type);
        Assert.IsNotNull(state.Stats);
        Assert.AreNotSame(firstStats, state.Stats);
        Assert.Greater(body.CurrentCommandToken, previousCommandToken);
        Assert.IsTrue(target.TryClaim(2, machine, out CollectorTargetClaim secondClaim));
        target.ReleaseClaim(secondClaim);
    }

    private SpawnRobotCollectorController CreateMachine() {
        GameObject root = CreateObject("Collector Machine");
        CreateChild(root.transform, "Spawn Top");
        CreateChild(root.transform, "Spawn Bottom");
        return root.AddComponent<SpawnRobotCollectorController>();
    }

    private DeadRobotCollectable CreateCollectableTarget() {
        GameObject root = CreateObject("Dead Target");
        RobotStateController state = root.AddComponent<RobotStateController>();
        GameObject part = CreateChild(root.transform, "Dead Part").gameObject;
        part.AddComponent<Rigidbody2D>();
        part.AddComponent<BoxCollider2D>();
        state.SetInitialDeadState();
        return DeadRobotCollectable.EnsureFor(state);
    }

    private GameObject CreateCollectorPrefab() {
        GameObject root = CreateObject("Collector Prefab");
        GameObject bodyObject = CreateChild(root.transform, "bone_Body").gameObject;
        Rigidbody2D bodyRigidbody = bodyObject.AddComponent<Rigidbody2D>();
        bodyObject.AddComponent<BoxCollider2D>();
        GameObject magnetObject = CreateChild(root.transform, "bone_Magnet").gameObject;
        Rigidbody2D magnetRigidbody = magnetObject.AddComponent<Rigidbody2D>();
        magnetObject.AddComponent<BoxCollider2D>();
        HingeJoint2D hinge = magnetObject.AddComponent<HingeJoint2D>();
        hinge.connectedBody = bodyRigidbody;
        hinge.useLimits = true;
        hinge.limits = new JointAngleLimits2D { min = -90f, max = 90f };

        Transform masterRoot = CreateChild(bodyObject.transform, "Master");
        Transform masterBody = CreateChild(masterRoot, "bone_Body");
        Transform masterMagnet = CreateChild(masterRoot, "bone_Magnet");
        Transform propeller = CreateChild(bodyObject.transform, "PropellerPivot");
        SimplePuppetBinder binder = root.AddComponent<SimplePuppetBinder>();
        binder.MasterRoot = masterRoot;
        binder.PuppetRoot = root.transform;
        binder.Pairs = new List<SimplePuppetBinder.BonePair> {
            new SimplePuppetBinder.BonePair {
                Master = masterBody,
                Puppet = bodyObject.transform,
                PuppetBody2D = bodyRigidbody
            },
            new SimplePuppetBinder.BonePair {
                Master = masterMagnet,
                Puppet = magnetObject.transform,
                PuppetBody2D = magnetRigidbody
            }
        };

        RobotMemoryNew memory = root.AddComponent<RobotMemoryNew>();
        RobotHeartNew heart = root.AddComponent<RobotHeartNew>();
        RobotBrainNew brain = root.AddComponent<RobotBrainNew>();
        root.AddComponent<HealthBot>();
        JointBreaker breaker = root.AddComponent<JointBreaker>();
        RobotStateController state = root.AddComponent<RobotStateController>();
        CollectorFlightMotor2D motor = root.AddComponent<CollectorFlightMotor2D>();
        CollectorObstacleSensor2D sensor = root.AddComponent<CollectorObstacleSensor2D>();
        CollectorMagnetController2D magnet = root.AddComponent<CollectorMagnetController2D>();
        CollectorFlightVisuals visuals = root.AddComponent<CollectorFlightVisuals>();
        CollectorRobotBodyController collectorBody = root.AddComponent<CollectorRobotBodyController>();
        CollectorRobotObservationBridge bridge = root.AddComponent<CollectorRobotObservationBridge>();
        CollectorPoolLifecycle lifecycle = root.AddComponent<CollectorPoolLifecycle>();

        heart.ConfigureRole(RobotRole.Collector, resetStack: true);
        state.Stats = new EnemyRobotFactory().CreateRobot();
        motor.ConfigureReferences(bodyRigidbody, magnetRigidbody, sensor);
        sensor.ConfigureReferences(root.transform);
        magnet.ConfigureReferences(bodyRigidbody, magnetRigidbody);
        visuals.ConfigureReferences(propeller, motor);
        collectorBody.ConfigureReferences(bodyRigidbody, magnetRigidbody, masterMagnet,
            hinge, binder, motor, sensor, magnet, visuals);
        bridge.ConfigureReferences(collectorBody, brain);
        lifecycle.ConfigureReferences(memory, brain, heart, state, breaker, binder,
            collectorBody, magnet, visuals, bridge);
        root.SetActive(false);
        return root;
    }

    private GameObject CreateObject(string objectName) {
        GameObject created = new GameObject(objectName);
        createdObjects.Add(created);
        return created;
    }

    private Transform CreateChild(Transform parent, string objectName) {
        GameObject child = CreateObject(objectName);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static void ResetObjectPoolSingletonState() {
        System.Type singletonType = typeof(ObjectPool).BaseType;
        singletonType?.GetField("isShuttingDown", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, false);
        singletonType?.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, null);
        singletonType?.GetField("objectLock", BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, null);
    }

    private static void InvokePrivate(object owner, string methodName) {
        MethodInfo method = owner.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Expected private method '" + methodName + "'.");
        method.Invoke(owner, null);
    }
}
