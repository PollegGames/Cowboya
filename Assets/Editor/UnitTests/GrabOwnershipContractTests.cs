using System.Collections.Generic;
using System.Reflection;
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
}
