using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class WorkerCollectorCarryAttachmentTests {
    private Scene testScene;
    private PhysicsScene2D physicsScene;

    [SetUp]
    public void SetUp() {
        // Preview scenes provide an isolated physics world in Edit Mode, where
        // SceneManager.CreateScene with LocalPhysicsMode is not supported.
        testScene = EditorSceneManager.NewPreviewScene();
        testScene.name = "WorkerCollectorCarryAttachmentTests_" + Guid.NewGuid();
        physicsScene = testScene.GetPhysicsScene2D();
        Assert.IsTrue(physicsScene.IsValid());
        Assert.AreNotEqual(Physics2D.defaultPhysicsScene, physicsScene,
            "Manual simulation must never advance physics in the user's open scene.");
    }

    [TearDown]
    public void TearDown() {
        if (testScene.IsValid())
            EditorSceneManager.ClosePreviewScene(testScene);
    }

    [Test]
    public void CarriedCube_FollowsMovingAndRotatingHandWithoutSpringDrift() {
        WhiteCubeCargo cargo = CreateCargo();
        Rigidbody2D body = cargo.GetComponent<Rigidbody2D>();
        TargetJoint2D joint = cargo.GetComponent<TargetJoint2D>();
        Transform worker = CreateObject("Worker").transform;
        Transform hand = CreateObject("Hand").transform;
        hand.SetParent(worker);
        hand.localPosition = new Vector3(0.75f, 0.4f, 0f);
        body.linearVelocity = new Vector2(-8f, 4f);
        body.angularVelocity = 90f;
        Assert.IsTrue(cargo.TryClaim(worker, out WhiteCubeClaim claim));

        Assert.IsTrue(cargo.TryBeginCarry(claim, hand));

        for (int i = 0; i < 60; i++) {
            worker.position = new Vector3(i * 0.1f, Mathf.Sin(i * 0.3f) * 0.1f, 0f);
            worker.rotation = Quaternion.Euler(0f, 0f, i * 2f);
            hand.localPosition = new Vector3(0.75f, 0.4f + Mathf.Sin(i * 0.5f) * 0.15f, 0f);
            cargo.Pickup.OnAttract(new Vector2(-100f, -100f));
            InvokePrivate(cargo.Pickup, "FixedUpdate");
            Physics2D.SyncTransforms();
            Assert.IsTrue(physicsScene.Simulate(0.02f));

            Assert.That(Vector3.Distance(cargo.transform.position, hand.position), Is.LessThan(0.001f),
                "Only the carrying hand should determine the cube position, including after physics.");
            Assert.AreSame(hand, cargo.transform.parent);
            Assert.IsFalse(joint.enabled);
            Assert.AreEqual(RigidbodyType2D.Kinematic, body.bodyType);
            Assert.AreEqual(RigidbodyInterpolation2D.None, body.interpolation);
            Assert.IsTrue(body.simulated);
            Assert.AreSame(cargo.GetComponent<Collider2D>(), physicsScene.OverlapPoint(hand.position),
                "Carried cargo must remain visible to player grab queries.");
        }
    }

    [TestCase(RigidbodyInterpolation2D.None)]
    [TestCase(RigidbodyInterpolation2D.Interpolate)]
    [TestCase(RigidbodyInterpolation2D.Extrapolate)]
    public void Release_RestoresLoosePhysicsAndOriginalInterpolation(RigidbodyInterpolation2D interpolation) {
        WhiteCubeCargo cargo = CreateCargo(interpolation);
        Transform hand = CreateObject("Hand").transform;
        Rigidbody2D body = cargo.GetComponent<Rigidbody2D>();
        Assert.IsTrue(cargo.TryClaim(hand, out WhiteCubeClaim claim));
        Assert.IsTrue(cargo.TryBeginCarry(claim, hand));

        cargo.Pickup.OnRelease(Vector2.zero);
        Assert.IsTrue(cargo.ReleaseClaim(claim));

        Assert.IsNull(cargo.transform.parent);
        Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
        Assert.AreEqual(interpolation, body.interpolation);
        Assert.IsTrue(body.simulated);
        Assert.IsFalse(cargo.GetComponent<TargetJoint2D>().enabled);
        Assert.IsTrue(cargo.IsAvailable);
    }

    [Test]
    public void PlayerTakesCarriedCube_RestoresSpringAndInvalidatesWorkerClaim() {
        WhiteCubeCargo cargo = CreateCargo(RigidbodyInterpolation2D.Interpolate);
        Transform hand = CreateObject("WorkerHand").transform;
        Transform playerHand = CreateObject("PlayerHand").transform;
        playerHand.position = new Vector3(3f, 2f, 0f);
        Rigidbody2D body = cargo.GetComponent<Rigidbody2D>();
        TargetJoint2D joint = cargo.GetComponent<TargetJoint2D>();
        Assert.IsTrue(cargo.TryClaim(hand, out WhiteCubeClaim claim));
        Assert.IsTrue(cargo.TryBeginCarry(claim, hand));

        cargo.Pickup.OnGrab(playerHand);
        playerHand.position += Vector3.right;
        InvokePrivate(cargo.Pickup, "FixedUpdate");

        Assert.IsFalse(cargo.IsClaimValid(claim));
        Assert.AreEqual(WhiteCubeCargoState.HeldExternally, cargo.State);
        Assert.AreSame(playerHand, cargo.transform.parent);
        Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
        Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, body.interpolation);
        Assert.IsTrue(body.simulated);
        Assert.IsTrue(joint.enabled);
        Assert.AreEqual((Vector2)playerHand.position, joint.target);
    }

    [Test]
    public void GarageStoresCarriedCube_DetachesFromHandAndKeepsSlotOwnership() {
        WhiteCubeCargo cargo = CreateCargo(RigidbodyInterpolation2D.Interpolate);
        Transform hand = CreateObject("Hand").transform;
        Transform slot = CreateObject("GarageSlot").transform;
        slot.position = new Vector3(8f, 3f, 0f);
        Rigidbody2D body = cargo.GetComponent<Rigidbody2D>();
        Assert.IsTrue(cargo.TryClaim(hand, out WhiteCubeClaim claim));
        Assert.IsTrue(cargo.TryBeginCarry(claim, hand));

        Assert.IsTrue(cargo.TryStore(claim, slot));
        hand.position += Vector3.left * 10f;
        Physics2D.SyncTransforms();
        Assert.IsTrue(physicsScene.Simulate(0.02f));

        Assert.AreSame(slot, cargo.transform.parent);
        Assert.That(Vector3.Distance(slot.position, cargo.transform.position), Is.LessThan(0.001f));
        Assert.AreEqual(WhiteCubeCargoState.Stored, cargo.State);
        Assert.IsFalse(cargo.IsClaimValid(claim));
        Assert.AreEqual(RigidbodyType2D.Kinematic, body.bodyType);
        Assert.IsTrue(body.simulated);
        Assert.IsFalse(cargo.GetComponent<TargetJoint2D>().enabled);

        cargo.Pickup.OnGrab(hand);

        Assert.AreEqual(RigidbodyType2D.Dynamic, body.bodyType);
        Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, body.interpolation);
        Assert.IsTrue(cargo.GetComponent<TargetJoint2D>().enabled);
        Assert.AreEqual(WhiteCubeCargoState.HeldExternally, cargo.State);
    }

    [Test]
    public void ConveyorGrabListenerRestoresDynamicPhysics_CarrierStillSecuresCubeAfterNotification() {
        WhiteCubeCargo cargo = CreateCargo(RigidbodyInterpolation2D.Interpolate);
        Transform hand = CreateObject("Hand").transform;
        Rigidbody2D body = cargo.GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        bool conveyorDetached = false;
        cargo.Pickup.OnGrabbed += grabbed => {
            conveyorDetached = true;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.linearVelocity = new Vector2(5f, 3f);
            body.angularVelocity = 20f;
        };
        Assert.IsTrue(cargo.TryClaim(hand, out WhiteCubeClaim claim));

        Assert.IsTrue(cargo.TryBeginCarry(claim, hand));

        Assert.IsTrue(conveyorDetached);
        Assert.AreEqual(RigidbodyType2D.Kinematic, body.bodyType);
        Assert.AreEqual(Vector2.zero, body.linearVelocity);
        Assert.AreEqual(0f, body.angularVelocity);
        Assert.AreEqual(RigidbodyInterpolation2D.None, body.interpolation);
        Assert.IsFalse(cargo.GetComponent<TargetJoint2D>().enabled);
        Assert.AreEqual(Vector3.zero, cargo.transform.localPosition);
    }

    [Test]
    public void AttractionBeforeCarrying_DoesNotPullAtOldTargetWhenPlayerTakesCube() {
        WhiteCubeCargo cargo = CreateCargo();
        Transform firstHand = CreateObject("FirstHand").transform;
        Transform carryHand = CreateObject("CarryHand").transform;
        Transform playerHand = CreateObject("PlayerHand").transform;
        playerHand.position = new Vector3(3f, 4f, 0f);
        cargo.Pickup.OnGrab(firstHand);
        cargo.Pickup.OnAttract(new Vector2(-20f, -30f));

        cargo.Pickup.AttachToCarrier(carryHand);
        cargo.Pickup.OnAttract(new Vector2(-40f, -50f));
        cargo.Pickup.OnGrab(playerHand);
        playerHand.position += Vector3.right;
        InvokePrivate(cargo.Pickup, "FixedUpdate");

        Assert.AreEqual((Vector2)playerHand.position, cargo.GetComponent<TargetJoint2D>().target);
    }

    [Test]
    public void GrabListenerReleasesCube_CarrierDoesNotReattachIt() {
        WhiteCubeCargo cargo = CreateCargo(RigidbodyInterpolation2D.Interpolate);
        Transform hand = CreateObject("Hand").transform;
        cargo.Pickup.OnGrabbed += grabbed => grabbed.OnRelease(Vector2.zero);
        Assert.IsTrue(cargo.TryClaim(hand, out WhiteCubeClaim claim));

        Assert.IsFalse(cargo.TryBeginCarry(claim, hand));

        Assert.IsNull(cargo.transform.parent);
        Assert.IsFalse(cargo.IsClaimValid(claim));
        Assert.AreEqual(RigidbodyType2D.Dynamic, cargo.GetComponent<Rigidbody2D>().bodyType);
        Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, cargo.GetComponent<Rigidbody2D>().interpolation);
        Assert.IsFalse(cargo.GetComponent<TargetJoint2D>().enabled);
    }

    [Test]
    public void GrabListenerTransfersCube_CarrierDoesNotOverrideNewHolder() {
        WhiteCubeCargo cargo = CreateCargo(RigidbodyInterpolation2D.Interpolate);
        Transform hand = CreateObject("Hand").transform;
        Transform playerHand = CreateObject("PlayerHand").transform;
        bool transferred = false;
        cargo.Pickup.OnGrabbed += grabbed => {
            if (transferred)
                return;
            transferred = true;
            grabbed.OnGrab(playerHand);
        };
        Assert.IsTrue(cargo.TryClaim(hand, out WhiteCubeClaim claim));

        Assert.IsFalse(cargo.TryBeginCarry(claim, hand));

        Assert.AreSame(playerHand, cargo.transform.parent);
        Assert.IsFalse(cargo.IsClaimValid(claim));
        Assert.AreEqual(WhiteCubeCargoState.HeldExternally, cargo.State);
        Assert.AreEqual(RigidbodyType2D.Dynamic, cargo.GetComponent<Rigidbody2D>().bodyType);
        Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, cargo.GetComponent<Rigidbody2D>().interpolation);
        Assert.IsTrue(cargo.GetComponent<TargetJoint2D>().enabled);
    }

    private WhiteCubeCargo CreateCargo(RigidbodyInterpolation2D interpolation = RigidbodyInterpolation2D.None) {
        GameObject cube = CreateObject("Cargo");
        Rigidbody2D body = cube.AddComponent<Rigidbody2D>();
        body.interpolation = interpolation;
        cube.AddComponent<TargetJoint2D>();
        cube.AddComponent<BoxCollider2D>();
        CubePickup pickup = cube.AddComponent<CubePickup>();
        InvokePrivate(pickup, "Awake");
        return cube.AddComponent<WhiteCubeCargo>();
    }

    private GameObject CreateObject(string name) {
        GameObject created = new GameObject(name);
        SceneManager.MoveGameObjectToScene(created, testScene);
        return created;
    }

    private static void InvokePrivate(object owner, string methodName) {
        MethodInfo method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(owner, null);
    }
}
