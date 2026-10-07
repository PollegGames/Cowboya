using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class WorkerCollectorGrabPipelineTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private RobotNewPipelineMode previousMode;
    private bool previousDriveGameplayInShadow;

    [SetUp]
    public void SetUp()
    {
        previousMode = RobotNewPipelineRuntime.Mode;
        previousDriveGameplayInShadow = RobotNewPipelineRuntime.DriveGameplayInShadow;
        RobotNewPipelineRuntime.Mode = RobotNewPipelineMode.NewOnly;
        RobotNewPipelineRuntime.DriveGameplayInShadow = true;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null)
                UnityEngine.Object.DestroyImmediate(created[i]);
        }
        created.Clear();
        RobotNewPipelineRuntime.Mode = previousMode;
        RobotNewPipelineRuntime.DriveGameplayInShadow = previousDriveGameplayInShadow;
    }

    [Test]
    public void WhiteCubeClaim_IsExclusive_AndPlayerGrabInvalidatesIt()
    {
        WhiteCubeCargo cargo = CreateCargo("White");
        GameObject workerA = CreateObject("WorkerA");
        GameObject workerB = CreateObject("WorkerB");
        Transform carryAnchor = CreateObject("CarryAnchor").transform;
        Transform playerAnchor = CreateObject("PlayerAnchor").transform;

        Assert.IsTrue(cargo.TryClaim(workerA, out WhiteCubeClaim claim));
        Assert.IsFalse(cargo.TryClaim(workerB, out _));
        Assert.IsTrue(cargo.TryBeginCarry(claim, carryAnchor));
        Assert.AreEqual(carryAnchor, cargo.transform.parent);
        Assert.AreEqual(WhiteCubeCargoState.Carried, cargo.State);

        cargo.Pickup.OnGrab(playerAnchor);

        Assert.IsFalse(cargo.IsClaimValid(claim));
        Assert.AreEqual(playerAnchor, cargo.transform.parent);
        Assert.AreEqual(WhiteCubeCargoState.HeldExternally, cargo.State);
        cargo.Pickup.OnRelease(Vector2.zero);
        Assert.IsTrue(cargo.IsAvailable);
    }

    [Test]
    public void SourceProvider_SelectsOnlyMarkedWhiteCargo()
    {
        GameObject sourceObject = CreateObject("Source");
        WorkerCollectorWhiteCubeSourceProvider source =
            sourceObject.AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        GameObject colored = CreatePickup("Colored");
        colored.transform.SetParent(sourceObject.transform);
        WhiteCubeCargo white = CreateCargo("White");
        white.transform.SetParent(sourceObject.transform);
        GameObject worker = CreateObject("Worker");

        Assert.IsTrue(source.TryClaimCube(worker, out WhiteCubeCargo selected, out WhiteCubeClaim claim));
        Assert.AreSame(white, selected);
        Assert.IsTrue(white.IsClaimValid(claim));
        Assert.IsNull(colored.GetComponent<WhiteCubeCargo>());
    }

    [Test]
    public void SourceProvider_ClaimsConveyorCargoOnlyNearApproachWaypoint()
    {
        GameObject sourceObject = CreateObject("Source");
        WorkerCollectorWhiteCubeSourceProvider source =
            sourceObject.AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        GameObject approachObject = CreateObject("Approach");
        approachObject.transform.SetParent(sourceObject.transform);
        RoomWaypoint approach = approachObject.AddComponent<RoomWaypoint>();
        SetPrivateField(source, "approachWaypoint", approach);
        SetPrivateField(source, "cubeRoot", sourceObject.transform);

        GameObject conveyorObject = CreateObject("Conveyor");
        conveyorObject.transform.SetParent(sourceObject.transform);
        conveyorObject.AddComponent<GarageCubeConveyorController>();
        WhiteCubeCargo cargo = CreateCargo("MovingCargo");
        cargo.transform.SetParent(conveyorObject.transform);
        GameObject worker = CreateObject("Worker");

        cargo.transform.position = approach.transform.position + Vector3.right * 5f;
        Assert.IsFalse(source.TryClaimCube(worker, out _, out _));
        Assert.IsTrue(cargo.IsAvailable);

        cargo.transform.position = approach.transform.position + Vector3.right;
        Assert.IsTrue(source.TryClaimCube(worker, out WhiteCubeCargo selected, out WhiteCubeClaim claim));
        Assert.AreSame(cargo, selected);
        Assert.IsTrue(cargo.IsClaimValid(claim));
    }

    [Test]
    public void SourceProvider_UsesRigidbodyWorldXYAsCanonicalWorkerPlane()
    {
        GameObject sourceObject = CreateObject("RotatedSource");
        sourceObject.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        WorkerCollectorWhiteCubeSourceProvider source =
            sourceObject.AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        WhiteCubeCargo cargo = CreateCargo("PresentedCube");
        cargo.transform.SetParent(sourceObject.transform, false);
        cargo.GetComponent<Rigidbody2D>().position = new Vector2(7.5f, 3.25f);

        Vector2 pickupPoint = source.GetWorkerPlanePickupPoint(cargo);

        Assert.AreEqual(new Vector2(7.5f, 3.25f), pickupPoint);
        Assert.AreNotEqual(cargo.transform.position.z, pickupPoint.y);
    }

    [Test]
    public void BodyApproach_PlacesCubeOnConfiguredArmOrbit()
    {
        const string PrefabPath =
            "Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab";
        GameObject instance = UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        created.Add(instance);
        WorkerCollectorBodyController controller =
            instance.GetComponent<WorkerCollectorBodyController>();
        RobotObjectArmReachController reachController =
            instance.GetComponent<RobotObjectArmReachController>();
        GameObject sourceObject = CreateObject("ApproachSource");
        WorkerCollectorWhiteCubeSourceProvider source =
            sourceObject.AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        GameObject dropOffObject = CreateObject("ApproachDropOff");
        WorkerCollectorDropOffProvider dropOff =
            dropOffObject.AddComponent<WorkerCollectorDropOffProvider>();
        WhiteCubeCargo cargo = CreateCargo("ApproachCargo");
        cargo.GetComponent<Rigidbody2D>().position = new Vector2(12f, 4f);
        Assert.IsTrue(cargo.TryClaim(controller, out WhiteCubeClaim claim));
        var assignment = new WorkerCollectorMissionAssignment(
            99, source, dropOff, cargo, claim);

        Vector3 destination = InvokePrivate<Vector3>(
            controller, "CalculateBodyApproachPosition", assignment);
        Vector2 pickupPoint = source.GetWorkerPlanePickupPoint(cargo);

        Assert.Less(destination.x, pickupPoint.x);
        Assert.AreEqual(
            reachController.ReachRadius,
            Vector2.Distance(destination, pickupPoint),
            0.01f);
    }

    [Test]
    public void ArmReach_MovesOnlySolverXY_AndMeasuresFromEffector()
    {
        const string PrefabPath =
            "Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab";
        GameObject instance = UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        created.Add(instance);
        WorkerCollectorBodyController controller =
            instance.GetComponent<WorkerCollectorBodyController>();
        Transform solverTarget = FindDescendant(instance.transform, "RArm_Solver_Target");
        Transform effector = FindDescendant(instance.transform, "RHand_Effector");
        Vector3 solverStart = solverTarget.position;
        Vector3 effectorStart = effector.position;
        Vector2 pickupPoint = (Vector2)solverStart + new Vector2(2f, 1f);

        InvokePrivate<object>(
            controller, "AdvanceArmSolverTarget", pickupPoint, 0.1f);

        Assert.AreNotEqual(solverStart.x, solverTarget.position.x);
        Assert.AreNotEqual(solverStart.y, solverTarget.position.y);
        Assert.AreEqual(solverStart.z, solverTarget.position.z);
        Assert.AreEqual(effectorStart, effector.position);

        GameObject sourceObject = CreateObject("ReachSource");
        WorkerCollectorWhiteCubeSourceProvider source =
            sourceObject.AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        GameObject dropOffObject = CreateObject("ReachDropOff");
        WorkerCollectorDropOffProvider dropOff =
            dropOffObject.AddComponent<WorkerCollectorDropOffProvider>();
        WhiteCubeCargo cargo = CreateCargo("ReachCargo");
        cargo.GetComponent<Rigidbody2D>().position = (Vector2)effector.position + Vector2.right;
        Assert.IsTrue(cargo.TryClaim(controller, out WhiteCubeClaim claim));
        var assignment = new WorkerCollectorMissionAssignment(
            100, source, dropOff, cargo, claim);

        float reachDistance = InvokePrivate<float>(
            controller, "CalculateReachDistance", assignment);

        Assert.AreEqual(1f, reachDistance, 0.01f);
    }

    [Test]
    public void ArmReach_DiagnosticDoesNotAbandonUnsecuredCube()
    {
        const string PrefabPath =
            "Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab";
        GameObject instance = UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        created.Add(instance);
        WorkerCollectorBodyController controller =
            instance.GetComponent<WorkerCollectorBodyController>();
        Transform effector = FindDescendant(instance.transform, "RHand_Effector");
        GameObject sourceObject = CreateObject("PersistentReachSource");
        WorkerCollectorWhiteCubeSourceProvider source =
            sourceObject.AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        GameObject dropOffObject = CreateObject("PersistentReachDropOff");
        WorkerCollectorDropOffProvider dropOff =
            dropOffObject.AddComponent<WorkerCollectorDropOffProvider>();
        WhiteCubeCargo cargo = CreateCargo("PersistentReachCargo");
        cargo.GetComponent<Rigidbody2D>().position = (Vector2)effector.position + Vector2.right * 4f;
        Assert.IsTrue(cargo.TryClaim(controller, out WhiteCubeClaim claim));
        var assignment = new WorkerCollectorMissionAssignment(
            101, source, dropOff, cargo, claim);
        controller.GrabCube(assignment);
        SetPrivateField(controller, "nextArmReachDiagnosticAt", float.NegativeInfinity);

        InvokePrivate(controller, "Update");

        Assert.AreSame(assignment, controller.CurrentAssignment);
        Assert.IsTrue(cargo.IsClaimValid(claim));
        Assert.AreNotEqual(WhiteCubeCargoState.Carried, cargo.State);
    }

    [Test]
    public void GrabCube_ReachesWithObjectController_AndAttachesCargoToHandAnchor()
    {
        const string PrefabPath =
            "Assets/Resources/Prefabs/Robots/WorkerCollector/WorkerCollector.prefab";
        GameObject instance = UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        created.Add(instance);
        WorkerCollectorBodyController controller =
            instance.GetComponent<WorkerCollectorBodyController>();
        RobotObjectArmReachController reachController =
            instance.GetComponent<RobotObjectArmReachController>();
        Assert.IsNull(instance.GetComponent<WorkerCollectorArmReachTestIsolation>());

        GameObject sourceObject = CreateObject("GrabSource");
        WorkerCollectorWhiteCubeSourceProvider source =
            sourceObject.AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        GameObject dropOffObject = CreateObject("GrabDropOff");
        WorkerCollectorDropOffProvider dropOff =
            dropOffObject.AddComponent<WorkerCollectorDropOffProvider>();
        WhiteCubeCargo cargo = CreateCargo("GrabCargo");
        cargo.GetComponent<Rigidbody2D>().position =
            (Vector2)instance.transform.position + Vector2.right * reachController.ReachRadius;
        Assert.IsTrue(cargo.TryClaim(controller, out WhiteCubeClaim claim));
        var assignment = new WorkerCollectorMissionAssignment(
            102, source, dropOff, cargo, claim);

        controller.GrabCube(assignment);
        reachController.Tick(1f);
        Transform hand = reachController.ActiveHandEffector;
        Assert.IsNotNull(hand);
        cargo.GetComponent<Rigidbody2D>().position = hand.position;
        InvokePrivate(controller, "Update");

        Assert.AreSame(cargo.transform, reachController.Target);
        Assert.AreEqual(WhiteCubeCargoState.Carried, cargo.State);
        Assert.AreSame(controller.CarryAnchor, cargo.transform.parent);
    }

    [Test]
    public void WaypointPathFollower_PreciseFinalArrival_UsesIndependentAxisThresholds()
    {
        Transform body = CreateObject("Body").transform;
        RoomWaypoint start = CreateObject("Start").AddComponent<RoomWaypoint>();
        RoomWaypoint target = CreateObject("Target").AddComponent<RoomWaypoint>();
        start.transform.position = Vector3.zero;
        target.transform.position = Vector3.right * 5f;
        Vector3 finalPosition = target.transform.position + Vector3.right * 0.83f;
        PathMoverSpy mover = new PathMoverSpy();
        WaypointPathFollower follower = new WaypointPathFollower(
            body,
            mover,
            new PathQueriesStub(start, target),
            arrivalThresholdX: 2f,
            arrivalThresholdY: 2f);

        follower.SetDestination(
            target,
            finalPosition,
            includeUnavailable: true,
            preciseFinalArrivalThreshold: 0.3f,
            preciseFinalArrivalThresholdY: 2f,
            replaceTargetWaypoint: true);
        Assert.AreEqual(2, follower.CurrentPathCount);
        body.position = target.transform.position;
        follower.Update(0.02f);

        body.position = finalPosition - Vector3.right;
        follower.Update(0.02f);
        Assert.IsFalse(follower.HasArrived);
        Assert.AreEqual(1f, mover.Horizontal);

        body.position = finalPosition - Vector3.right * 0.2f + Vector3.up * 1.5f;
        follower.Update(0.02f);
        Assert.IsTrue(follower.HasArrived);
    }

    [Test]
    public void MemoryBrainHeartTask_FlowsFromClaimToTemporaryDropOff()
    {
        Pipeline setup = CreatePipeline();
        WorkerCollectorMissionAssignment assignment = CreateAssignment(setup.Body, 1);
        setup.Body.Commands.Clear();

        Assert.IsTrue(setup.Brain.OnWorkerCollectorMissionAssigned(assignment));
        AssertCurrent(setup, RobotTaskType.WorkerCollectorMoveToCube, assignment);
        CollectionAssert.AreEqual(new[] { "Cancel", "MoveToCube" }, setup.Body.Commands);

        setup.Body.Commands.Clear();
        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.TargetApproach(assignment, 1)));
        AssertCurrent(setup, RobotTaskType.WorkerCollectorGrabCube, assignment);
        CollectionAssert.AreEqual(new[] { "Cancel", "Grab" }, setup.Body.Commands);

        setup.Body.Commands.Clear();
        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.Cargo(assignment, 2, secured: true)));
        AssertCurrent(setup, RobotTaskType.WorkerCollectorMoveToGarage, assignment);
        CollectionAssert.AreEqual(new[] { "Cancel", "MoveToDropOff" }, setup.Body.Commands);

        setup.Body.Commands.Clear();
        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.DropOffApproach(assignment, 3)));
        AssertCurrent(setup, RobotTaskType.WorkerCollectorDepositCube, assignment);
        CollectionAssert.AreEqual(new[] { "Cancel", "Deposit" }, setup.Body.Commands);
    }

    [Test]
    public void TargetLoss_ClearsMissionAndReturnsToFindCube()
    {
        Pipeline setup = CreatePipeline();
        WorkerCollectorMissionAssignment assignment = CreateAssignment(setup.Body, 2);
        Assert.IsTrue(setup.Brain.OnWorkerCollectorMissionAssigned(assignment));
        setup.Body.Commands.Clear();

        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.TargetLost(assignment, 1)));

        Assert.IsNull(setup.Memory.Snapshot.WorkerCollector.Assignment);
        AssertCurrent(setup, RobotTaskType.WorkerCollectorFindCube, null);
        Assert.Contains("Find", setup.Body.Commands);
    }

    [Test]
    public void CompletedNinthDelivery_WaitsForBatch_Rests_ThenResumesFinding()
    {
        Pipeline setup = CreatePipeline();
        WorkerCollectorMissionAssignment baseAssignment = CreateAssignment(setup.Body, 3);
        GameObject restObject = CreateObject("Rest");
        WorkerCollectorSpawnRestProvider rest = restObject.AddComponent<WorkerCollectorSpawnRestProvider>();
        rest.Configure(null, restObject.transform, null, restObject.transform, 5f);
        WorkerCollectorMissionAssignment assignment = new WorkerCollectorMissionAssignment(
            baseAssignment.MissionId, baseAssignment.Source, baseAssignment.DropOff, rest,
            baseAssignment.Target, baseAssignment.Claim, default);
        Assert.IsTrue(setup.Brain.OnWorkerCollectorMissionAssigned(assignment));

        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.Delivery(assignment, 1, waitingForBatch: true)));
        AssertCurrent(setup, RobotTaskType.WorkerCollectorWaitForBatch, assignment);

        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.BatchProcessed(assignment, 2)));
        AssertCurrent(setup, RobotTaskType.WorkerCollectorMoveToRest, assignment);

        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.RestApproach(assignment, 3)));
        AssertCurrent(setup, RobotTaskType.WorkerCollectorRest, assignment);
        Assert.Greater(setup.Memory.Snapshot.WorkerCollector.RestUntil, Time.time);

        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.RestFinished(assignment, 4)));
        AssertCurrent(setup, RobotTaskType.WorkerCollectorFindCube, null);
    }

    [Test]
    public void InterruptedFullBatch_ReturnsToCubeSearchWithoutResting()
    {
        Pipeline setup = CreatePipeline();
        WorkerCollectorMissionAssignment assignment = CreateAssignment(setup.Body, 4);
        Assert.IsTrue(setup.Brain.OnWorkerCollectorMissionAssigned(assignment));
        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.Delivery(assignment, 1, waitingForBatch: true)));
        AssertCurrent(setup, RobotTaskType.WorkerCollectorWaitForBatch, assignment);

        Assert.IsTrue(setup.Brain.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.BatchInterrupted(assignment, 2)));

        Assert.IsFalse(setup.Memory.Snapshot.WorkerCollector.RestApproachReached);
        AssertCurrent(setup, RobotTaskType.WorkerCollectorFindCube, null);
    }

    private Pipeline CreatePipeline()
    {
        GameObject root = CreateObject("WorkerCollectorPipeline");
        root.SetActive(false);
        WorkerTaskBodySpy body = root.AddComponent<WorkerTaskBodySpy>();
        RobotMemoryNew memory = root.AddComponent<RobotMemoryNew>();
        RobotHeartNew heart = root.AddComponent<RobotHeartNew>();
        RobotBrainNew brain = root.AddComponent<RobotBrainNew>();
        heart.ConfigureRole(RobotRole.WorkerCollector, resetStack: true);
        root.SetActive(true);
        InvokePrivate(memory, "Awake");
        InvokePrivate(brain, "Awake");
        InvokePrivate(heart, "Awake");
        InvokePrivate(heart, "OnEnable");
        InvokePrivate(brain, "OnEnable");
        AssertCurrent(new Pipeline(body, memory, heart, brain), RobotTaskType.WorkerCollectorFindCube, null);
        return new Pipeline(body, memory, heart, brain);
    }

    private WorkerCollectorMissionAssignment CreateAssignment(UnityEngine.Object claimant, int id)
    {
        GameObject sourceObject = CreateObject("Source_" + id);
        WorkerCollectorWhiteCubeSourceProvider source =
            sourceObject.AddComponent<WorkerCollectorWhiteCubeSourceProvider>();
        GameObject dropOffObject = CreateObject("DropOff_" + id);
        WorkerCollectorDropOffProvider dropOff = dropOffObject.AddComponent<WorkerCollectorDropOffProvider>();
        WhiteCubeCargo cargo = CreateCargo("Cargo_" + id);
        cargo.transform.SetParent(sourceObject.transform);
        Assert.IsTrue(cargo.TryClaim(claimant, out WhiteCubeClaim claim));
        return new WorkerCollectorMissionAssignment(id, source, dropOff, cargo, claim);
    }

    private WhiteCubeCargo CreateCargo(string name)
    {
        GameObject go = CreatePickup(name);
        return go.AddComponent<WhiteCubeCargo>();
    }

    private GameObject CreatePickup(string name)
    {
        GameObject go = CreateObject(name);
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<TargetJoint2D>();
        go.AddComponent<BoxCollider2D>();
        CubePickup pickup = go.AddComponent<CubePickup>();
        InvokePrivate(pickup, "Awake");
        return go;
    }

    private GameObject CreateObject(string name)
    {
        GameObject go = new GameObject(name);
        created.Add(go);
        return go;
    }

    private static void AssertCurrent(
        Pipeline setup, RobotTaskType type, WorkerCollectorMissionAssignment assignment)
    {
        Assert.IsNotNull(setup.Heart.CurrentTask);
        Assert.AreEqual(type, setup.Heart.CurrentTask.Type);
        Assert.AreSame(assignment, setup.Heart.CurrentTask.Payload);
        RobotTaskStackNew stack = GetPrivateField<RobotTaskStackNew>(setup.Heart, "taskStack");
        Assert.AreEqual(1, stack.Tasks.Count);
    }

    private static T GetPrivateField<T>(object owner, string fieldName)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return (T)field.GetValue(owner);
    }

    private static void SetPrivateField(object owner, string fieldName, object value)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        field.SetValue(owner, value);
    }

    private static void InvokePrivate(object owner, string methodName)
    {
        MethodInfo method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(owner, null);
    }

    private static T InvokePrivate<T>(object owner, string methodName, params object[] arguments)
    {
        MethodInfo method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return (T)method.Invoke(owner, arguments);
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i].name == objectName)
                return descendants[i];
        }
        Assert.Fail($"Could not find '{objectName}'.");
        return null;
    }

    private sealed class WorkerTaskBodySpy : MonoBehaviour, IWorkerCollectorTaskBody
    {
        public List<string> Commands { get; } = new List<string>();
        public void FindCube() => Commands.Add("Find");
        public void BeginMoveToCube(WorkerCollectorMissionAssignment assignment) => Commands.Add("MoveToCube");
        public void GrabCube(WorkerCollectorMissionAssignment assignment) => Commands.Add("Grab");
        public void BeginMoveToDropOff(WorkerCollectorMissionAssignment assignment) => Commands.Add("MoveToDropOff");
        public void DepositCube(WorkerCollectorMissionAssignment assignment) => Commands.Add("Deposit");
        public void WaitAtDropOff(WorkerCollectorMissionAssignment assignment) => Commands.Add("Wait");
        public void BeginMoveToRest(WorkerCollectorMissionAssignment assignment) => Commands.Add("MoveToRest");
        public void Rest(WorkerCollectorMissionAssignment assignment) => Commands.Add("Rest");
        public void CancelCurrentCommand(WorkerCollectorMissionAssignment assignment) => Commands.Add("Cancel");
        public void StopAllActuators() => Commands.Add("Stop");
    }

    private sealed class PathMoverSpy : IMover
    {
        public float Horizontal { get; private set; }
        public void SetMovement(float direction) => Horizontal = direction;
        public void SetVerticalMovement(float direction) { }
    }

    private sealed class PathQueriesStub : IWaypointQueries
    {
        private readonly List<RoomWaypoint> waypoints;

        public PathQueriesStub(params RoomWaypoint[] waypoints)
        {
            this.waypoints = new List<RoomWaypoint>(waypoints);
        }

        public event Action<RoomWaypoint, Vector2> OnClosestWaypointToPlayerChanged
        {
            add { }
            remove { }
        }

        public RoomWaypoint ClosestWaypointToPlayer => waypoints[0];
        public List<RoomWaypoint> GetAllWaypoints() => new List<RoomWaypoint>(waypoints);
        public List<RoomWaypoint> GetActiveWaypoints() => new List<RoomWaypoint>(waypoints);
        public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end) =>
            new List<RoomWaypoint> { start, end };
        public RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false) =>
            waypoints[0];
        public RoomWaypoint GetEndPoint() => waypoints[waypoints.Count - 1];
        public RoomWaypoint GetStartPoint() => waypoints[0];
        public void UpdateClosestWaypointToPlayer(Vector2 playerPosition) { }
    }

    private readonly struct Pipeline
    {
        public Pipeline(WorkerTaskBodySpy body, RobotMemoryNew memory, RobotHeartNew heart, RobotBrainNew brain)
        {
            Body = body;
            Memory = memory;
            Heart = heart;
            Brain = brain;
        }
        public WorkerTaskBodySpy Body { get; }
        public RobotMemoryNew Memory { get; }
        public RobotHeartNew Heart { get; }
        public RobotBrainNew Brain { get; }
    }
}
