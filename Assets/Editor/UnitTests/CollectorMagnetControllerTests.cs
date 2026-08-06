using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CollectorMagnetControllerTests
{
    private readonly List<GameObject> createdObjects = new();
    private SimulationMode2D previousSimulationMode;

    [SetUp]
    public void SetUp()
    {
        previousSimulationMode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
    }

    [TearDown]
    public void TearDown()
    {
        Physics2D.simulationMode = previousSimulationMode;
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    [Test]
    public void Gathering_AddsDedicatedCappedJointWithoutChangingExistingJoint()
    {
        CollectorMissionAssignment assignment = CreateAssignment(1, out Rigidbody2D part);
        TargetJoint2D existingJoint = part.gameObject.AddComponent<TargetJoint2D>();
        existingJoint.enabled = false;
        CollectorMagnetController2D magnet = CreateMagnet();

        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.02f);

        TargetJoint2D[] joints = part.GetComponents<TargetJoint2D>();
        CollectorCargoLink cargoLink = part.GetComponent<CollectorCargoLink>();
        Assert.AreEqual(2, joints.Length);
        Assert.Contains(existingJoint, joints);
        Assert.IsFalse(existingJoint.enabled);
        Assert.IsTrue(magnet.Owns(part));
        Assert.IsNotNull(cargoLink);
        Assert.That(cargoLink.OwnedJoint.maxForce, Is.EqualTo(90f).Within(0.001f));
        Assert.That(cargoLink.OwnedJoint.dampingRatio, Is.EqualTo(0.9f).Within(0.001f));

        magnet.ReleaseAll();

        Assert.IsNotNull(existingJoint);
        Assert.IsFalse(existingJoint.enabled);
        Assert.AreEqual(1, part.GetComponents<TargetJoint2D>().Length);
        Assert.AreEqual(0, part.GetComponents<CollectorCargoLink>().Length);
    }

    [Test]
    public void SecureObservation_RequiresDwellAndEscapingPartPublishesLoss()
    {
        CollectorMissionAssignment assignment = CreateAssignment(1, out Rigidbody2D part);
        CollectorMagnetController2D magnet = CreateMagnet(out Rigidbody2D magnetBody);
        part.position = magnetBody.transform.TransformPoint(new Vector2(0f, -0.3f));
        CollectorCargoStatus latest = default;
        int statusCount = 0;
        magnet.CargoStatusChanged += status =>
        {
            latest = status;
            statusCount++;
        };

        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.2f);
        Assert.IsFalse(latest.CargoSecure);
        magnet.StepPhysics(0.3f);

        Assert.IsTrue(latest.CargoSecure);
        Assert.AreEqual(1, latest.RequiredPartCount);
        Assert.AreEqual(1, latest.SecuredPartCount);

        Vector2 slot = part.GetComponent<CollectorCargoLink>().OwnedJoint.target;
        part.position = slot + Vector2.right;
        part.linearVelocity = Vector2.right * 10f;
        magnet.StepPhysics(0.02f);

        Assert.IsTrue(latest.CargoSecure, "Close cargo jitter must remain latched.");
        Assert.IsFalse(latest.CargoLost);

        part.position = new Vector2(10f, 10f);
        magnet.StepPhysics(0.02f);

        Assert.IsFalse(latest.CargoSecure);
        Assert.IsTrue(latest.CargoLost);
        Assert.GreaterOrEqual(statusCount, 3);
    }

    [Test]
    public void CargoSlots_KeepWorldSpacingWhenCollectorRootIsScaled()
    {
        CollectorMissionAssignment assignment = CreateAssignment(2, out _);
        IReadOnlyList<Rigidbody2D> parts = assignment.Target.GetRequiredParts(assignment.Claim);
        CollectorMagnetController2D magnet = CreateMagnet();
        magnet.transform.localScale = Vector3.one * 0.4f;

        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.02f);

        Vector2 firstTarget = parts[0].GetComponent<CollectorCargoLink>().OwnedJoint.target;
        Vector2 secondTarget = parts[1].GetComponent<CollectorCargoLink>().OwnedJoint.target;
        Assert.That(Vector2.Distance(firstTarget, secondTarget), Is.EqualTo(0.32f).Within(0.001f));
    }

    [Test]
    public void SecureObservation_UsesVelocityAtTheRotatingCargoSlot()
    {
        CollectorMissionAssignment assignment = CreateAssignment(1, out Rigidbody2D part);
        CollectorMagnetController2D magnet = CreateMagnet(out Rigidbody2D magnetBody);
        CollectorCargoStatus latest = default;
        magnet.CargoStatusChanged += status => latest = status;
        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.02f);

        Vector2 slot = part.GetComponent<CollectorCargoLink>().OwnedJoint.target;
        magnetBody.angularVelocity = 720f;
        part.position = slot;
        part.linearVelocity = magnetBody.GetPointVelocity(slot);
        magnet.StepPhysics(0.2f);
        magnet.StepPhysics(0.3f);

        Assert.IsTrue(latest.CargoSecure);
    }

    [TestCase(
        "Assets/Resources/Prefabs/Robots/WorkerSpawner/WorkerSpawner.prefab",
        13)]
    [TestCase(
        "Assets/Resources/Prefabs/Robots/Follower/Follower.prefab",
        13)]
    public void SupportedCorpse_AllRequiredPartsBecomeSecure(
        string prefabPath,
        int expectedPartCount)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(prefab);
        GameObject corpse = Object.Instantiate(prefab);
        corpse.name = "WorkerSpawner collection test corpse";
        createdObjects.Add(corpse);
        RobotStateController state = corpse.GetComponent<RobotStateController>();
        Assert.IsNotNull(state);
        state.SetInitialDeadState();
        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(state);
        GameObject homeObject = CreateObject("WorkerSpawner collection test home");
        SpawnRobotCollectorController home = homeObject.AddComponent<SpawnRobotCollectorController>();
        Assert.IsTrue(target.TryClaim(31, home, out CollectorTargetClaim claim));
        CollectorMissionAssignment assignment = new CollectorMissionAssignment(
            31,
            home,
            target,
            claim);
        CollectorMagnetController2D magnet = CreateMagnet(out Rigidbody2D magnetBody);
        magnet.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        magnetBody.bodyType = RigidbodyType2D.Kinematic;
        magnet.transform.position = target.GetLiveCollectionCenter(claim) + Vector2.up * 0.75f;
        CollectorCargoStatus latest = default;
        magnet.CargoStatusChanged += status => latest = status;
        Physics2D.SyncTransforms();

        magnet.BeginGathering(assignment);
        for (int i = 0; i < 500 && !latest.CargoSecure; i++)
        {
            magnet.StepPhysics(0.02f);
            Physics2D.Simulate(0.02f);
        }

        Assert.That(latest.RequiredPartCount, Is.EqualTo(expectedPartCount));
        IReadOnlyList<Rigidbody2D> requiredParts = target.GetRequiredParts(claim);
        List<string> partDetails = new List<string>();
        for (int i = 0; i < requiredParts.Count; i++)
        {
            Rigidbody2D part = requiredParts[i];
            CollectorCargoLink link = part != null
                ? part.GetComponent<CollectorCargoLink>()
                : null;
            float distance = link != null && link.OwnedJoint != null
                ? Vector2.Distance(part.worldCenterOfMass, link.OwnedJoint.target)
                : float.PositiveInfinity;
            partDetails.Add(
                $"{part?.name ?? "missing"}: secured={link != null && link.IsSecured}, "
                + $"distance={distance:0.000}, velocity={part?.linearVelocity.magnitude ?? 0f:0.000}");
        }
        Assert.IsTrue(
            latest.CargoSecure,
            $"Only {latest.SecuredPartCount}/{latest.RequiredPartCount} parts became secure. "
            + string.Join("; ", partDetails));
    }

    [TestCase(
        "Assets/Resources/Prefabs/Robots/Follower/Follower.prefab",
        13)]
    [TestCase(
        "Assets/Resources/Prefabs/Robots/WorkerSpawner/WorkerSpawner.prefab",
        13)]
    public void AuthoredCorpse_OnFloor_AllRequiredPartsBecomeSecure(
        string corpsePrefabPath,
        int expectedPartCount)
    {
        GameObject corpsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            corpsePrefabPath);
        GameObject collectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab");
        Assert.IsNotNull(corpsePrefab);
        Assert.IsNotNull(collectorPrefab);

        GameObject floor = CreateObject("Follower collection test floor");
        floor.transform.position = new Vector2(0f, -0.25f);
        BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
        floorCollider.size = new Vector2(30f, 0.5f);

        GameObject corpse = Object.Instantiate(corpsePrefab, Vector3.up * 2f,
            Quaternion.identity);
        createdObjects.Add(corpse);
        RobotStateController state = corpse.GetComponent<RobotStateController>();
        Assert.IsNotNull(state);
        state.SetInitialDeadState();
        Rigidbody2D[] corpseBodies = corpse.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < corpseBodies.Length; i++)
        {
            Rigidbody2D corpseBody = corpseBodies[i];
            if (corpseBody == null || corpseBody.bodyType != RigidbodyType2D.Dynamic)
                continue;
            float direction = (i & 1) == 0 ? -1f : 1f;
            corpseBody.AddForce(
                new Vector2(direction * (0.5f + i * 0.08f), 0.3f),
                ForceMode2D.Impulse);
        }

        Physics2D.SyncTransforms();
        for (int i = 0; i < 150; i++)
            Physics2D.Simulate(0.02f);

        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(state);
        GameObject homeObject = CreateObject("Follower collection test home");
        SpawnRobotCollectorController home =
            homeObject.AddComponent<SpawnRobotCollectorController>();
        Assert.IsTrue(target.TryClaim(32, home, out CollectorTargetClaim claim));
        CollectorMissionAssignment assignment = new CollectorMissionAssignment(
            32,
            home,
            target,
            claim);

        GameObject collector = Object.Instantiate(collectorPrefab);
        createdObjects.Add(collector);
        collector.SetActive(false);
        collector.GetComponent<RobotHeartNew>().enabled = false;
        collector.GetComponent<RobotBrainNew>().enabled = false;
        collector.GetComponent<CollectorRobotObservationBridge>().enabled = false;
        collector.SetActive(true);
        CollectorRobotBodyController collectorBody =
            collector.GetComponent<CollectorRobotBodyController>();
        CollectorMagnetController2D magnet =
            collector.GetComponent<CollectorMagnetController2D>();
        Assert.IsNotNull(collectorBody);
        Assert.IsNotNull(magnet);
        collectorBody.ResetPhysicalState();
        Vector2 desiredBodyPosition = target.GetLiveCollectionCenter(claim) + Vector2.up * 0.75f;
        collector.transform.position +=
            (Vector3)(desiredBodyPosition - collectorBody.BodyRigidbody.position);
        Physics2D.SyncTransforms();

        CollectorCargoStatus latest = default;
        magnet.CargoStatusChanged += status => latest = status;
        collectorBody.BeginGathering(assignment);
        for (int i = 0; i < 1000 && !latest.CargoSecure; i++)
        {
            collectorBody.StepPhysics(0.02f);
            Physics2D.Simulate(0.02f);
        }

        IReadOnlyList<Rigidbody2D> requiredParts = target.GetRequiredParts(claim);
        List<string> partDetails = new List<string>();
        for (int i = 0; i < requiredParts.Count; i++)
        {
            Rigidbody2D part = requiredParts[i];
            CollectorCargoLink link = part != null
                ? part.GetComponent<CollectorCargoLink>()
                : null;
            float distance = link != null && link.OwnedJoint != null
                ? Vector2.Distance(part.worldCenterOfMass, link.OwnedJoint.target)
                : float.PositiveInfinity;
            partDetails.Add(
                $"{part?.name ?? "missing"}: secured={link != null && link.IsSecured}, "
                + $"distance={distance:0.000}, position={part?.position}, "
                + $"velocity={part?.linearVelocity.magnitude ?? 0f:0.000}");
        }

        Assert.IsTrue(
            latest.CargoSecure,
            $"Only {latest.SecuredPartCount}/{latest.RequiredPartCount} floor parts "
            + "became secure. " + string.Join("; ", partDetails));
        Assert.AreEqual(expectedPartCount, latest.RequiredPartCount);
    }

    [TestCase(
        "Assets/Resources/Prefabs/Robots/Follower/Follower.prefab")]
    [TestCase(
        "Assets/Resources/Prefabs/Robots/WorkerSpawner/WorkerSpawner.prefab")]
    public void DistantHead_OnFloor_IsPhysicallyRecovered(string corpsePrefabPath)
    {
        GameObject corpsePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            corpsePrefabPath);
        GameObject collectorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Robots/Collector/CollectorRobot_Fly.prefab");
        Assert.IsNotNull(corpsePrefab);
        Assert.IsNotNull(collectorPrefab);

        GameObject floor = CreateObject("Distant head collection test floor");
        floor.transform.position = new Vector2(0f, -0.25f);
        BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
        floorCollider.size = new Vector2(40f, 0.5f);

        GameObject corpse = Object.Instantiate(
            corpsePrefab,
            Vector3.up * 2f,
            Quaternion.identity);
        createdObjects.Add(corpse);
        RobotStateController state = corpse.GetComponent<RobotStateController>();
        Assert.IsNotNull(state);
        state.SetInitialDeadState();
        Physics2D.SyncTransforms();
        for (int i = 0; i < 150; i++)
            Physics2D.Simulate(0.02f);

        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(state);
        GameObject homeObject = CreateObject("Distant head collection test home");
        SpawnRobotCollectorController home =
            homeObject.AddComponent<SpawnRobotCollectorController>();
        Assert.IsTrue(target.TryClaim(33, home, out CollectorTargetClaim claim));
        IReadOnlyList<Rigidbody2D> requiredParts = target.GetRequiredParts(claim);
        Rigidbody2D head = null;
        for (int i = 0; i < requiredParts.Count; i++)
        {
            if (requiredParts[i] != null && requiredParts[i].name == "Head")
            {
                head = requiredParts[i];
                break;
            }
        }

        Assert.IsNotNull(head);
        head.position += Vector2.right * 7.5f;
        head.linearVelocity = Vector2.zero;
        head.angularVelocity = 0f;
        Physics2D.SyncTransforms();

        CollectorMissionAssignment assignment = new CollectorMissionAssignment(
            33,
            home,
            target,
            claim);
        GameObject collector = Object.Instantiate(collectorPrefab);
        createdObjects.Add(collector);
        collector.SetActive(false);
        collector.GetComponent<RobotHeartNew>().enabled = false;
        collector.GetComponent<RobotBrainNew>().enabled = false;
        collector.GetComponent<CollectorRobotObservationBridge>().enabled = false;
        collector.SetActive(true);
        CollectorRobotBodyController collectorBody =
            collector.GetComponent<CollectorRobotBodyController>();
        CollectorMagnetController2D magnet =
            collector.GetComponent<CollectorMagnetController2D>();
        collectorBody.ResetPhysicalState();
        Vector2 desiredBodyPosition = target.GetLiveCollectionCenter(claim)
            + Vector2.up * 0.75f;
        collector.transform.position +=
            (Vector3)(desiredBodyPosition - collectorBody.BodyRigidbody.position);
        Physics2D.SyncTransforms();

        CollectorCargoStatus latest = default;
        magnet.CargoStatusChanged += status => latest = status;
        collectorBody.BeginGathering(assignment);
        for (int i = 0; i < 2000 && !latest.CargoSecure; i++)
        {
            collectorBody.StepPhysics(0.02f);
            Physics2D.Simulate(0.02f);
        }

        CollectorCargoLink headLink = head.GetComponent<CollectorCargoLink>();
        Assert.IsTrue(
            latest.CargoSecure,
            $"Only {latest.SecuredPartCount}/{latest.RequiredPartCount} parts secured. "
            + $"Head slot distance={headLink?.LastSlotDistance:0.000}, "
            + $"cargo distance={headLink?.LastCargoCenterDistance:0.000}, "
            + $"collector={collectorBody.BodyRigidbody.position}, head={head.position}.");
    }

    [Test]
    public void SecureObservation_HasBoundedFallbackForCloseContactJitter()
    {
        CollectorMissionAssignment assignment = CreateAssignment(1, out Rigidbody2D part);
        CollectorMagnetController2D magnet = CreateMagnet();
        CollectorCargoStatus latest = default;
        magnet.CargoStatusChanged += status => latest = status;
        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.02f);

        Vector2 slot = part.GetComponent<CollectorCargoLink>().OwnedJoint.target;
        part.position = slot;
        part.linearVelocity = Vector2.right * 10f;
        magnet.StepPhysics(0.46f);
        magnet.StepPhysics(0.46f);
        Assert.IsFalse(latest.CargoSecure);
        magnet.StepPhysics(0.46f);

        Assert.IsTrue(latest.CargoSecure);
    }

    [Test]
    public void StuckPartRecovery_SecuresOwnedPartInsideEscapeEnvelope()
    {
        CollectorMissionAssignment assignment = CreateAssignment(1, out Rigidbody2D part);
        CollectorMagnetController2D magnet = CreateMagnet();
        CollectorCargoStatus latest = default;
        magnet.CargoStatusChanged += status => latest = status;
        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.02f);
        CollectorCargoLink link = part.GetComponent<CollectorCargoLink>();
        Assert.IsNotNull(link);
        Vector2 slot = link.OwnedJoint.target;
        part.position = slot + Vector2.right * 2.5f;
        part.linearVelocity = Vector2.zero;

        for (int i = 0; i < 8 && !latest.CargoSecure; i++)
            magnet.StepPhysics(0.5f);

        Assert.IsTrue(latest.CargoSecure);
        Assert.IsFalse(link.IsRecoveryActive);
        Assert.That(link.OwnedJoint.maxForce, Is.EqualTo(360f).Within(0.001f));
    }

    [Test]
    public void StuckPartRecovery_ReleasesBlockingContactAndRestoresCollision()
    {
        CollectorMissionAssignment assignment = CreateAssignment(1, out Rigidbody2D part);
        part.gravityScale = 0f;
        part.position = Vector2.right * 4f;
        Collider2D partCollider = part.GetComponent<Collider2D>();
        GameObject wall = CreateObject("Cargo recovery wall");
        wall.transform.position = Vector2.right * 2f;
        BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.size = new Vector2(0.5f, 20f);
        CollectorMagnetController2D magnet = CreateMagnet(out Rigidbody2D magnetBody);
        magnet.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        magnetBody.bodyType = RigidbodyType2D.Kinematic;
        CollectorCargoStatus latest = default;
        magnet.CargoStatusChanged += status => latest = status;
        Physics2D.SyncTransforms();

        magnet.BeginGathering(assignment);
        bool sawRecoveryOverride = false;
        for (int i = 0; i < 600 && !latest.CargoSecure; i++)
        {
            magnet.StepPhysics(0.02f);
            CollectorCargoLink activeLink = part.GetComponent<CollectorCargoLink>();
            sawRecoveryOverride |= activeLink != null
                && activeLink.RecoveryCollisionOverrideCount > 0;
            Physics2D.Simulate(0.02f);
        }

        Assert.IsTrue(sawRecoveryOverride, "The blocking wall contact was not released.");
        Assert.IsTrue(latest.CargoSecure);
        Assert.IsFalse(Physics2D.GetIgnoreCollision(partCollider, wallCollider));
    }

    [Test]
    public void CompactCargoEnvelope_SecuresPartDespiteDistantArbitrarySlot()
    {
        CollectorMissionAssignment assignment = CreateAssignment(1, out Rigidbody2D part);
        CollectorMagnetController2D magnet = CreateMagnet(out Rigidbody2D magnetBody);
        SetPrivateField(magnet, "firstRowOffset", 5f);
        part.position = magnetBody.position;
        part.linearVelocity = Vector2.zero;
        CollectorCargoStatus latest = default;
        magnet.CargoStatusChanged += status => latest = status;

        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.5f);

        CollectorCargoLink link = part.GetComponent<CollectorCargoLink>();
        Assert.IsNotNull(link);
        Assert.That(link.LastSlotDistance, Is.GreaterThan(4f));
        Assert.That(link.LastCargoCenterDistance, Is.LessThan(0.001f));
        Assert.IsTrue(latest.CargoSecure);
    }

    [Test]
    public void CarryMode_RetainsOwnedPartsButDoesNotAcquireMissingParts()
    {
        CollectorMissionAssignment assignment = CreateAssignment(2, out _);
        IReadOnlyList<Rigidbody2D> parts = assignment.Target.GetRequiredParts(assignment.Claim);
        CollectorMagnetController2D magnet = CreateMagnet();

        magnet.BeginCarry(assignment);
        magnet.StepPhysics(0.02f);
        Assert.AreEqual(0, magnet.OwnedPartCount);

        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.02f);
        Assert.AreEqual(2, magnet.OwnedPartCount);

        magnet.BeginCarry(assignment);
        magnet.StepPhysics(0.02f);
        Assert.IsTrue(magnet.Owns(parts[0]));
        Assert.IsTrue(magnet.Owns(parts[1]));
    }

    [Test]
    public void UnsecuredCenter_UsesOnlyAssignedUnsettledParts()
    {
        CollectorMissionAssignment assignment = CreateAssignment(2, out _);
        IReadOnlyList<Rigidbody2D> parts = assignment.Target.GetRequiredParts(assignment.Claim);
        parts[0].position = new Vector2(-2f, 4f);
        parts[1].position = new Vector2(4f, 2f);
        CollectorMagnetController2D magnet = CreateMagnet();
        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.02f);

        Assert.IsTrue(magnet.TryGetUnsecuredCenter(assignment, out Vector2 center));
        Assert.That(center.x, Is.EqualTo(1f).Within(0.001f));
        Assert.That(center.y, Is.EqualTo(3f).Within(0.001f));
    }

    [Test]
    public void ReleaseAll_DisablesOwnedJointSynchronouslyAndRejectsOldOwnership()
    {
        CollectorMissionAssignment assignment = CreateAssignment(1, out Rigidbody2D part);
        CollectorMagnetController2D magnet = CreateMagnet();
        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.02f);
        CollectorCargoLink link = part.GetComponent<CollectorCargoLink>();
        TargetJoint2D ownedJoint = link.OwnedJoint;
        Assert.IsTrue(ownedJoint.enabled);

        magnet.ReleaseAll();

        Assert.IsFalse(magnet.Owns(part));
        Assert.IsFalse(link != null && link.IsActive);
        Assert.IsTrue(ownedJoint == null || !ownedJoint.enabled);
    }

    [Test]
    public void CollisionOverrides_AreScopedAndRestoreExactPreviousStates()
    {
        CollectorMissionAssignment assignment = CreateAssignment(2, out Rigidbody2D firstPart);
        IReadOnlyList<Rigidbody2D> parts = assignment.Target.GetRequiredParts(assignment.Claim);
        Rigidbody2D secondPart = parts[0] == firstPart ? parts[1] : parts[0];
        Collider2D firstCargoCollider = firstPart.GetComponent<Collider2D>();
        Collider2D secondCargoCollider = secondPart.GetComponent<Collider2D>();
        CollectorMagnetController2D magnet = CreateMagnet(out Rigidbody2D magnetBody);
        Collider2D collectorBodyCollider = magnet.GetComponent<Collider2D>();
        Collider2D collectorMagnetCollider = magnetBody.GetComponent<Collider2D>();
        Physics2D.IgnoreCollision(firstCargoCollider, collectorMagnetCollider, true);
        bool originalCorpsePair = Physics2D.GetIgnoreCollision(
            firstCargoCollider,
            secondCargoCollider);

        magnet.BeginGathering(assignment);
        magnet.StepPhysics(0.02f);

        Assert.IsTrue(Physics2D.GetIgnoreCollision(
            firstCargoCollider,
            collectorBodyCollider));
        Assert.IsTrue(Physics2D.GetIgnoreCollision(
            firstCargoCollider,
            collectorMagnetCollider));
        Assert.IsTrue(Physics2D.GetIgnoreCollision(
            secondCargoCollider,
            collectorBodyCollider));
        Assert.AreEqual(
            originalCorpsePair,
            Physics2D.GetIgnoreCollision(firstCargoCollider, secondCargoCollider));

        magnet.ReleaseAll();

        Assert.IsFalse(Physics2D.GetIgnoreCollision(
            firstCargoCollider,
            collectorBodyCollider));
        Assert.IsTrue(
            Physics2D.GetIgnoreCollision(firstCargoCollider, collectorMagnetCollider),
            "A pre-existing ignore must remain ignored.");
        Assert.IsFalse(Physics2D.GetIgnoreCollision(
            secondCargoCollider,
            collectorBodyCollider));
        Assert.AreEqual(
            originalCorpsePair,
            Physics2D.GetIgnoreCollision(firstCargoCollider, secondCargoCollider));

        Physics2D.IgnoreCollision(firstCargoCollider, collectorMagnetCollider, false);
    }

    private CollectorMissionAssignment CreateAssignment(
        int partCount,
        out Rigidbody2D firstPart)
    {
        GameObject targetObject = CreateObject("Dead target");
        RobotStateController state = targetObject.AddComponent<RobotStateController>();
        firstPart = null;
        for (int i = 0; i < partCount; i++)
        {
            GameObject partObject = CreateObject($"Part {i}");
            partObject.transform.SetParent(targetObject.transform);
            Rigidbody2D part = partObject.AddComponent<Rigidbody2D>();
            part.gravityScale = 3f;
            partObject.AddComponent<BoxCollider2D>();
            if (firstPart == null)
                firstPart = part;
        }

        state.SetInitialDeadState();
        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(state);
        GameObject homeObject = CreateObject("Collector home");
        SpawnRobotCollectorController home = homeObject.AddComponent<SpawnRobotCollectorController>();
        Assert.IsTrue(target.TryClaim(11, home, out CollectorTargetClaim claim));
        return new CollectorMissionAssignment(11, home, target, claim);
    }

    private CollectorMagnetController2D CreateMagnet()
    {
        return CreateMagnet(out _);
    }

    private CollectorMagnetController2D CreateMagnet(out Rigidbody2D magnetBody)
    {
        GameObject collector = CreateObject("Collector");
        Rigidbody2D collectorBody = collector.AddComponent<Rigidbody2D>();
        collector.AddComponent<BoxCollider2D>();
        GameObject magnetObject = CreateObject("Magnet body");
        magnetObject.transform.SetParent(collector.transform);
        magnetBody = magnetObject.AddComponent<Rigidbody2D>();
        magnetObject.AddComponent<CircleCollider2D>();
        CollectorMagnetController2D magnet = collector.AddComponent<CollectorMagnetController2D>();
        magnet.ConfigureReferences(collectorBody, magnetBody);
        return magnet;
    }

    private GameObject CreateObject(string objectName)
    {
        GameObject created = new GameObject(objectName);
        createdObjects.Add(created);
        return created;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Expected private field '{fieldName}'.");
        field.SetValue(target, value);
    }
}
