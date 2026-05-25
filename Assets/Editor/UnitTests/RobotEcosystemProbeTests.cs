using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class RobotEcosystemProbeTests
{
    [SetUp]
    public void SetUp()
    {
        RobotNewPipelineRuntime.EnableEcosystemProbe = true;
        RobotNewPipelineRuntime.WorkerCycleValidationMode = true;
        RobotEcosystemProbe.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        RobotNewPipelineRuntime.WorkerCycleValidationMode = true;
        RobotEcosystemProbe.Reset();
    }

    [Test]
    public void SpawnWiring_MemorySeedsWaypointsAndLastVisited()
    {
        var robot = new GameObject("Robot_MemorySeed");
        var memory = robot.AddComponent<RobotMemoryNew>();

        var wpA = CreateWaypoint("WP_A", WaypointType.Work, true, new Vector3(1f, 2f, 0f));
        var wpB = CreateWaypoint("WP_B", WaypointType.Rest, false, new Vector3(3f, 4f, 0f));

        memory.InitializeWaypointAvailability(new[] { wpA, wpB });
        memory.SetLastVisitedPoint(wpA);

        Assert.AreEqual(wpA, memory.LastVisitedPoint);
        Assert.IsNotNull(memory.AllAvailableWaypoints);
        Assert.AreEqual(2, memory.AllAvailableWaypoints.Count);
        Assert.IsTrue(memory.AllAvailableWaypoints[wpA]);
        Assert.IsFalse(memory.AllAvailableWaypoints[wpB]);
    }

    [Test]
    public void BrainMachineEvent_IsCapturedByProbe()
    {
        var brain = CreateRobotWithBrain("Robot_BrainMachine").brain;

        brain.OnMachineStateEvent(null, false);

        Assert.Greater(RobotEcosystemProbe.GetCallCount("Brain.OnMachineStateEvent"), 0);
    }

    [Test]
    public void SecurityDispatch_IsCallableAndCaptured()
    {
        var brain = CreateRobotWithBrain("Robot_SecurityDispatch").brain;

        brain.OnSecurityDispatch(null);

        Assert.Greater(RobotEcosystemProbe.GetCallCount("Brain.OnSecurityDispatch"), 0);
    }

    [Test]
    public void SlotGating_WorkerSlotRequiresWorkTask_AndRestSlotRejectsWorkTask()
    {
        var setup = CreateRobotWithBrain("Robot_WorkerSlot");
        var brain = setup.brain;
        var heart = setup.heart;

        heart.ResetIntentStack(repopulateDefaultTask: true);
        heart.QueueTask(new RobotTask(RobotTaskType.WorkAtMachine));

        var workerSlotGo = new GameObject("WorkerSlotGO");
        var workerSlot = workerSlotGo.AddComponent<WorkerSlot>();
        var workerMachineGo = new GameObject("WorkerMachine");
        var factoryMachine = workerMachineGo.AddComponent<FactoryMachine>();
        SetPrivateField(workerSlot, "machine", factoryMachine);

        InvokePrivate(workerSlot, "OnTriggerEnter2D", setup.collider);
        Assert.IsTrue(factoryMachine.IsOccupied, "WorkerSlot should attach directly when task is WorkAtMachine.");

        var restSlotGo = new GameObject("RestSlotGO");
        var restSlot = restSlotGo.AddComponent<RestingSlot>();
        var restMachineGo = new GameObject("RestMachine");
        var restingMachine = restMachineGo.AddComponent<RestingMachine>();
        SetPrivateField(restSlot, "machine", restingMachine);

        InvokePrivate(restSlot, "OnTriggerEnter2D", setup.collider);
        Assert.IsNull(restingMachine.CurrentWorker, "RestingSlot should reject when task is not Rest.");
    }

    [Test]
    public void BossSpawnMetadata_UsesEndWaypointDataInProbe()
    {
        var owner = new GameObject("BossProbeOwner").AddComponent<RobotBrainNew>();
        var endWaypoint = CreateWaypoint("EndWaypoint", WaypointType.Center, true, new Vector3(10f, 0f, 0f));

        RobotEcosystemProbe.RecordSpawn(owner, RobotRole.Boss, endWaypoint);
        var snapshot = RobotEcosystemProbe.GetSnapshot();
        string robotId = owner.name + "#" + owner.GetInstanceID();

        Assert.IsTrue(snapshot.RobotRoles.ContainsKey(robotId));
        Assert.AreEqual("Boss", snapshot.RobotRoles[robotId]);
        Assert.IsTrue(snapshot.RobotSpawnWaypoints[robotId].Contains("Center@"));
    }

    [Test]
    public void ProbeSummary_CallCoverageTracksCalledAndNotCalled()
    {
        var brain = CreateRobotWithBrain("Robot_Coverage").brain;
        brain.OnMachineStateEvent(null, false);

        RobotEcosystemProbe.DumpSummary("EditModeCoverage");

        Assert.Greater(RobotEcosystemProbe.GetCallCount("Brain.OnMachineStateEvent"), 0);
        Assert.AreEqual(0, RobotEcosystemProbe.GetCallCount("Brain.OnPerceptionChanged"));
    }

    [Test]
    public void KnownCurrentBehavior_GuardStationCheckReturnsFalse()
    {
        var brain = CreateRobotWithBrain("Robot_GuardStation").brain;

        MethodInfo method = typeof(MachineSecurityManager).GetMethod(
            "IsGuardStationedAtSecurityMachine",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "Expected private static method IsGuardStationedAtSecurityMachine.");

        bool result = (bool)method.Invoke(null, new object[] { brain });
        Assert.IsFalse(result);
    }

    [Test]
    public void WorkerPlan_WhenConnectedFromRest_PlansRestTask()
    {
        var setup = CreateRobotWithBrain("Robot_WorkerConnectedRest");
        var memory = setup.brain.Memory;
        Assert.IsNotNull(memory);

        var restWaypoint = CreateWaypoint("RestWP", WaypointType.Rest, true, Vector3.zero);
        var workWaypoint = CreateWaypoint("WorkWP", WaypointType.Work, true, Vector3.right);
        memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        memory.SetLastVisitedPoint(restWaypoint);
        memory.ChangeConnectionToMachine(true);

        bool ok = setup.brain.TryGetCurrentPlan(out _, out var task);
        Assert.IsTrue(ok);
        Assert.IsNotNull(task);
        Assert.AreEqual(RobotTaskType.Rest, task.Type);
    }

    [Test]
    public void WorkerPlan_WhenDisconnectedFromRest_PrefersWorkWaypoint()
    {
        var setup = CreateRobotWithBrain("Robot_WorkerDisconnectedRest");
        var memory = setup.brain.Memory;
        Assert.IsNotNull(memory);

        var restWaypoint = CreateWaypoint("RestWP", WaypointType.Rest, true, Vector3.zero);
        var workWaypoint = CreateWaypoint("WorkWP", WaypointType.Work, true, Vector3.right);
        memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        memory.SetLastVisitedPoint(restWaypoint);
        memory.ChangeConnectionToMachine(false);

        bool ok = setup.brain.TryGetCurrentPlan(out _, out var task);
        Assert.IsTrue(ok);
        Assert.IsNotNull(task);
        Assert.AreEqual(RobotTaskType.GoToMachine, task.Type);
        Assert.AreEqual(workWaypoint, task.Payload);
    }

    [Test]
    public void WorkerPlan_WhenConnectedFromWork_PlansWorkAtMachine()
    {
        var setup = CreateRobotWithBrain("Robot_WorkerConnectedWork");
        var memory = setup.brain.Memory;
        Assert.IsNotNull(memory);

        var restWaypoint = CreateWaypoint("RestWP", WaypointType.Rest, true, Vector3.zero);
        var workWaypoint = CreateWaypoint("WorkWP", WaypointType.Work, true, Vector3.right);
        memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        memory.SetLastVisitedPoint(workWaypoint);
        memory.ChangeConnectionToMachine(true);

        bool ok = setup.brain.TryGetCurrentPlan(out _, out var task);
        Assert.IsTrue(ok);
        Assert.IsNotNull(task);
        Assert.AreEqual(RobotTaskType.WorkAtMachine, task.Type);
    }

    [Test]
    public void WorkerPlan_WhenDisconnectedFromWork_PrefersRestWaypoint()
    {
        var setup = CreateRobotWithBrain("Robot_WorkerDisconnectedWork");
        var memory = setup.brain.Memory;
        Assert.IsNotNull(memory);

        var restWaypoint = CreateWaypoint("RestWP", WaypointType.Rest, true, Vector3.zero);
        var workWaypoint = CreateWaypoint("WorkWP", WaypointType.Work, true, Vector3.right);
        memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        memory.SetLastVisitedPoint(workWaypoint);
        memory.ChangeConnectionToMachine(false);

        bool ok = setup.brain.TryGetCurrentPlan(out _, out var task);
        Assert.IsTrue(ok);
        Assert.IsNotNull(task);
        Assert.AreEqual(RobotTaskType.GoToMachine, task.Type);
        Assert.AreEqual(restWaypoint, task.Payload);
    }

    [Test]
    public void WorkerPlan_DoesNotIdle_WhenMachineWaypointsExistEvenIfUnavailable()
    {
        var setup = CreateRobotWithBrain("Robot_WorkerNoIdleWhenUnavailable");
        var memory = setup.brain.Memory;
        Assert.IsNotNull(memory);

        var restWaypoint = CreateWaypoint("RestWP", WaypointType.Rest, false, Vector3.zero);
        var workWaypoint = CreateWaypoint("WorkWP", WaypointType.Work, false, Vector3.right);
        memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        memory.SetLastVisitedPoint(restWaypoint);
        memory.ChangeConnectionToMachine(false);

        bool ok = setup.brain.TryGetCurrentPlan(out _, out var task);
        Assert.IsTrue(ok);
        Assert.IsNotNull(task);
        Assert.AreNotEqual(RobotTaskType.Idle, task.Type);
        Assert.AreEqual(RobotTaskType.GoToMachine, task.Type);
    }

    [Test]
    public void WorkerPlan_DesiredMachineType_HardFiltersBeforeCycleFallback()
    {
        var setup = CreateRobotWithBrain("Robot_WorkerDesiredType");
        var memory = setup.brain.Memory;
        Assert.IsNotNull(memory);

        var restWaypoint = CreateWaypoint("RestWP_HardFilter", WaypointType.Rest, true, Vector3.zero);
        var workWaypoint = CreateWaypoint("WorkWP_HardFilter", WaypointType.Work, true, Vector3.right);
        memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        memory.SetLastVisitedPoint(workWaypoint);
        memory.ChangeConnectionToMachine(false);
        memory.SetDesiredMachineType(MachineType.WorkStation);

        bool ok = setup.brain.TryGetCurrentPlan(out _, out var task);
        Assert.IsTrue(ok);
        Assert.IsNotNull(task);
        Assert.AreEqual(RobotTaskType.GoToMachine, task.Type);
        Assert.AreEqual(workWaypoint, task.Payload, "DesiredMachineType should be the primary planner input.");
    }

    [Test]
    public void MachineWorkerManager_PowerOn_RestoresWorkerWaypointFromMachineService()
    {
        var workWaypoint = CreateWaypoint("WorkWP_Restored", WaypointType.Work, true, Vector3.right);
        var restWaypoint = CreateWaypoint("RestWP_Restored", WaypointType.Rest, true, Vector3.zero);

        var workerA = CreateRobotWithBrain("Robot_WorkerRestore_A");
        var workerB = CreateRobotWithBrain("Robot_WorkerRestore_B");
        PrepareWorkerForUnavailableWorkMachine(workerA.brain.Memory, workWaypoint, restWaypoint);
        PrepareWorkerForUnavailableWorkMachine(workerB.brain.Memory, workWaypoint, restWaypoint);

        AssertWorkerPlansWaypoint(workerA.brain, restWaypoint, "Worker should use fallback while work waypoint is unavailable.");
        AssertWorkerPlansWaypoint(workerB.brain, restWaypoint, "Worker should use fallback while work waypoint is unavailable.");

        var machineGo = new GameObject("WorkingDesk_ServiceWaypoint");
        machineGo.transform.position = Vector3.right * 1.2f;
        machineGo.AddComponent<BoxCollider2D>();
        var machine = machineGo.AddComponent<FactoryMachine>();
        machine.InitializeWaypointService(new FakeWaypointService(workWaypoint, restWaypoint));

        var managerGo = new GameObject("MachineWorkerManager_Restore");
        var manager = managerGo.AddComponent<MachineWorkerManager>();
        manager.RegisterMachine(machine);

        machine.PowerOff();
        machine.PowerOn();

        Assert.IsTrue(workerA.brain.Memory.AllAvailableWaypoints[workWaypoint]);
        Assert.IsTrue(workerB.brain.Memory.AllAvailableWaypoints[workWaypoint]);
        AssertWorkerPlansWaypoint(workerA.brain, workWaypoint, "Worker A should retarget the restored work waypoint.");
        AssertWorkerPlansWaypoint(workerB.brain, workWaypoint, "Worker B should retarget the restored work waypoint.");
        Assert.GreaterOrEqual(RobotEcosystemProbe.GetCallCount("Brain.MachineWorkerManager.NotifyWorkersMachinePoweredOn"), 2);
    }

    [Test]
    public void MachineWorkerManager_PowerOff_RetargetsWorkerTravelingToRestMachine()
    {
        var setup = CreateRobotWithBrain("Robot_TargetedRestPowerOff");

        var machineGo = new GameObject("RestDesk_TargetedPowerOff");
        var machine = machineGo.AddComponent<RestingMachine>();
        var restWaypoint = machineGo.AddComponent<RoomWaypoint>();
        restWaypoint.type = WaypointType.Rest;
        restWaypoint.IsAvailable = true;
        var workWaypoint = CreateWaypoint("WorkFallback_TargetedPowerOff", WaypointType.Work, true, Vector3.right);

        setup.brain.Memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        setup.brain.Memory.SetDesiredMachineType(MachineType.RestStation);
        setup.heart.ResetIntentStack(repopulateDefaultTask: true);
        setup.heart.QueueTask(new RobotTask(RobotTaskType.GoToMachine, restWaypoint));

        var managerGo = new GameObject("MachineWorkerManager_TargetedPowerOff");
        var manager = managerGo.AddComponent<MachineWorkerManager>();
        manager.RegisterMachine(machine);

        machine.PowerOff();

        Assert.IsFalse(setup.brain.Memory.AllAvailableWaypoints[restWaypoint], "A worker targeting the machine should learn immediately that it powered off.");
        AssertWorkerPlansWaypoint(setup.brain, workWaypoint, "A worker targeting an off rest machine should reroute without waiting for another slot entry.");
        Assert.GreaterOrEqual(RobotEcosystemProbe.GetCallCount("Brain.MachineWorkerManager.NotifyWorkersMachinePoweredOff"), 1);
    }

    [Test]
    public void WorkerPlan_TransientDetach_DoesNotForceNeedMachineLoop()
    {
        var setup = CreateRobotWithBrain("Robot_TransientDetach");
        var memory = setup.brain.Memory;
        Assert.IsNotNull(memory);

        var workWaypoint = CreateWaypoint("WorkWP_Transient", WaypointType.Work, true, Vector3.right);
        memory.InitializeWaypointAvailability(new[] { workWaypoint });
        memory.SetLastVisitedPoint(workWaypoint);
        memory.ChangeConnectionToMachine(true);

        memory.NotifyMachineSlotReleasedTransient();

        bool ok = setup.brain.TryGetCurrentPlan(out var options, out var task);
        Assert.IsTrue(ok);
        Assert.IsNotNull(task);
        Assert.IsFalse(options.HasFlag(BrainOption.NeedMachine), "Transient detach should not immediately mark NeedMachine.");
    }

    [Test]
    public void SecurityGuardPerception_AttackPlanUsesPlayerTransformPayload()
    {
        var setup = CreateRobotWithBrain("Robot_GuardAttackPayload");
        setup.heart.ConfigureRole(RobotRole.SecurityGuard, resetStack: true);
        var player = new GameObject("PlayerBody").transform;
        player.position = new Vector3(2f, 0f, 0f);

        setup.brain.OnPerceptionChanged(
            playerInDetectZone: true,
            playerInAttackZone: true,
            playerPosition: player.position,
            hasKnownPosition: true,
            playerTransform: player);

        bool ok = setup.brain.TryGetCurrentPlan(out var options, out var task);
        Assert.IsTrue(ok);
        Assert.IsTrue(options.HasFlag(BrainOption.CanAttack));
        Assert.IsNotNull(task);
        Assert.AreEqual(RobotTaskType.AttackTarget, task.Type);
        Assert.AreEqual(player, task.Payload, "AttackTarget must carry the live player Transform so RobotTaskNew can call TryStartAttack.");
    }

    [Test]
    public void FollowerPerception_UsesPlayerWaypointAndDoesNotAttack()
    {
        var setup = CreateRobotWithBrain("Robot_FollowerWaypointChase");
        setup.heart.ConfigureRole(RobotRole.Follower, resetStack: true);
        var playerWaypoint = CreateWaypoint("PlayerNearestWP", WaypointType.Center, true, new Vector3(5f, 0f, 0f));
        Vector3 playerPosition = new Vector3(5.5f, 0.5f, 0f);

        setup.brain.OnPerceptionChanged(
            playerInDetectZone: true,
            playerInAttackZone: true,
            playerPosition: playerPosition,
            hasKnownPosition: true,
            playerWaypoint: playerWaypoint);

        bool ok = setup.brain.TryGetCurrentPlan(out var options, out var task);
        Assert.IsTrue(ok);
        Assert.IsTrue(options.HasFlag(BrainOption.CanAttack));
        Assert.IsNotNull(task);
        Assert.AreEqual(RobotTaskType.ChasePlayer, task.Type);
        Assert.IsInstanceOf<RobotPlayerChaseTarget>(task.Payload);

        var target = (RobotPlayerChaseTarget)task.Payload;
        Assert.AreEqual(playerWaypoint, target.Waypoint);
        Assert.AreEqual(playerPosition, target.PlayerPosition);
    }

    [Test]
    public void FollowPlayerTriggerHandler_AwakeResolvesRobotComponentsFromParent()
    {
        var robot = new GameObject("Robot_WithChildTrigger");
        var memory = robot.AddComponent<RobotMemoryNew>();
        var heart = robot.AddComponent<RobotHeartNew>();
        var brain = robot.AddComponent<RobotBrainNew>();
        var child = new GameObject("TriggerHandlerChild");
        child.transform.SetParent(robot.transform);
        var handler = child.AddComponent<FollowPlayerTriggerHandler>();

        InvokePrivate(handler, "Awake");

        Assert.AreEqual(brain, GetPrivateField<RobotBrainNew>(handler, "brain"));
        Assert.AreEqual(brain, GetPrivateField<RobotBrainNew>(handler, "brainNew"));
        Assert.AreEqual(memory, GetPrivateField<RobotMemoryNew>(handler, "memory"));
        Assert.AreEqual(memory, GetPrivateField<RobotMemoryNew>(handler, "memoryNew"));
        Assert.AreEqual(RobotRole.Worker, heart.Role);
    }

    [Test]
    public void WorkerSlot_DedupesDuplicateTriggerEntries()
    {
        RobotNewPipelineRuntime.WorkerCycleValidationMode = false;

        var setup = CreateRobotWithBrain("Robot_WorkerSlotDedup");
        var heart = setup.heart;
        heart.ResetIntentStack(repopulateDefaultTask: true);
        heart.QueueTask(new RobotTask(RobotTaskType.WorkAtMachine));

        var machineGo = new GameObject("FactoryMachine");
        machineGo.AddComponent<BoxCollider2D>();
        var machine = machineGo.AddComponent<FactoryMachine>();

        var slotGo = new GameObject("WorkerSlotDedupGO");
        var slot = slotGo.AddComponent<WorkerSlot>();
        SetPrivateField(slot, "machine", machine);

        InvokePrivate(slot, "OnTriggerEnter2D", setup.collider);
        InvokePrivate(slot, "OnTriggerEnter2D", setup.collider);

        Assert.IsTrue(machine.IsOccupied);
        Assert.AreEqual(1, RobotEcosystemProbe.GetCallCount("Slot.WorkerSlot.attach_requested"));
    }

    [Test]
    public void RestingSlot_ArrivalAtPoweredOffMachine_MarksUnavailableAndReplans()
    {
        var setup = CreateRobotWithBrain("Robot_RestPoweredOffArrival");

        var machineGo = new GameObject("PoweredOffRestMachine");
        var machine = machineGo.AddComponent<RestingMachine>();
        var restWaypoint = machineGo.AddComponent<RoomWaypoint>();
        restWaypoint.type = WaypointType.Rest;
        restWaypoint.IsAvailable = true;
        var workWaypoint = CreateWaypoint("WorkFallback", WaypointType.Work, true, Vector3.right);

        setup.brain.Memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        setup.heart.ResetIntentStack(repopulateDefaultTask: true);
        setup.heart.QueueTask(new RobotTask(RobotTaskType.GoToMachine, restWaypoint));
        machine.PowerOff();

        var slotGo = new GameObject("PoweredOffRestSlot");
        var slot = slotGo.AddComponent<RestingSlot>();
        SetPrivateField(slot, "machine", machine);

        InvokePrivate(slot, "OnTriggerEnter2D", setup.collider);

        Assert.IsNull(machine.CurrentWorker, "Powered-off rest machines must reject arriving workers.");
        Assert.IsFalse(setup.brain.Memory.AllAvailableWaypoints[restWaypoint], "The arriving worker should learn that the rest machine is unavailable.");
        AssertWorkerPlansWaypoint(setup.brain, workWaypoint, "A rejected rest arrival should replan to the available fallback machine.");
        Assert.AreEqual(1, RobotEcosystemProbe.GetCallCount("Slot.RestingSlot.rejected_machine_off_replan"));
    }

    [Test]
    public void RestingSlot_GoToMachineTargetingAnotherRestWaypoint_DoesNotAttach()
    {
        var setup = CreateRobotWithBrain("Robot_RestExactTarget");
        var targetedWaypoint = CreateWaypoint("TargetedRestWaypoint", WaypointType.Rest, true, Vector3.left);

        var wrongMachineGo = new GameObject("WrongRestMachine");
        var wrongMachine = wrongMachineGo.AddComponent<RestingMachine>();
        var wrongWaypoint = wrongMachineGo.AddComponent<RoomWaypoint>();
        wrongWaypoint.type = WaypointType.Rest;
        wrongWaypoint.IsAvailable = true;

        setup.brain.Memory.InitializeWaypointAvailability(new[] { targetedWaypoint, wrongWaypoint });
        setup.heart.ResetIntentStack(repopulateDefaultTask: true);
        setup.heart.QueueTask(new RobotTask(RobotTaskType.GoToMachine, targetedWaypoint));

        var slotGo = new GameObject("WrongRestSlot");
        var slot = slotGo.AddComponent<RestingSlot>();
        SetPrivateField(slot, "machine", wrongMachine);

        InvokePrivate(slot, "OnTriggerEnter2D", setup.collider);

        Assert.IsNull(wrongMachine.CurrentWorker, "A rest slot must reject workers travelling to a different rest waypoint.");
        Assert.AreEqual(0, RobotEcosystemProbe.GetCallCount("Slot.RestingSlot.attach_requested"));
    }

    [Test]
    public void RestingSlot_NewArrivalReplacesCurrentWorker_DirectReplacement()
    {
        RobotNewPipelineRuntime.WorkerCycleValidationMode = true;

        var workerA = CreateRobotWithBrain("Robot_RestOwner_A");
        var workerB = CreateRobotWithBrain("Robot_RestOwner_B");

        workerA.heart.ResetIntentStack(repopulateDefaultTask: true);
        workerA.heart.QueueTask(new RobotTask(RobotTaskType.Rest));
        workerB.heart.ResetIntentStack(repopulateDefaultTask: true);
        workerB.heart.QueueTask(new RobotTask(RobotTaskType.Rest));

        var restWaypoint = CreateWaypoint("RestWP", WaypointType.Rest, true, Vector3.zero);
        var workWaypoint = CreateWaypoint("WorkWP", WaypointType.Work, true, Vector3.right);
        workerA.brain.Memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        workerB.brain.Memory.InitializeWaypointAvailability(new[] { restWaypoint, workWaypoint });
        workerA.brain.Memory.SetLastVisitedPoint(restWaypoint);
        workerB.brain.Memory.SetLastVisitedPoint(restWaypoint);

        var machineGo = new GameObject("RestMachine");
        var machineCollider = machineGo.AddComponent<BoxCollider2D>();
        machineCollider.isTrigger = true;
        var restingMachine = machineGo.AddComponent<RestingMachine>();

        var slotGo = new GameObject("RestSlotTakeoverGO");
        var slot = slotGo.AddComponent<RestingSlot>();
        SetPrivateField(slot, "machine", restingMachine);
        InvokePrivate(slot, "OnTriggerEnter2D", workerA.collider);
        Assert.AreEqual(workerA.brain, restingMachine.CurrentWorker);
        InvokePrivate(slot, "OnTriggerEnter2D", workerB.collider);

        Assert.AreEqual(workerB.brain, restingMachine.CurrentWorker, "Incoming worker should replace current worker.");

        bool ok = workerA.brain.TryGetCurrentPlan(out _, out var task);
        Assert.IsTrue(ok);
        Assert.IsNotNull(task);
        Assert.AreEqual(RobotTaskType.GoToMachine, task.Type);
        Assert.AreEqual(workWaypoint, task.Payload);
    }

    [Test]
    public void RestingSlot_ExitDoesNotReleaseOwner_MemoryDisconnectsOnMachineRelease()
    {
        RobotNewPipelineRuntime.WorkerCycleValidationMode = true;

        var setup = CreateRobotWithBrain("Robot_RestMemory");
        setup.heart.ResetIntentStack(repopulateDefaultTask: true);
        setup.heart.QueueTask(new RobotTask(RobotTaskType.Rest));

        var restWaypoint = CreateWaypoint("RestWP", WaypointType.Rest, true, Vector3.zero);
        setup.brain.Memory.InitializeWaypointAvailability(new[] { restWaypoint });

        var machineGo = new GameObject("RestMachineMemory");
        var machineCollider = machineGo.AddComponent<BoxCollider2D>();
        machineCollider.isTrigger = true;
        var restingMachine = machineGo.AddComponent<RestingMachine>();

        var waypointOnMachine = machineGo.AddComponent<RoomWaypoint>();
        waypointOnMachine.type = WaypointType.Rest;
        waypointOnMachine.IsAvailable = true;

        var slotGo = new GameObject("RestSlotMemoryGO");
        var slot = slotGo.AddComponent<RestingSlot>();
        SetPrivateField(slot, "machine", restingMachine);
        InvokePrivate(slot, "OnTriggerEnter2D", setup.collider);

        Assert.IsTrue(setup.brain.Memory.IsConnectedToMachine);
        Assert.AreEqual(waypointOnMachine, setup.brain.Memory.LastVisitedPoint);

        InvokePrivate(slot, "OnTriggerExit2D", setup.collider);
        Assert.IsTrue(setup.brain.Memory.IsConnectedToMachine);

        restingMachine.TryReleaseWorker(setup.brain, "rest_done");
        Assert.IsFalse(setup.brain.Memory.IsConnectedToMachine);
    }

    [Test]
    public void WorkerSlot_ExitFromNonOwner_DoesNotReleaseCurrentOccupant()
    {
        var workerA = CreateRobotWithBrain("Robot_Owner");
        var workerB = CreateRobotWithBrain("Robot_NonOwner");
        workerA.heart.ResetIntentStack(repopulateDefaultTask: true);
        workerA.heart.QueueTask(new RobotTask(RobotTaskType.WorkAtMachine));
        workerB.heart.ResetIntentStack(repopulateDefaultTask: true);
        workerB.heart.QueueTask(new RobotTask(RobotTaskType.WorkAtMachine));

        var machineGo = new GameObject("FactoryMachine_NonOwnerExit");
        var machine = machineGo.AddComponent<FactoryMachine>();

        var slotGo = new GameObject("WorkerSlot_NonOwnerExit");
        var slot = slotGo.AddComponent<WorkerSlot>();
        SetPrivateField(slot, "machine", machine);

        InvokePrivate(slot, "OnTriggerEnter2D", workerA.collider);
        Assert.AreEqual(workerA.brain, machine.CurrentWorker);

        InvokePrivate(slot, "OnTriggerExit2D", workerB.collider);
        Assert.AreEqual(workerA.brain, machine.CurrentWorker, "Non-owner exit must not release current occupant.");
    }

    private static (RobotBrainNew brain, RobotHeartNew heart, Collider2D collider) CreateRobotWithBrain(string name)
    {
        var robot = new GameObject(name);
        robot.AddComponent<RobotMemoryNew>();
        var heart = robot.AddComponent<RobotHeartNew>();
        var brain = robot.AddComponent<RobotBrainNew>();
        var collider = robot.AddComponent<BoxCollider2D>();

        InvokePrivate(heart, "Awake");
        InvokePrivate(brain, "Awake");
        InvokePrivate(brain, "OnEnable");
        InvokePrivate(heart, "OnEnable");

        return (brain, heart, collider);
    }

    private static RoomWaypoint CreateWaypoint(string name, WaypointType type, bool available, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var wp = go.AddComponent<RoomWaypoint>();
        wp.type = type;
        wp.IsAvailable = available;
        return wp;
    }

    private static void PrepareWorkerForUnavailableWorkMachine(RobotMemoryNew memory, RoomWaypoint workWaypoint, RoomWaypoint restWaypoint)
    {
        Assert.IsNotNull(memory);

        memory.InitializeWaypointAvailability(new[] { workWaypoint, restWaypoint });
        memory.SetLastVisitedPoint(restWaypoint);
        memory.SetRoomWaypointAvailability(workWaypoint, false);
        memory.ChangeConnectionToMachine(false);
        memory.SetDesiredMachineType(MachineType.WorkStation);
    }

    private static void AssertWorkerPlansWaypoint(RobotBrainNew brain, RoomWaypoint waypoint, string message)
    {
        bool ok = brain.TryGetCurrentPlan(out _, out var task);
        Assert.IsTrue(ok);
        Assert.IsNotNull(task);
        Assert.AreEqual(RobotTaskType.GoToMachine, task.Type);
        Assert.AreEqual(waypoint, task.Payload, message);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
        return (T)field.GetValue(target);
    }

    private static void InvokePrivate(object target, string methodName, params object[] args)
    {
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var method = target.GetType().GetMethod(methodName, flags);
        Assert.IsNotNull(method, $"Method '{methodName}' not found on {target.GetType().Name}.");
        method.Invoke(target, args);
    }

    private sealed class FakeWaypointService : IWaypointService
    {
        private readonly List<RoomWaypoint> waypoints;
        public event Action<RoomWaypoint, Vector2> OnClosestWaypointToPlayerChanged;

        public FakeWaypointService(params RoomWaypoint[] waypoints)
        {
            this.waypoints = new List<RoomWaypoint>(waypoints);
        }

        public RoomWaypoint ClosestWaypointToPlayer { get; private set; }

        public void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints) { }
        public void UnregisterRoomWaypoints(RoomManager room) { }
        public void BuildAllNeighbors(bool includeUnavailable = false) { }
        public void Subscribe(IRobotNavigationListener robot) { }
        public void Unsubscribe(IRobotNavigationListener robot) { }
        public void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable) { }
        public List<RoomWaypoint> GetAllWaypoints() => new List<RoomWaypoint>(waypoints);
        public List<RoomWaypoint> GetActiveWaypoints() => waypoints.FindAll(waypoint => waypoint != null && waypoint.IsAvailable);
        public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end) => new List<RoomWaypoint> { start, end };
        public RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false) => waypoints.Count > 0 ? waypoints[0] : null;
        public RoomWaypoint GetEndPoint() => null;
        public RoomWaypoint GetStartPoint() => null;
        public void UpdateClosestWaypointToPlayer(Vector2 playerPosition)
        {
            var previous = ClosestWaypointToPlayer;
            ClosestWaypointToPlayer = GetClosestWaypoint(playerPosition, true);
            if (ClosestWaypointToPlayer != null && ClosestWaypointToPlayer != previous)
                OnClosestWaypointToPlayerChanged?.Invoke(ClosestWaypointToPlayer, playerPosition);
        }
        public RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetAnyOnWorkPoint(RoomWaypoint exclude = null) => null;
        public FactoryMachine GetAnyOnFactoryMachine() => null;
        public RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetFirstFreeSecurityPoint() => null;
        public RoomWaypoint GetSecurityOrRestPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetBlockedRoomSecuritySpawning(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetBlockedRoomCenter(RoomWaypoint exclude = null) => null;
        public bool IsPOIReserved(RoomWaypoint poi) => false;
        public void ReleaseInvalidReservations() { }
        public void ReleasePOI(RoomWaypoint poi) { }
    }
}
