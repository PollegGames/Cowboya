using System.Collections.Generic;
using System.Reflection;
using CowBoya.Robots;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class GrabOwnershipContractTests {
    private readonly List<GameObject> createdObjects = new List<GameObject>();

    [TearDown]
    public void TearDown() {
        for (int i = createdObjects.Count - 1; i >= 0; i--) {
            if (createdObjects[i] != null) {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
    }

    [Test]
    public void EnemyGrabbable_GrabAndReleaseEventsFireExactlyOnce() {
        EnemyGrabbable grabbable = CreateEnemyGrabbable(Vector2.zero);
        Transform hand = CreateObject("Enemy grab hand").transform;
        int startedCount = 0;
        int endedCount = 0;
        grabbable.OnGrabStarted += _ => startedCount++;
        grabbable.OnGrabEnded += _ => endedCount++;

        grabbable.OnGrab(hand);
        grabbable.OnGrab(hand);

        Assert.IsTrue(grabbable.IsGrabbed);
        Assert.AreEqual(1, startedCount);
        Assert.AreEqual(0, endedCount);

        grabbable.OnRelease(Vector2.zero);
        grabbable.OnRelease(Vector2.zero);

        Assert.IsFalse(grabbable.IsGrabbed);
        Assert.AreEqual(1, startedCount);
        Assert.AreEqual(1, endedCount);
        Assert.IsNull(grabbable.GetComponent<TargetJoint2D>());
    }

    [Test]
    public void EnemyGrabbable_GrabUsesLimpMassBalancedPhysicsAndRestoresIt() {
        GameObject enemy = CreateObject("Mass balanced grabbable enemy");
        Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
        body.mass = 1.5f;
        body.constraints = RigidbodyConstraints2D.FreezePositionX
            | RigidbodyConstraints2D.FreezeRotation;
        SimplePuppetBinder enabledBinder = enemy.AddComponent<SimplePuppetBinder>();

        GameObject magnetObject = CreateObject("Light robot part");
        magnetObject.transform.SetParent(enemy.transform, false);
        Rigidbody2D magnetBody = magnetObject.AddComponent<Rigidbody2D>();
        magnetBody.mass = 0.35f;
        magnetBody.freezeRotation = true;
        BoxCollider2D magnetCollider = magnetObject.AddComponent<BoxCollider2D>();
        SimplePuppetBinder disabledBinder = magnetObject.AddComponent<SimplePuppetBinder>();
        disabledBinder.enabled = false;

        GameObject kinematicPart = CreateObject("Kinematic robot part");
        kinematicPart.transform.SetParent(enemy.transform, false);
        Rigidbody2D kinematicBody = kinematicPart.AddComponent<Rigidbody2D>();
        kinematicBody.bodyType = RigidbodyType2D.Kinematic;
        kinematicBody.mass = 100f;

        GameObject unsimulatedPart = CreateObject("Unsimulated robot part");
        unsimulatedPart.transform.SetParent(enemy.transform, false);
        Rigidbody2D unsimulatedBody = unsimulatedPart.AddComponent<Rigidbody2D>();
        unsimulatedBody.mass = 100f;
        unsimulatedBody.simulated = false;

        EnemyGrabbable grabbable = enemy.AddComponent<EnemyGrabbable>();
        Transform hand = CreateObject("Limp grab hand").transform;
        grabbable.SetGrabContext(magnetCollider, hand.position);

        grabbable.OnGrab(hand);

        Assert.IsTrue(grabbable.IsGrabbed);
        Assert.IsFalse(enabledBinder.enabled,
            "Every puppet pose driver must pause so the held robot becomes limp.");
        Assert.IsFalse(disabledBinder.enabled,
            "A binder that was already disabled must remain disabled.");
        Assert.AreEqual(
            RigidbodyConstraints2D.FreezePositionX,
            body.constraints,
            "Grab must release rotation without discarding unrelated constraints.");
        Assert.IsFalse(magnetBody.freezeRotation);

        TargetJoint2D grabJoint = magnetBody.GetComponent<TargetJoint2D>();
        Assert.IsNotNull(grabJoint);
        Assert.AreEqual(129.5f, grabJoint.maxForce, 0.001f,
            "Only simulated dynamic puppet mass may contribute to the force cap.");

        grabbable.OnRelease(Vector2.zero);

        Assert.IsFalse(enabledBinder.enabled,
            "The puppet binder must remain off for the release frame.");
        Assert.IsFalse(disabledBinder.enabled);
        Assert.AreEqual(
            RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation,
            body.constraints);
        Assert.IsTrue(magnetBody.freezeRotation);
        Assert.IsNull(magnetBody.GetComponent<TargetJoint2D>());

        AdvanceBinderRestoreFrame(grabbable);

        Assert.IsTrue(enabledBinder.enabled);
        Assert.IsFalse(disabledBinder.enabled);
    }

    [TestCase("Assets/Resources/Prefabs/Robots/Worker/Worker3.prefab")]
    [TestCase("Assets/Resources/Prefabs/Robots/SecurityGuard/SecurityGuard.prefab")]
    [TestCase("Assets/Resources/Prefabs/Robots/Follower/Follower.prefab")]
    [TestCase("Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab")]
    [TestCase("Assets/Resources/Prefabs/Robots/Boss/BossMony.prefab")]
    [TestCase("Assets/Resources/Prefabs/Robots/WorkerSpawner/WorkerSpawner.prefab")]
    [TestCase("Assets/Resources/Prefabs/Robots/DocBot/DocBot.prefab")]
    public void RobotPrefab_GrabPausesEveryPuppetBinder(string prefabPath) {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(prefab, prefabPath);

        GameObject instance = Object.Instantiate(prefab);
        createdObjects.Add(instance);
        EnemyGrabbable grabbable = instance.GetComponent<EnemyGrabbable>();
        Assert.IsNotNull(grabbable, prefabPath);

        SimplePuppetBinder[] binders =
            instance.GetComponentsInChildren<SimplePuppetBinder>(true);
        Assert.IsNotEmpty(binders, prefabPath);
        bool[] initialBinderStates = new bool[binders.Length];
        for (int i = 0; i < binders.Length; i++) {
            initialBinderStates[i] = binders[i].enabled;
        }

        Rigidbody2D[] bodies = instance.GetComponentsInChildren<Rigidbody2D>(true);
        RigidbodyConstraints2D[] initialConstraints =
            new RigidbodyConstraints2D[bodies.Length];
        float totalDynamicMass = 0f;
        for (int i = 0; i < bodies.Length; i++) {
            initialConstraints[i] = bodies[i].constraints;
            if (bodies[i].simulated && bodies[i].bodyType == RigidbodyType2D.Dynamic) {
                totalDynamicMass += bodies[i].mass;
            }
        }

        Behaviour movementController = instance.GetComponent<RobotBodyController>();
        if (movementController == null) {
            movementController = instance.GetComponent<CollectorRobotBodyController>();
        }
        bool movementWasEnabled = movementController != null && movementController.enabled;

        RobotStateController state = instance.GetComponent<RobotStateController>();
        Assert.IsNotNull(state, prefabPath);
        Assert.AreEqual(RobotState.Alive, state.CurrentState, prefabPath);

        Collider2D sourceCollider = FindGrabCollider(instance);
        Assert.IsNotNull(sourceCollider, prefabPath);
        Transform hand = CreateObject($"{instance.name} grab hand").transform;
        hand.position = sourceCollider.bounds.center;
        grabbable.SetGrabContext(sourceCollider, hand.position);

        grabbable.OnGrab(hand);

        Assert.IsTrue(grabbable.IsGrabbed, prefabPath);
        for (int i = 0; i < binders.Length; i++) {
            Assert.IsFalse(binders[i].enabled,
                $"{prefabPath} binder {i} continued driving the held puppet.");
        }
        for (int i = 0; i < bodies.Length; i++) {
            Assert.AreEqual(
                RigidbodyConstraints2D.None,
                bodies[i].constraints & RigidbodyConstraints2D.FreezeRotation,
                $"{prefabPath} body {i} remained rotation-locked while held.");
        }
        if (movementController != null) {
            Assert.IsFalse(movementController.enabled, prefabPath);
        }
        Assert.AreEqual(RobotState.Alive, state.CurrentState,
            "Temporary grab ragdoll must not change the robot's actual state.");

        TargetJoint2D grabJoint =
            sourceCollider.attachedRigidbody.GetComponent<TargetJoint2D>();
        Assert.IsNotNull(grabJoint, prefabPath);
        Assert.Greater(grabJoint.maxForce, 0f, prefabPath);
        float authoredMaxForce = GetPrivateField<float>(grabbable, "maxForce");
        float maximumGrabAcceleration =
            GetPrivateField<float>(grabbable, "maximumGrabAcceleration");
        float expectedMaxForce = Mathf.Min(
            authoredMaxForce,
            totalDynamicMass * maximumGrabAcceleration);
        Assert.AreEqual(expectedMaxForce, grabJoint.maxForce, 0.001f,
            $"{prefabPath} did not apply both authored and whole-rig force limits.");

        grabbable.OnRelease(Vector2.zero);

        for (int i = 0; i < bodies.Length; i++) {
            Assert.AreEqual(initialConstraints[i], bodies[i].constraints,
                $"{prefabPath} body {i} did not restore its authored constraints.");
        }
        if (movementController != null && movementWasEnabled) {
            Assert.IsFalse(movementController.enabled,
                $"{prefabPath} locomotion resumed before its puppet binder.");
        }

        for (int i = 0; i < binders.Length; i++) {
            Assert.IsFalse(binders[i].enabled,
                $"{prefabPath} binder {i} reactivated during the release frame.");
        }

        AdvanceBinderRestoreFrame(grabbable);

        if (movementController != null) {
            Assert.AreEqual(movementWasEnabled, movementController.enabled, prefabPath);
        }
        for (int i = 0; i < binders.Length; i++) {
            Assert.AreEqual(initialBinderStates[i], binders[i].enabled,
                $"{prefabPath} binder {i} did not return to its authored state.");
        }
    }

    [TestCase("Assets/Resources/Prefabs/Robots/Worker/Worker3.prefab", 13, null)]
    [TestCase("Assets/Resources/Prefabs/Robots/SecurityGuard/SecurityGuard.prefab", 13, null)]
    [TestCase("Assets/Resources/Prefabs/Robots/Follower/Follower.prefab", 14, "Badge")]
    [TestCase("Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab", 2, "bone_Magnet")]
    public void RobotPrefab_CanGrabEveryPhysicalBody(
        string prefabPath,
        int expectedBodyCount,
        string requiredBodyName) {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(prefab, prefabPath);

        GameObject instance = Object.Instantiate(prefab);
        createdObjects.Add(instance);
        EnemyGrabbable grabbable = instance.GetComponent<EnemyGrabbable>();
        Assert.IsNotNull(grabbable, prefabPath);
        SetPrivateField(grabbable, "resetIntentOnRelease", false);

        Transform hand = CreateObject($"{instance.name} all-body grab hand").transform;
        HashSet<Rigidbody2D> visitedBodies = new HashSet<Rigidbody2D>();
        HashSet<string> visitedBodyNames = new HashSet<string>();
        Collider2D[] colliders = instance.GetComponentsInChildren<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++) {
            Collider2D sourceCollider = colliders[i];
            Rigidbody2D sourceBody = sourceCollider != null
                ? sourceCollider.attachedRigidbody
                : null;
            if (sourceCollider == null
                || !sourceCollider.enabled
                || !sourceCollider.gameObject.activeInHierarchy
                || sourceCollider.isTrigger
                || sourceBody == null
                || sourceCollider.GetComponentInParent<EnemyGrabbable>() != grabbable
                || !visitedBodies.Add(sourceBody)) {
                continue;
            }

            visitedBodyNames.Add(sourceBody.name);
            hand.position = sourceCollider.bounds.center;
            grabbable.SetGrabContext(sourceCollider, hand.position);
            grabbable.OnGrab(hand);

            Assert.IsTrue(grabbable.IsGrabbed,
                $"{prefabPath} could not grab body '{sourceBody.name}'.");
            TargetJoint2D sourceJoint = sourceBody.GetComponent<TargetJoint2D>();
            Assert.IsNotNull(sourceJoint,
                $"{prefabPath} did not attach its grab joint to '{sourceBody.name}'.");
            Assert.IsTrue(sourceJoint.enabled, prefabPath);

            TargetJoint2D[] joints = instance.GetComponentsInChildren<TargetJoint2D>(true);
            Assert.AreEqual(1, joints.Length,
                $"{prefabPath} created an ambiguous joint while grabbing '{sourceBody.name}'.");
            Assert.AreSame(sourceJoint, joints[0], prefabPath);

            grabbable.OnRelease(Vector2.zero);

            Assert.IsFalse(grabbable.IsGrabbed, prefabPath);
            Assert.IsNull(sourceBody.GetComponent<TargetJoint2D>(),
                $"{prefabPath} left a joint on '{sourceBody.name}' after release.");
        }

        Assert.AreEqual(expectedBodyCount, visitedBodies.Count,
            $"{prefabPath} physical grab coverage changed.");
        if (!string.IsNullOrEmpty(requiredBodyName)) {
            Assert.Contains(requiredBodyName, new List<string>(visitedBodyNames),
                $"{prefabPath} did not exercise its requested edge body.");
        }

        AdvanceBinderRestoreFrame(grabbable);
    }

    [Test]
    public void TryDetachHeldObject_RemovesBothHandsAndExactInventoryReferenceWithoutRelease() {
        CowboyGrabController controller = CreateGrabController(
            Vector2.zero,
            Vector2.zero,
            out Inventory inventory);
        JunkPickup junk = CreateJunk("Transfer junk", Vector2.zero);
        JunkPickup unrelated = CreateJunk("Unrelated junk", Vector2.right * 10f);
        int releasedCount = 0;
        junk.OnReleased += _ => releasedCount++;
        Physics2D.SyncTransforms();

        Assert.IsTrue(controller.TryGrab(CowboyArmSide.Left));
        Assert.IsTrue(controller.TryGrab(CowboyArmSide.Right));
        Assert.AreSame(junk, controller.GetHeldObject(CowboyArmSide.Left));
        Assert.AreSame(junk, controller.GetHeldObject(CowboyArmSide.Right));
        Assert.IsTrue(junk.GetComponent<TargetJoint2D>().enabled);

        inventory.SetItem(PickupType.Cube, junk);
        inventory.SetItem(PickupType.Battery, unrelated);

        Assert.IsTrue(controller.TryDetachHeldObject(junk));

        Assert.IsFalse(controller.HasHeldObject(CowboyArmSide.Left));
        Assert.IsFalse(controller.HasHeldObject(CowboyArmSide.Right));
        Assert.IsFalse(inventory.HasItem(PickupType.Cube));
        Assert.AreSame(unrelated, inventory.GetItem(PickupType.Battery));
        Assert.AreEqual(0, releasedCount);
        Assert.IsFalse(junk.IsHeld);
        Assert.IsNull(junk.CurrentHolder);
        Assert.IsNull(junk.transform.parent);
        Assert.IsFalse(junk.GetComponent<TargetJoint2D>().enabled);
        Assert.IsFalse(controller.TryDetachHeldObject(junk));
    }

    [Test]
    public void JunkGrabLock_IsExclusiveIdempotentAndRecoverableAfterOwnerDestruction() {
        JunkPickup junk = CreateJunk("Lockable junk", Vector2.zero);
        GameObject firstOwner = CreateObject("First junk owner");
        GameObject secondOwner = CreateObject("Second junk owner");

        Assert.IsTrue(junk.CanBeGrabbed(null));
        Assert.IsFalse(junk.TryLockGrab(null));
        Assert.IsTrue(junk.TryLockGrab(firstOwner));
        Assert.IsTrue(junk.TryLockGrab(firstOwner));
        Assert.IsTrue(junk.IsGrabLocked);
        Assert.AreSame(firstOwner, junk.GrabLockOwner);
        Assert.IsFalse(junk.CanBeGrabbed(null));
        Assert.IsFalse(junk.TryLockGrab(secondOwner));
        Assert.IsFalse(junk.UnlockGrab(secondOwner));

        Object.DestroyImmediate(firstOwner);

        Assert.IsFalse(junk.IsGrabLocked);
        Assert.IsTrue(junk.TryLockGrab(secondOwner));
        Assert.IsTrue(junk.UnlockGrab(secondOwner));
        Assert.IsFalse(junk.UnlockGrab(secondOwner));
        Assert.IsTrue(junk.CanBeGrabbed(null));
    }

    [Test]
    public void JunkCurrentHolder_TracksGrabTransferAndRelease() {
        JunkPickup junk = CreateJunk("Holder junk", Vector2.zero);
        Transform firstHand = CreateObject("First holder hand").transform;
        Transform secondHand = CreateObject("Second holder hand").transform;

        junk.OnGrab(firstHand);
        Assert.IsTrue(junk.IsHeld);
        Assert.AreSame(firstHand, junk.CurrentHolder);

        junk.OnGrab(secondHand);
        Assert.AreSame(secondHand, junk.CurrentHolder);

        junk.OnRelease(Vector2.zero);
        Assert.IsFalse(junk.IsHeld);
        Assert.IsNull(junk.CurrentHolder);
    }

    [Test]
    public void TryDetachHeldObject_RejectsItemsWithoutAtomicDetachHook() {
        CowboyGrabController controller = CreateGrabController(
            Vector2.zero,
            Vector2.zero,
            out Inventory inventory);
        CubePickup cube = CreateCube("Unsupported transfer cube", Vector2.zero);
        inventory.SetItem(PickupType.Cube, cube);

        Assert.IsFalse(controller.TryDetachHeldObject(cube));
        Assert.AreSame(cube, inventory.GetItem(PickupType.Cube));
    }

    [Test]
    public void ExistingJunkPrefabs_ResolveAllEightVariantsWithoutPrefabEdits() {
        Assert.AreEqual(9, System.Enum.GetValues(typeof(JunkVariant)).Length);
        Assert.AreEqual(0, (int)JunkVariant.None);

        for (int index = 1; index <= 8; index++) {
            string path = $"Assets/Resources/Prefabs/IntereableObjects/Junk_{index}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path);
            JunkPickup pickup = prefab.GetComponent<JunkPickup>();
            Assert.IsNotNull(pickup, path);
            Assert.AreEqual((JunkVariant)index, pickup.Variant, path);
        }
    }

    private CowboyGrabController CreateGrabController(
        Vector2 leftHandPosition,
        Vector2 rightHandPosition,
        out Inventory inventory) {
        GameObject player = CreateObject("Grab contract player");
        inventory = player.AddComponent<Inventory>();
        CowboyGrabController controller = player.AddComponent<CowboyGrabController>();
        Transform leftHand = CreateObject("Left grab hand").transform;
        leftHand.SetParent(player.transform, false);
        leftHand.position = leftHandPosition;
        Transform rightHand = CreateObject("Right grab hand").transform;
        rightHand.SetParent(player.transform, false);
        rightHand.position = rightHandPosition;

        SetPrivateField(controller, "leftHandGrabAnchor", leftHand);
        SetPrivateField(controller, "leftHandHoldParent", leftHand);
        SetPrivateField(controller, "rightHandGrabAnchor", rightHand);
        SetPrivateField(controller, "rightHandHoldParent", rightHand);
        SetPrivateField(controller, "inventory", inventory);
        SetPrivateField(controller, "grabRadius", 0.25f);
        return controller;
    }

    private EnemyGrabbable CreateEnemyGrabbable(Vector2 position) {
        GameObject enemy = CreateObject("Grabbable enemy");
        enemy.transform.position = position;
        enemy.AddComponent<Rigidbody2D>();
        enemy.AddComponent<BoxCollider2D>();
        return enemy.AddComponent<EnemyGrabbable>();
    }

    private static Collider2D FindGrabCollider(GameObject root) {
        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++) {
            Collider2D collider = colliders[i];
            if (collider != null && !collider.isTrigger && collider.attachedRigidbody != null) {
                return collider;
            }
        }

        return null;
    }

    private static void AdvanceBinderRestoreFrame(EnemyGrabbable grabbable) {
        Assert.AreEqual(
            Time.frameCount + 1,
            GetPrivateField<int>(grabbable, "releaseRestoreFrame"),
            "Release physics must be scheduled for the following frame.");

        InvokeLifecycle(grabbable, "Update");

        Assert.AreEqual(
            Time.frameCount + 1,
            GetPrivateField<int>(grabbable, "releaseRestoreFrame"),
            "A same-frame Update must not restore release physics.");

        SetPrivateField(grabbable, "releaseRestoreFrame", Time.frameCount);
        InvokeLifecycle(grabbable, "Update");
    }

    private JunkPickup CreateJunk(string objectName, Vector2 position) {
        GameObject junkObject = CreateObject(objectName);
        junkObject.transform.position = position;
        junkObject.AddComponent<BoxCollider2D>();
        JunkPickup junk = junkObject.AddComponent<JunkPickup>();
        InvokeLifecycle(junk, "Awake");
        return junk;
    }

    private CubePickup CreateCube(string objectName, Vector2 position) {
        GameObject cubeObject = CreateObject(objectName);
        cubeObject.transform.position = position;
        cubeObject.AddComponent<BoxCollider2D>();
        CubePickup cube = cubeObject.AddComponent<CubePickup>();
        InvokeLifecycle(cube, "Awake");
        return cube;
    }

    private static void InvokeLifecycle(MonoBehaviour behaviour, string methodName) {
        MethodInfo method = behaviour.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Expected lifecycle method '{methodName}'.");
        method.Invoke(behaviour, null);
    }

    private GameObject CreateObject(string objectName) {
        GameObject created = new GameObject(objectName);
        createdObjects.Add(created);
        return created;
    }

    private static void SetPrivateField(object owner, string fieldName, object value) {
        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected field '{fieldName}'.");
        field.SetValue(owner, value);
    }

    private static T GetPrivateField<T>(object owner, string fieldName) {
        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected field '{fieldName}'.");
        return (T)field.GetValue(owner);
    }
}
