using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CollectorRobotPipelineTests
{
    private readonly List<GameObject> createdObjects = new List<GameObject>();
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
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                UnityEngine.Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
        RobotNewPipelineRuntime.Mode = previousMode;
        RobotNewPipelineRuntime.DriveGameplayInShadow = previousDriveGameplayInShadow;
    }

    [Test]
    public void SerializedEnums_AppendCollectorWithoutChangingExistingValues()
    {
        Assert.AreEqual(0, (int)RobotRole.Worker);
        Assert.AreEqual(1, (int)RobotRole.SecurityGuard);
        Assert.AreEqual(2, (int)RobotRole.WorkerSpawner);
        Assert.AreEqual(3, (int)RobotRole.Follower);
        Assert.AreEqual(4, (int)RobotRole.Boss);
        Assert.AreEqual(5, (int)RobotRole.Collector);
        Assert.AreEqual(18, (int)RobotTaskType.Dead);
        Assert.AreEqual(19, (int)RobotTaskType.CollectorStandby);
        Assert.AreEqual(25, (int)RobotTaskType.CollectorDock);
        Assert.AreEqual(14, (int)MemoryChangeType.Normal);
        Assert.AreEqual(15, (int)MemoryChangeType.CollectorMissionAssigned);
    }

    [Test]
    public void TaskStack_ReplacesSequentialCollectorFamilyInsteadOfGrowing()
    {
        var stack = new RobotTaskStackNew();
        var assignment = CreateMission(101);

        stack.PushOrRefresh(new RobotTask(RobotTaskType.CollectorStandby));
        stack.ReplaceCollectorFamily(new RobotTask(RobotTaskType.CollectorLaunch, assignment));
        stack.ReplaceCollectorFamily(new RobotTask(RobotTaskType.CollectorFlyToTarget, assignment));
        stack.ReplaceCollectorFamily(new RobotTask(RobotTaskType.CollectorGatherCargo, assignment));

        Assert.AreEqual(1, stack.Tasks.Count);
        Assert.AreEqual(RobotTaskType.CollectorGatherCargo, stack.Current.Type);
        Assert.AreSame(assignment, stack.Current.Payload);
    }

    [Test]
    public void Memory_AssignmentIsAtomicIdempotentAndRejectsStaleObservations()
    {
        PipelineSetup setup = CreateCollectorPipeline("Collector_AtomicMemory");
        CollectorMissionAssignment assignment = CreateMission(102);
        MemoryChangeType lastChange = MemoryChangeType.Normal;
        int eventCount = 0;
        setup.Memory.OnMemoryChanged += change =>
        {
            eventCount++;
            lastChange = change.Type;
        };

        Assert.IsTrue(setup.Brain.OnCollectorMissionAssigned(assignment));
        Assert.IsFalse(setup.Brain.OnCollectorMissionAssigned(assignment));

        Assert.AreEqual(1, eventCount);
        Assert.AreEqual(MemoryChangeType.CollectorMissionAssigned, lastChange);
        Assert.AreSame(assignment, setup.Memory.Snapshot.Collector.Assignment);
        Assert.IsFalse(setup.Memory.Snapshot.Collector.LaunchExitReached);
        Assert.IsFalse(setup.Memory.Snapshot.Collector.CargoSecure);
        Assert.IsFalse(setup.Memory.Snapshot.Collector.DockAccessGranted);

        var differentHandle = new CollectorMissionAssignment(
            assignment.MissionId,
            assignment.Home,
            assignment.Target,
            assignment.Claim);
        Assert.IsFalse(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.LaunchExit(differentHandle, commandToken: 1)));
        Assert.IsFalse(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.LaunchExit(assignment, commandToken: 0)));
        Assert.AreEqual(1, eventCount);

        assignment.Target.ReleaseClaim(assignment.Claim);
        Assert.IsFalse(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.TargetApproach(assignment, commandToken: 1)));
        Assert.AreEqual(1, eventCount);
    }

    [Test]
    public void BrainHeartTasks_RunNormalCollectorSequenceWithOneStablePayload()
    {
        PipelineSetup setup = CreateCollectorPipeline("Collector_NormalSequence");
        CollectorMissionAssignment assignment = CreateMission(103);
        setup.Body.Commands.Clear();

        Assert.IsTrue(setup.Brain.OnCollectorMissionAssigned(assignment));
        AssertCurrent(setup, RobotTaskType.CollectorLaunch, assignment, expectedStackCount: 1);
        CollectionAssert.AreEqual(new[] { "StopAll", "BeginLaunch" }, setup.Body.Commands);

        setup.Body.Commands.Clear();
        Assert.IsTrue(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.LaunchExit(assignment, commandToken: 1)));
        AssertCurrent(setup, RobotTaskType.CollectorFlyToTarget, assignment, expectedStackCount: 1);
        CollectionAssert.AreEqual(new[] { "Cancel", "BeginOutbound" }, setup.Body.Commands);

        setup.Body.Commands.Clear();
        Assert.IsTrue(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.TargetApproach(assignment, commandToken: 2)));
        AssertCurrent(setup, RobotTaskType.CollectorGatherCargo, assignment, expectedStackCount: 1);
        CollectionAssert.AreEqual(new[] { "Cancel", "BeginGathering" }, setup.Body.Commands);

        setup.Body.Commands.Clear();
        Assert.IsTrue(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.Cargo(
                assignment,
                commandToken: 3,
                requiredPartCount: 1,
                securedPartCount: 1,
                cargoSecure: true,
                cargoLost: false)));
        AssertCurrent(setup, RobotTaskType.CollectorReturnHome, assignment, expectedStackCount: 1);
        CollectionAssert.AreEqual(new[] { "Cancel", "BeginReturn" }, setup.Body.Commands);

        setup.Body.Commands.Clear();
        Assert.IsTrue(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.Cargo(
                assignment,
                commandToken: 4,
                requiredPartCount: 1,
                securedPartCount: 0,
                cargoSecure: false,
                cargoLost: true)));
        AssertCurrent(setup, RobotTaskType.CollectorGatherCargo, assignment, expectedStackCount: 1);

        Assert.IsTrue(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.Cargo(
                assignment,
                commandToken: 5,
                requiredPartCount: 1,
                securedPartCount: 1,
                cargoSecure: true,
                cargoLost: false)));
        AssertCurrent(setup, RobotTaskType.CollectorReturnHome, assignment, expectedStackCount: 1);

        setup.Body.Commands.Clear();
        Assert.IsTrue(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.DockApproach(assignment, commandToken: 6)));
        AssertCurrent(setup, RobotTaskType.CollectorReturnHome, assignment, expectedStackCount: 1);
        Assert.IsEmpty(setup.Body.Commands, "Dock approach alone must keep the stable Return task.");

        Assert.IsTrue(setup.Brain.OnCollectorDockAccessChanged(assignment, granted: true));
        AssertCurrent(setup, RobotTaskType.CollectorDock, assignment, expectedStackCount: 1);

        Assert.IsTrue(setup.Brain.OnCollectorIntakeConfirmed(assignment));
        AssertCurrent(setup, RobotTaskType.CollectorStandby, null, expectedStackCount: 1);
    }

    [Test]
    public void Brain_InvalidTargetAbortsAndAllowsClaimlessAbortIntake()
    {
        PipelineSetup setup = CreateCollectorPipeline("Collector_InvalidTarget");
        CollectorMissionAssignment assignment = CreateMission(104);
        Assert.IsTrue(setup.Brain.OnCollectorMissionAssigned(assignment));

        Assert.IsTrue(setup.Brain.OnCollectorTargetInvalidated(assignment));
        assignment.Target.ReleaseClaim(assignment.Claim);
        AssertCurrent(setup, RobotTaskType.CollectorAbortAndReturn, assignment, expectedStackCount: 1);

        Assert.IsTrue(setup.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.DockApproach(assignment, commandToken: 1)));
        Assert.IsTrue(setup.Brain.OnCollectorDockAccessChanged(assignment, granted: true));
        AssertCurrent(setup, RobotTaskType.CollectorDock, assignment, expectedStackCount: 1);

        Assert.IsTrue(setup.Brain.OnCollectorIntakeConfirmed(assignment));
        AssertCurrent(setup, RobotTaskType.CollectorStandby, null, expectedStackCount: 1);
    }

    [Test]
    public void Brain_CancellationAndFlightFaultEachSelectAbortReturn()
    {
        PipelineSetup cancelled = CreateCollectorPipeline("Collector_Cancelled");
        CollectorMissionAssignment cancelledAssignment = CreateMission(108);
        Assert.IsTrue(cancelled.Brain.OnCollectorMissionAssigned(cancelledAssignment));
        Assert.IsTrue(cancelled.Brain.OnCollectorMissionCancelled(cancelledAssignment));
        AssertCurrent(
            cancelled,
            RobotTaskType.CollectorAbortAndReturn,
            cancelledAssignment,
            expectedStackCount: 1);

        PipelineSetup faulted = CreateCollectorPipeline("Collector_FlightFault");
        CollectorMissionAssignment faultedAssignment = CreateMission(109);
        Assert.IsTrue(faulted.Brain.OnCollectorMissionAssigned(faultedAssignment));
        Assert.IsTrue(faulted.Brain.OnCollectorBodyObservation(
            CollectorBodyObservation.FlightFault(faultedAssignment, commandToken: 1)));
        AssertCurrent(
            faulted,
            RobotTaskType.CollectorAbortAndReturn,
            faultedAssignment,
            expectedStackCount: 1);
        Assert.IsTrue(faulted.Brain.CurrentOptions.HasFlag(BrainOption.CollectorFlightFault));
    }

    [Test]
    public void Brain_DeadOverridesCollectorAndSkipsGenericMachineOptions()
    {
        PipelineSetup setup = CreateCollectorPipeline("Collector_DeadOverride");
        CollectorMissionAssignment assignment = CreateMission(105);
        Assert.IsTrue(setup.Brain.OnCollectorMissionAssigned(assignment));

        setup.Memory.SetDead(true);

        Assert.IsTrue(setup.Brain.TryGetCurrentPlan(out BrainOption options, out RobotTask task));
        Assert.IsTrue(options.HasFlag(BrainOption.Dead));
        Assert.IsFalse(options.HasFlag(BrainOption.NeedMachine));
        Assert.IsFalse(options.HasFlag(BrainOption.MachineUnavailable));
        Assert.AreEqual(RobotTaskType.Dead, task.Type);
        Assert.AreEqual(RobotTaskType.Dead, setup.Heart.CurrentTask.Type);
        Assert.AreEqual("StopAll", setup.Body.Commands[setup.Body.Commands.Count - 1]);
    }

    [Test]
    public void Heart_DisableExitsOnceAndReenableEntersRetainedCollectorTask()
    {
        PipelineSetup setup = CreateCollectorPipeline("Collector_DisableLifecycle");
        CollectorMissionAssignment assignment = CreateMission(106);
        Assert.IsTrue(setup.Brain.OnCollectorMissionAssigned(assignment));
        setup.Body.Commands.Clear();

        setup.Root.SetActive(false);
        InvokePrivate(setup.Heart, "OnDisable");
        InvokePrivate(setup.Brain, "OnDisable");
        CollectionAssert.AreEqual(new[] { "StopAll" }, setup.Body.Commands);

        setup.Root.SetActive(false);
        CollectionAssert.AreEqual(new[] { "StopAll" }, setup.Body.Commands);

        setup.Root.SetActive(true);
        InvokePrivate(setup.Heart, "OnEnable");
        InvokePrivate(setup.Brain, "OnEnable");
        CollectionAssert.AreEqual(new[] { "StopAll", "BeginLaunch" }, setup.Body.Commands);
    }

    [Test]
    public void Brain_PublicationSuspensionRejectsIngressUntilPoolGateIsReleased()
    {
        PipelineSetup setup = CreateCollectorPipeline("Collector_Suspension");
        CollectorMissionAssignment assignment = CreateMission(107);

        setup.Brain.SetPlanPublicationSuspended(true);
        Assert.IsFalse(setup.Brain.OnCollectorMissionAssigned(assignment));
        Assert.IsNull(setup.Memory.Snapshot.Collector.Assignment);

        setup.Brain.ResetPlanningCache();
        setup.Brain.SetPlanPublicationSuspended(false);
        Assert.IsTrue(setup.Brain.OnCollectorMissionAssigned(assignment));
        AssertCurrent(setup, RobotTaskType.CollectorLaunch, assignment, expectedStackCount: 1);
    }

    private PipelineSetup CreateCollectorPipeline(string name)
    {
        GameObject root = CreateObject(name);
        root.SetActive(false);
        var body = root.AddComponent<CollectorTaskBodySpy>();
        var memory = root.AddComponent<RobotMemoryNew>();
        var heart = root.AddComponent<RobotHeartNew>();
        var brain = root.AddComponent<RobotBrainNew>();
        heart.ConfigureRole(RobotRole.Collector, resetStack: true);
        root.SetActive(true);
        InvokePrivate(memory, "Awake");
        InvokePrivate(brain, "Awake");
        InvokePrivate(heart, "Awake");
        InvokePrivate(heart, "OnEnable");
        InvokePrivate(brain, "OnEnable");

        Assert.AreEqual(RobotTaskType.CollectorStandby, heart.CurrentTask.Type);
        return new PipelineSetup(root, body, memory, heart, brain);
    }

    private CollectorMissionAssignment CreateMission(int missionId)
    {
        GameObject homeObject = CreateObject("CollectorHome_" + missionId);
        var home = homeObject.AddComponent<SpawnRobotCollectorController>();

        GameObject targetObject = CreateObject("DeadTarget_" + missionId);
        var state = targetObject.AddComponent<RobotStateController>();
        GameObject part = new GameObject("RequiredPart");
        part.transform.SetParent(targetObject.transform);
        var rigidbody = part.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Dynamic;
        rigidbody.simulated = true;
        part.AddComponent<BoxCollider2D>();
        state.SetInitialDeadState();

        DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(state);
        Assert.IsNotNull(target);
        Assert.IsTrue(target.TryClaim(missionId, home, out CollectorTargetClaim claim));
        return new CollectorMissionAssignment(missionId, home, target, claim);
    }

    private static void AssertCurrent(
        PipelineSetup setup,
        RobotTaskType expectedType,
        CollectorMissionAssignment expectedAssignment,
        int expectedStackCount)
    {
        Assert.IsNotNull(setup.Heart.CurrentTask);
        Assert.AreEqual(expectedType, setup.Heart.CurrentTask.Type);
        Assert.AreSame(expectedAssignment, setup.Heart.CurrentTask.Payload);

        RobotTaskStackNew stack = GetPrivateField<RobotTaskStackNew>(setup.Heart, "taskStack");
        Assert.AreEqual(expectedStackCount, stack.Tasks.Count);
    }

    private GameObject CreateObject(string name)
    {
        var gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static T GetPrivateField<T>(object owner, string fieldName)
    {
        FieldInfo field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Expected private field '" + fieldName + "'.");
        return (T)field.GetValue(owner);
    }

    private static void InvokePrivate(object owner, string methodName)
    {
        MethodInfo method = owner.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Expected private method '" + methodName + "'.");
        method.Invoke(owner, null);
    }

    private sealed class CollectorTaskBodySpy : MonoBehaviour, ICollectorTaskBody
    {
        public event Action<CollectorBodyObservation> OnObservation
        {
            add { }
            remove { }
        }

        public List<string> Commands { get; } = new List<string>();

        public void BeginLaunch(CollectorMissionAssignment assignment) => Commands.Add("BeginLaunch");
        public void BeginOutbound(CollectorMissionAssignment assignment) => Commands.Add("BeginOutbound");
        public void BeginGathering(CollectorMissionAssignment assignment) => Commands.Add("BeginGathering");
        public void BeginReturn(CollectorMissionAssignment assignment) => Commands.Add("BeginReturn");
        public void BeginAbortReturn(CollectorMissionAssignment assignment) => Commands.Add("BeginAbortReturn");
        public void BeginDocking(CollectorMissionAssignment assignment) => Commands.Add("BeginDocking");
        public void CancelCurrentCommand(CollectorMissionAssignment assignment) => Commands.Add("Cancel");
        public void StopAllActuators() => Commands.Add("StopAll");
        public void ResetPhysicalState() => Commands.Add("Reset");
    }

    private readonly struct PipelineSetup
    {
        public PipelineSetup(
            GameObject root,
            CollectorTaskBodySpy body,
            RobotMemoryNew memory,
            RobotHeartNew heart,
            RobotBrainNew brain)
        {
            Root = root;
            Body = body;
            Memory = memory;
            Heart = heart;
            Brain = brain;
        }

        public GameObject Root { get; }
        public CollectorTaskBodySpy Body { get; }
        public RobotMemoryNew Memory { get; }
        public RobotHeartNew Heart { get; }
        public RobotBrainNew Brain { get; }
    }
}
