using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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

    private static void InvokePrivate(object owner, string methodName)
    {
        MethodInfo method = owner.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(owner, null);
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
