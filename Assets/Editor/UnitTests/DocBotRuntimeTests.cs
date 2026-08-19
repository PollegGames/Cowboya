using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class DocBotRuntimeTests {
    private sealed class DocBotFixture {
        public GameObject Root;
        public Rigidbody2D RootBody;
        public HealthBot Health;
        public RobotStateController State;
        public EnemyGrabbable Grabbable;
        public DocBotController Controller;
        public DocBotItemHolder ItemHolder;
        public Transform LeftHand;
        public Transform RightHand;
    }

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
    public void DocBot_UsesNonCombatStatsAndFearsDamageAndGrab() {
        DocBotFixture docBot = CreateDocBot();
        LaboratoryProgress progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryBeginVisit(0));
        docBot.Controller.InitializeForVisit(progress);

        RobotStats stats = docBot.State.Stats;
        Assert.IsNotNull(stats);
        Assert.AreEqual("DocBot", stats.RobotName);
        Assert.AreEqual(20f, stats.MaxHealth);
        Assert.AreEqual(20f, stats.CurrentHealth);
        Assert.AreEqual(0f, stats.MaxEnergy);
        Assert.AreEqual(0f, stats.CurrentEnergy);
        Assert.IsNotNull(stats.Attacks);
        Assert.IsEmpty(stats.Attacks);
        Assert.IsFalse(docBot.State.CanPerformAttack());
        Assert.AreEqual(DocBotActivity.Work, docBot.Controller.CurrentActivity);

        docBot.Health.TakeDamage(1);

        Assert.AreEqual(19f, stats.CurrentHealth);
        Assert.AreEqual(RobotState.Alive, docBot.State.CurrentState);
        Assert.AreEqual(DocBotActivity.CowardTemporary, docBot.Controller.CurrentActivity);

        docBot.Controller.InitializeForVisit(progress);
        Transform playerHand = CreateChild(
            CreateObject("Grab owner").transform,
            "Grab hand",
            Vector3.zero);
        docBot.Grabbable.OnGrab(playerHand);

        Assert.IsTrue(docBot.Grabbable.IsGrabbed);
        Assert.AreEqual(DocBotActivity.CowardTemporary, docBot.Controller.CurrentActivity);

        docBot.Grabbable.OnRelease(Vector2.zero);

        Assert.IsFalse(docBot.Grabbable.IsGrabbed);
        Assert.AreEqual(DocBotActivity.CowardTemporary, docBot.Controller.CurrentActivity);
    }

    [Test]
    public void JunkTransfer_LocksAndDetachesBothPlayerHandsBeforeClosestDocBotHandOwnsIt() {
        DocBotFixture docBot = CreateDocBot();
        Vector2 offeredPosition = new Vector2(-0.45f, 0f);
        CowboyGrabController playerGrab = CreateGrabController(
            offeredPosition,
            offeredPosition,
            out Inventory inventory);
        JunkPickup junk = CreateJunk("Junk_4(Clone)", offeredPosition);
        Physics2D.SyncTransforms();

        Assert.IsTrue(playerGrab.TryGrab(CowboyArmSide.Left));
        Assert.IsTrue(playerGrab.TryGrab(CowboyArmSide.Right));
        inventory.SetItem(PickupType.Cube, junk);
        Assert.IsTrue(docBot.ItemHolder.TryGetClosestFreeHand(
            junk.transform.position,
            out DocBotHand selectedHand));
        Assert.AreEqual(DocBotHand.Left, selectedHand);
        Assert.IsTrue(junk.TryLockGrab(docBot.ItemHolder));

        Assert.IsTrue(playerGrab.TryDetachHeldObject(junk));
        Assert.IsTrue(docBot.ItemHolder.TryAttachLockedJunk(junk, selectedHand));

        Assert.IsFalse(playerGrab.HasHeldObject(CowboyArmSide.Left));
        Assert.IsFalse(playerGrab.HasHeldObject(CowboyArmSide.Right));
        Assert.IsFalse(inventory.HasItem(PickupType.Cube));
        Assert.AreSame(junk, docBot.ItemHolder.HeldJunk);
        Assert.AreSame(docBot.LeftHand, junk.CurrentHolder);
        Assert.AreSame(docBot.LeftHand, junk.transform.parent);
        Assert.IsTrue(docBot.ItemHolder.IsHandFree(DocBotHand.Right));
        Assert.IsTrue(junk.IsGrabLocked);
        Assert.AreSame(docBot.ItemHolder, junk.GrabLockOwner);
        Assert.AreEqual(JunkVariant.Junk4, junk.Variant);
    }

    [Test]
    public void Death_DetachesHeldJunkBeforeCorpseCargoIsCreated() {
        DocBotFixture docBot = CreateDocBot();
        LaboratoryProgress progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryBeginVisit(0));
        docBot.Controller.InitializeForVisit(progress);
        JunkPickup junk = CreateJunk("Junk_7(Clone)", Vector2.zero);
        Rigidbody2D junkBody = junk.GetComponent<Rigidbody2D>();
        Assert.IsTrue(junk.TryLockGrab(docBot.ItemHolder));
        Assert.IsTrue(docBot.ItemHolder.TryAttachLockedJunk(junk, DocBotHand.Right));
        Assert.IsTrue(docBot.Controller.TryCommitAcceptedJunk(junk.Variant));
        Assert.IsNotNull(junk.GetComponent<CollectorCargoExclusion>());
        DeadRobotCollectable collectable = DeadRobotCollectable.EnsureFor(docBot.State);
        Assert.IsNotNull(collectable);
        Assert.IsFalse(collectable.IsCollectible);

        docBot.Health.TakeDamage(100);

        Assert.AreEqual(RobotState.Dead, docBot.State.CurrentState);
        Assert.AreEqual(DocBotActivity.Dead, docBot.Controller.CurrentActivity);
        Assert.IsTrue(progress.ScientistDiedThisVisit);
        Assert.IsFalse(docBot.ItemHolder.HasHeldJunk);
        Assert.IsFalse(junk.IsHeld);
        Assert.IsTrue(junk.IsGrabLocked);
        Assert.IsInstanceOf<LaboratoryStoredJunkRepresentation>(junk.GrabLockOwner);
        Assert.IsNull(junk.CurrentHolder);
        Assert.IsNull(junk.transform.parent);
        Assert.IsTrue(progress.TryFinalizeVisit(out LaboratoryVisitOutcome outcome));
        Assert.AreEqual(LaboratoryVisitOutcome.JunkReturnedAfterScientistDeath, outcome);
        Assert.AreEqual(1, progress.GetStoredJunkCount(JunkVariant.Junk7));
        Assert.AreEqual(0, progress.WhiteCubeCountPendingForNextVisit);

        GameObject claimOwner = CreateObject("Collector claim owner");
        Assert.IsTrue(collectable.IsCollectible);
        Assert.IsTrue(collectable.TryClaim(1, claimOwner, out CollectorTargetClaim claim));
        IReadOnlyList<Rigidbody2D> requiredParts = collectable.GetRequiredParts(claim);

        Assert.AreEqual(1, requiredParts.Count);
        Assert.AreSame(docBot.RootBody, requiredParts[0]);
        CollectionAssert.DoesNotContain(requiredParts, junkBody);
    }

    [Test]
    public void WhiteCubeReward_FirstGrabClaimsLogicalRewardExactlyOnce() {
        LaboratoryProgress progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryBeginVisit(0));
        Assert.IsTrue(progress.TryAcceptJunk(JunkVariant.Junk2));
        Assert.IsTrue(progress.TryFinalizeVisit());
        Assert.AreEqual(1, progress.WhiteCubeCountPendingForNextVisit);
        Assert.IsTrue(progress.TryBeginVisit(1));
        Assert.AreEqual(1, progress.AvailableWhiteCubeCount);

        CubePickup cube = CreateCube("DocBot white cube", Vector2.zero);
        LaboratoryWhiteCubeReward reward =
            cube.gameObject.AddComponent<LaboratoryWhiteCubeReward>();
        reward.Configure(progress);
        Transform nonPlayerHand = CreateObject("Non-player cube hand").transform;
        CowboyGrabController player = CreateGrabController(
            Vector2.zero,
            Vector2.zero,
            out _);
        Transform firstPlayerHand = CreateChild(
            player.transform,
            "First player cube hand",
            Vector3.zero);
        Transform secondPlayerHand = CreateChild(
            player.transform,
            "Second player cube hand",
            Vector3.zero);

        cube.OnGrab(nonPlayerHand);
        Assert.AreEqual(1, progress.AvailableWhiteCubeCount);
        Assert.AreEqual(0, progress.LaboratoryFreeWhiteCubeCount);

        cube.OnGrab(firstPlayerHand);
        Assert.AreEqual(0, progress.AvailableWhiteCubeCount);
        Assert.AreEqual(1, progress.LaboratoryFreeWhiteCubeCount);

        cube.OnGrab(secondPlayerHand);
        Assert.AreEqual(0, progress.AvailableWhiteCubeCount);
        Assert.AreEqual(1, progress.LaboratoryFreeWhiteCubeCount);
        Assert.IsFalse(progress.TryClaimAvailableWhiteCube());
    }

    [Test]
    public void JunkReceiver_ReservesCandidateAndHandAcrossOneSecondAndMultipleColliders() {
        DocBotFixture docBot = CreateDocBot();
        LaboratoryProgress progress = new LaboratoryProgress();
        Assert.IsTrue(progress.TryBeginVisit(0));
        docBot.Controller.InitializeForVisit(progress);

        GameObject receiverObject = CreateObject("DocBot receiver fixture");
        receiverObject.transform.SetParent(docBot.Root.transform, false);
        CircleCollider2D receiverCollider = receiverObject.AddComponent<CircleCollider2D>();
        receiverCollider.isTrigger = true;
        receiverCollider.radius = 2f;
        DocBotJunkReceiver receiver = receiverObject.AddComponent<DocBotJunkReceiver>();
        receiver.Configure(docBot.Controller, docBot.ItemHolder,
            docBot.Root.GetComponent<DocBotHandReachController>());

        CowboyGrabController firstPlayer = CreateGrabController(
            new Vector2(-0.45f, 0f),
            new Vector2(-0.45f, 0f),
            out _);
        JunkPickup firstJunk = CreateJunk("Junk_4(Clone)", new Vector2(-0.45f, 0f));
        Collider2D firstCollider = firstJunk.GetComponent<Collider2D>();
        ((BoxCollider2D)firstCollider).size = Vector2.one * 0.1f;
        Transform extraColliderObject = CreateChild(
            firstJunk.transform,
            "Second Junk collider",
            firstJunk.transform.position);
        BoxCollider2D secondCollider = extraColliderObject.gameObject.AddComponent<BoxCollider2D>();
        secondCollider.size = Vector2.one * 0.1f;

        CowboyGrabController secondPlayer = CreateGrabController(
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            out _);
        JunkPickup secondJunk = CreateJunk("Junk_8(Clone)", new Vector2(0.5f, 0f));
        ((BoxCollider2D)secondJunk.GetComponent<Collider2D>()).size = Vector2.one * 0.1f;

        Physics2D.SyncTransforms();
        Assert.IsTrue(firstPlayer.TryGrab(CowboyArmSide.Left));
        Assert.IsTrue(secondPlayer.TryGrab(CowboyArmSide.Left));
        Physics2D.SyncTransforms();

        InvokePrivate(receiver, "OnTriggerEnter2D", firstCollider);
        InvokePrivate(receiver, "OnTriggerEnter2D", secondCollider);
        receiver.AdvanceAcceptance(0f);
        Assert.AreSame(firstJunk, receiver.ActiveCandidate);
        Assert.AreEqual(DocBotHand.Left, receiver.ActiveCandidateHand);

        InvokePrivate(
            receiver,
            "OnTriggerEnter2D",
            secondJunk.GetComponent<Collider2D>());
        InvokePrivate(receiver, "OnTriggerExit2D", firstCollider);
        receiver.AdvanceAcceptance(0.6f);

        Assert.AreSame(firstJunk, receiver.ActiveCandidate);
        Assert.AreEqual(DocBotHand.Left, receiver.ActiveCandidateHand);
        Assert.AreEqual(0.6f, receiver.CandidateElapsed, 0.0001f);

        receiver.AdvanceAcceptance(0.4f);

        Assert.IsFalse(receiver.enabled);
        Assert.AreSame(firstJunk, docBot.ItemHolder.HeldJunk);
        Assert.AreSame(docBot.LeftHand, firstJunk.CurrentHolder);
        Assert.AreEqual(JunkVariant.Junk4, progress.AcceptedJunkVariant);
        Assert.AreSame(secondJunk, secondPlayer.GetHeldObject(CowboyArmSide.Left));
    }

    [Test]
    public void HandReach_DisableRestoresOriginalMasterPoseWithoutRecachingExtension() {
        GameObject root = CreateObject("DocBot reach fixture");
        Transform leftTarget = CreateChild(
            root.transform,
            "Left solver target",
            new Vector3(-1f, 0f, 0f));
        Transform rightTarget = CreateChild(
            root.transform,
            "Right solver target",
            new Vector3(1f, 0f, 0f));
        Transform offeredItem = CreateObject("Offered reach item").transform;
        offeredItem.position = new Vector3(2f, 0f, 0f);
        DocBotHandReachController reach = root.AddComponent<DocBotHandReachController>();
        reach.Configure(leftTarget, rightTarget);
        Vector3 leftRest = leftTarget.localPosition;
        Vector3 rightRest = rightTarget.localPosition;

        reach.BeginReach(DocBotHand.Left, offeredItem);
        reach.AdvancePose(0.5f);
        Assert.AreNotEqual(leftRest, leftTarget.localPosition);
        Assert.AreEqual(rightRest, rightTarget.localPosition);

        reach.enabled = false;
        InvokeLifecycle(reach, "OnDisable");
        Assert.AreEqual(leftRest, leftTarget.localPosition);
        Assert.AreEqual(rightRest, rightTarget.localPosition);

        reach.enabled = true;
        InvokeLifecycle(reach, "OnEnable");
        offeredItem.position = new Vector3(-2f, 1f, 0f);
        reach.BeginReach(DocBotHand.Left, offeredItem);
        reach.AdvancePose(0.5f);
        Assert.AreNotEqual(leftRest, leftTarget.localPosition);

        reach.enabled = false;
        InvokeLifecycle(reach, "OnDisable");
        Assert.AreEqual(leftRest, leftTarget.localPosition);
    }

    private DocBotFixture CreateDocBot() {
        GameObject root = CreateObject("DocBot runtime fixture");
        Rigidbody2D rootBody = root.AddComponent<Rigidbody2D>();
        rootBody.bodyType = RigidbodyType2D.Dynamic;
        rootBody.simulated = true;
        BoxCollider2D rootCollider = root.AddComponent<BoxCollider2D>();
        rootCollider.size = new Vector2(0.2f, 0.2f);

        HealthBot health = root.AddComponent<HealthBot>();
        JointBreaker jointBreaker = root.AddComponent<JointBreaker>();
        RobotStateController state = root.AddComponent<RobotStateController>();
        state.ConfigureCoreReferences(health, jointBreaker, null);
        // Edit Mode does not dispatch runtime lifecycle messages for dynamically
        // added MonoBehaviours, so explicitly establish the health subscription.
        InvokeLifecycle(state, "Awake");
        EnemyGrabbable grabbable = root.AddComponent<EnemyGrabbable>();
        DocBotItemHolder itemHolder = root.AddComponent<DocBotItemHolder>();
        DocBotHandReachController handReach =
            root.AddComponent<DocBotHandReachController>();
        Transform leftHand = CreateChild(
            root.transform,
            "DocBot left hand",
            new Vector3(-0.5f, 0f, 0f));
        Transform rightHand = CreateChild(
            root.transform,
            "DocBot right hand",
            new Vector3(0.5f, 0f, 0f));
        itemHolder.Configure(leftHand, rightHand);
        handReach.Configure(leftHand, rightHand);

        DocBotController controller = root.AddComponent<DocBotController>();
        controller.Configure(
            state,
            health,
            jointBreaker,
            grabbable,
            itemHolder,
            handReach);

        return new DocBotFixture {
            Root = root,
            RootBody = rootBody,
            Health = health,
            State = state,
            Grabbable = grabbable,
            Controller = controller,
            ItemHolder = itemHolder,
            LeftHand = leftHand,
            RightHand = rightHand
        };
    }

    private CowboyGrabController CreateGrabController(
        Vector2 leftHandPosition,
        Vector2 rightHandPosition,
        out Inventory inventory) {
        GameObject player = CreateObject("DocBot transfer player");
        inventory = player.AddComponent<Inventory>();
        CowboyGrabController controller = player.AddComponent<CowboyGrabController>();
        Transform leftHand = CreateChild(
            player.transform,
            "Player left hand",
            leftHandPosition);
        Transform rightHand = CreateChild(
            player.transform,
            "Player right hand",
            rightHandPosition);

        SerializedObject serializedController = new SerializedObject(controller);
        SetObjectReference(serializedController, "leftHandGrabAnchor", leftHand);
        SetObjectReference(serializedController, "leftHandHoldParent", leftHand);
        SetObjectReference(serializedController, "rightHandGrabAnchor", rightHand);
        SetObjectReference(serializedController, "rightHandHoldParent", rightHand);
        SetObjectReference(serializedController, "inventory", inventory);
        SerializedProperty radius = serializedController.FindProperty("grabRadius");
        Assert.IsNotNull(radius);
        radius.floatValue = 0.25f;
        serializedController.ApplyModifiedPropertiesWithoutUndo();
        return controller;
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

    private static void InvokePrivate(
        MonoBehaviour behaviour,
        string methodName,
        object argument) {
        MethodInfo method = behaviour.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Expected private method '{methodName}'.");
        method.Invoke(behaviour, new[] { argument });
    }

    private GameObject CreateObject(string objectName) {
        GameObject created = new GameObject(objectName);
        createdObjects.Add(created);
        return created;
    }

    private static Transform CreateChild(
        Transform parent,
        string objectName,
        Vector3 position) {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        child.transform.position = position;
        return child.transform;
    }

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        Object value) {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        Assert.IsNotNull(property, propertyName);
        property.objectReferenceValue = value;
    }
}
