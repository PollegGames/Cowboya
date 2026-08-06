using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DeadRobotCollectableTests
{
    private readonly List<GameObject> createdObjects = new();

    [Test]
    public void FollowerRequiredParts_ExcludeAndPreserveTagOnlySecurityBadge()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Robots/Follower/Follower.prefab");
        Assert.IsNotNull(prefab);
        GameObject corpse = Object.Instantiate(prefab);
        createdObjects.Add(corpse);
        RobotStateController state = corpse.GetComponent<RobotStateController>();
        Assert.IsNotNull(state);
        state.SetInitialDeadState();
        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(state);
        GameObject ownerObject = CreateObject("Collector claim owner");
        SpawnRobotCollectorController owner =
            ownerObject.AddComponent<SpawnRobotCollectorController>();
        Assert.IsTrue(target.TryClaim(77, owner, out CollectorTargetClaim claim));

        IReadOnlyList<Rigidbody2D> parts = target.GetRequiredParts(claim);
        Transform taggedBadge = null;
        Transform[] descendants = corpse.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i] != null && descendants[i].gameObject.tag == "BadgeSecurity")
            {
                taggedBadge = descendants[i];
                break;
            }
        }

        Assert.IsNotNull(taggedBadge);
        Assert.IsNull(taggedBadge.GetComponent<SecurityBadgePickup>());
        Assert.AreEqual(13, parts.Count);
        for (int i = 0; i < parts.Count; i++)
        {
            Rigidbody2D part = parts[i];
            Assert.IsNotNull(part);
            Assert.IsTrue(part.gameObject.activeInHierarchy);
            Assert.AreNotSame(taggedBadge, part.transform);
        }

        createdObjects.Add(taggedBadge.gameObject);
        Assert.IsTrue(target.CompleteCollection(claim));
        Assert.IsNull(taggedBadge.parent);
        Joint2D badgeJoint = taggedBadge.GetComponent<Joint2D>();
        Assert.IsTrue(badgeJoint == null || !badgeJoint.enabled);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void TryClaim_IsAtomicAndReleaseCreatesANewerOpaqueClaim()
    {
        DeadRobotCollectable target = CreateDeadTarget(2, out _);
        GameObject firstOwner = CreateObject("First machine");
        GameObject secondOwner = CreateObject("Second machine");

        Assert.IsTrue(target.TryClaim(1, firstOwner, out CollectorTargetClaim firstClaim));
        Assert.IsFalse(target.TryClaim(2, secondOwner, out _));
        Assert.IsTrue(target.IsClaimValid(firstClaim));

        target.ReleaseClaim(firstClaim);

        Assert.IsFalse(target.IsClaimValid(firstClaim));
        Assert.IsTrue(target.TryClaim(2, secondOwner, out CollectorTargetClaim secondClaim));
        Assert.AreEqual(firstClaim.TargetInstanceId, secondClaim.TargetInstanceId);
        Assert.AreEqual(firstClaim.TargetGeneration, secondClaim.TargetGeneration);
        Assert.Greater(secondClaim.ClaimVersion, firstClaim.ClaimVersion);
    }

    [Test]
    public void DestroyedOwner_CannotKeepCorpseLocked()
    {
        DeadRobotCollectable target = CreateDeadTarget(1, out _);
        GameObject vanishedOwner = CreateObject("Vanished machine");
        GameObject nextOwner = CreateObject("Next machine");
        Assert.IsTrue(target.TryClaim(1, vanishedOwner, out CollectorTargetClaim oldClaim));

        createdObjects.Remove(vanishedOwner);
        Object.DestroyImmediate(vanishedOwner);

        Assert.IsFalse(target.IsClaimValid(oldClaim));
        Assert.IsTrue(target.TryClaim(2, nextOwner, out CollectorTargetClaim nextClaim));
        Assert.IsTrue(target.IsClaimValid(nextClaim));
    }

    [Test]
    public void DisabledBehaviourOwner_CannotKeepCorpseLocked()
    {
        DeadRobotCollectable target = CreateDeadTarget(1, out _);
        GameObject firstMachineObject = CreateObject("Disabled machine");
        SpawnRobotCollectorController firstMachine =
            firstMachineObject.AddComponent<SpawnRobotCollectorController>();
        GameObject nextMachineObject = CreateObject("Next machine");
        SpawnRobotCollectorController nextMachine =
            nextMachineObject.AddComponent<SpawnRobotCollectorController>();
        Assert.IsTrue(target.TryClaim(1, firstMachine, out CollectorTargetClaim oldClaim));

        firstMachine.enabled = false;

        Assert.IsFalse(target.IsClaimValid(oldClaim));
        Assert.IsTrue(target.TryClaim(2, nextMachine, out CollectorTargetClaim nextClaim));
        Assert.IsTrue(target.IsClaimValid(nextClaim));
    }

    [Test]
    public void NewDeadLifecycle_RejectsOldGeneration()
    {
        DeadRobotCollectable target = CreateDeadTarget(1, out RobotStateController state);
        GameObject owner = CreateObject("Machine");
        Assert.IsTrue(target.TryClaim(1, owner, out CollectorTargetClaim firstClaim));

        state.UpdateState(RobotState.Alive);
        state.SetInitialDeadState();

        Assert.IsFalse(target.IsClaimValid(firstClaim));
        Assert.IsTrue(target.TryClaim(2, owner, out CollectorTargetClaim secondClaim));
        Assert.Greater(secondClaim.TargetGeneration, firstClaim.TargetGeneration);
    }

    [Test]
    public void RequiredParts_ExcludeLootTriggersAndNonDynamicBodies()
    {
        GameObject root = CreateObject("Dead robot");
        RobotStateController state = root.AddComponent<RobotStateController>();
        AddPhysicalPart(root.transform, "Required", Vector2.zero);

        Rigidbody2D triggerBody = AddBody(root.transform, "Trigger sensor", Vector2.right);
        triggerBody.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;

        Rigidbody2D kinematicBody = AddBody(root.transform, "Kinematic helper", Vector2.left);
        kinematicBody.bodyType = RigidbodyType2D.Kinematic;
        kinematicBody.gameObject.AddComponent<BoxCollider2D>();

        GameObject badgeObject = CreateObject("Security badge");
        badgeObject.transform.SetParent(root.transform);
        badgeObject.AddComponent<BoxCollider2D>();
        badgeObject.AddComponent<SecurityBadgePickup>();

        state.SetInitialDeadState();
        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(state);
        GameObject owner = CreateObject("Machine");

        Assert.IsTrue(target.TryClaim(1, owner, out CollectorTargetClaim claim));
        Assert.AreEqual(1, target.GetRequiredParts(claim).Count);
        Assert.AreEqual("Required", target.GetRequiredParts(claim)[0].name);
    }

    [Test]
    public void ZeroEligibleParts_CannotBeClaimedOrBecomeVacuouslyCollected()
    {
        GameObject root = CreateObject("Empty dead robot");
        RobotStateController state = root.AddComponent<RobotStateController>();
        state.SetInitialDeadState();
        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(state);

        Assert.IsFalse(target.TryClaim(1, CreateObject("Machine"), out _));
        Assert.IsFalse(target.AreAllRequiredPartsInside(
            CreateObject("Intake").AddComponent<BoxCollider2D>(),
            default));
    }

    [Test]
    public void LiveCenterAndIntakeFollowScatteredPhysicalParts()
    {
        DeadRobotCollectable target = CreateDeadTarget(2, out _);
        IReadOnlyList<Rigidbody2D> unclaimedParts = GetChildBodies(target);
        unclaimedParts[0].position = new Vector2(-2f, 1f);
        unclaimedParts[1].position = new Vector2(4f, 3f);
        GameObject owner = CreateObject("Machine");
        Assert.IsTrue(target.TryClaim(1, owner, out CollectorTargetClaim claim));

        Assert.That(target.GetLiveCollectionCenter(claim).x, Is.EqualTo(1f).Within(0.001f));
        Assert.That(target.GetLiveCollectionCenter(claim).y, Is.EqualTo(2f).Within(0.001f));

        BoxCollider2D intake = CreateObject("Intake").AddComponent<BoxCollider2D>();
        intake.size = new Vector2(10f, 10f);
        Physics2D.SyncTransforms();
        Assert.IsTrue(target.AreAllRequiredPartsInside(intake, claim));

        unclaimedParts[1].position = new Vector2(20f, 20f);
        Physics2D.SyncTransforms();
        Assert.IsFalse(target.AreAllRequiredPartsInside(intake, claim));
    }

    [Test]
    public void IntakeMarginAcceptsCarriedRackButRejectsPartsLeftInRoom()
    {
        DeadRobotCollectable target = CreateDeadTarget(2, out _);
        IReadOnlyList<Rigidbody2D> parts = GetChildBodies(target);
        GameObject owner = CreateObject("Machine");
        Assert.IsTrue(target.TryClaim(1, owner, out CollectorTargetClaim claim));
        BoxCollider2D intake = CreateObject("Intake").AddComponent<BoxCollider2D>();
        intake.size = new Vector2(2f, 2f);
        parts[0].position = Vector2.zero;
        parts[1].position = new Vector2(2.25f, 0f);
        Physics2D.SyncTransforms();

        Assert.IsFalse(target.AreAllRequiredPartsInside(intake, claim));
        Assert.IsTrue(target.AreAllRequiredPartsWithinIntake(intake, claim, 1.4f));

        parts[1].position = new Vector2(4f, 0f);
        Physics2D.SyncTransforms();

        Assert.IsFalse(target.AreAllRequiredPartsWithinIntake(intake, claim, 1.4f));
    }

    [Test]
    public void DestroyedRequiredPart_UpdatesSetAndInvalidatesWhenNoneRemain()
    {
        DeadRobotCollectable target = CreateDeadTarget(2, out _);
        GameObject owner = CreateObject("Machine");
        Assert.IsTrue(target.TryClaim(1, owner, out CollectorTargetClaim claim));
        int changeCount = 0;
        target.OnRequiredPartsChanged += _ => changeCount++;
        IReadOnlyList<Rigidbody2D> parts = target.GetRequiredParts(claim);

        Object.DestroyImmediate(parts[0].gameObject);

        Assert.AreEqual(1, target.GetRequiredParts(claim).Count);
        Assert.AreEqual(1, changeCount);
        Object.DestroyImmediate(target.GetRequiredParts(claim)[0].gameObject);
        Assert.AreEqual(0, target.GetRequiredParts(claim).Count);
        Assert.IsFalse(target.IsClaimValid(claim));
    }

    [Test]
    public void CompleteCollection_DetachesBadgeAndLeavesDisposalToMachine()
    {
        DeadRobotCollectable target = CreateDeadTarget(1, out _);
        Inventory inventory = target.gameObject.AddComponent<Inventory>();
        GameObject badgeObject = CreateObject("Badge");
        badgeObject.transform.SetParent(target.transform);
        badgeObject.AddComponent<BoxCollider2D>();
        SecurityBadgePickup badge = badgeObject.AddComponent<SecurityBadgePickup>();
        badge.AssignInventory(inventory);
        inventory.SetItem(PickupType.SecurityBadge, badge);
        GameObject owner = CreateObject("Machine");
        Assert.IsTrue(target.TryClaim(1, owner, out CollectorTargetClaim claim));

        Assert.IsTrue(target.CompleteCollection(claim));

        Assert.IsTrue(target.IsCompleted);
        Assert.IsFalse(target.IsClaimValid(claim));
        Assert.IsNull(badge.transform.parent);
        Assert.IsFalse(inventory.HasItem(PickupType.SecurityBadge));
        Assert.IsTrue(target.gameObject.activeSelf, "The machine owns final pool timing.");
    }

    private DeadRobotCollectable CreateDeadTarget(
        int partCount,
        out RobotStateController state)
    {
        GameObject root = CreateObject("Dead robot");
        state = root.AddComponent<RobotStateController>();
        for (int i = 0; i < partCount; i++)
            AddPhysicalPart(root.transform, $"Part {i}", new Vector2(i, 0f));

        state.SetInitialDeadState();
        return DeadRobotCollectable.EnsureFor(state);
    }

    private Rigidbody2D AddPhysicalPart(Transform parent, string partName, Vector2 position)
    {
        Rigidbody2D body = AddBody(parent, partName, position);
        body.gameObject.AddComponent<BoxCollider2D>();
        return body;
    }

    private Rigidbody2D AddBody(Transform parent, string partName, Vector2 position)
    {
        GameObject part = CreateObject(partName);
        part.transform.SetParent(parent);
        part.transform.position = position;
        Rigidbody2D body = part.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.simulated = true;
        return body;
    }

    private static IReadOnlyList<Rigidbody2D> GetChildBodies(DeadRobotCollectable target)
    {
        return target.GetComponentsInChildren<Rigidbody2D>(true);
    }

    private GameObject CreateObject(string objectName)
    {
        GameObject created = new GameObject(objectName);
        createdObjects.Add(created);
        return created;
    }
}
