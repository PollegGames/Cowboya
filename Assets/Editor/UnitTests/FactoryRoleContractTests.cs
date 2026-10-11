using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class FactoryRoleContractTests
{
    [TestCase(RobotRole.Worker, true)]
    [TestCase(RobotRole.WorkerCollector, true)]
    [TestCase(RobotRole.SecurityGuard, true)]
    [TestCase(RobotRole.WorkerSpawner, false)]
    [TestCase(RobotRole.Follower, false)]
    [TestCase(RobotRole.Boss, false)]
    [TestCase(RobotRole.Collector, false)]
    [TestCase(RobotRole.SecurityReception, false)]
    public void SaveEligibility_MatchesGameplayContract(RobotRole role, bool expected)
    {
        Assert.AreEqual(expected, FactoryManager.IsSaveEligibleRole(role));
    }

    [Test]
    public void SecurityGuard_CannotReactivateOwnSecurityMachine()
    {
        var managerObject = new GameObject("SecurityManager");
        var manager = managerObject.AddComponent<MachineSecurityManager>();
        var machineObject = new GameObject("SecurityMachine");
        machineObject.AddComponent<MeshRenderer>();
        var machine = machineObject.AddComponent<SecurityMachine>();
        var guardObject = new GameObject("Guard");
        var memory = guardObject.AddComponent<RobotMemoryNew>();
        var heart = guardObject.AddComponent<RobotHeartNew>();
        var brain = guardObject.AddComponent<RobotBrainNew>();
        heart.ConfigureRole(RobotRole.SecurityGuard);

        manager.RegisterSecurityMachine(machine);
        machine.AttachRobot(guardObject);

        Assert.IsFalse(manager.CanGuardReactivate(brain, machine));

        Object.DestroyImmediate(guardObject);
        Object.DestroyImmediate(machineObject);
        Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void ReactivationJob_RemainsActiveUntilGuardReconnectsToAssignedPost()
    {
        var managerObject = new GameObject("SecurityManager");
        var manager = managerObject.AddComponent<MachineSecurityManager>();
        var postObject = new GameObject("AssignedPost");
        postObject.AddComponent<MeshRenderer>();
        var postWaypoint = postObject.AddComponent<RoomWaypoint>();
        postWaypoint.type = WaypointType.Security;
        postWaypoint.IsAvailable = true;
        var post = postObject.AddComponent<SecurityMachine>();
        var target = new GameObject("RepairTarget").AddComponent<FactoryMachine>();
        var guardObject = new GameObject("Guard");
        guardObject.SetActive(false);
        var memory = guardObject.AddComponent<RobotMemoryNew>();
        var heart = guardObject.AddComponent<RobotHeartNew>();
        var guard = guardObject.AddComponent<RobotBrainNew>();
        SetField(guard, "memory", memory);
        SetField(guard, "heart", heart);
        SetField(heart, "memory", memory);
        SetField(heart, "brain", guard);
        heart.ConfigureRole(RobotRole.SecurityGuard);
        memory.InitializeWaypointAvailability(new[] { postWaypoint });
        guardObject.SetActive(true);

        manager.RegisterSecurityMachine(post);
        post.AttachRobot(guardObject);
        SetField(manager, "activeReactivationGuard", guard);
        SetField(manager, "activeReactivationMachine", target);

        target.PowerOff();
        target.PowerOn();
        Assert.IsTrue(manager.HasActiveReactivationJob);

        Assert.IsTrue(manager.ReturnGuardToAssignedPost(guard));
        Assert.IsTrue(guard.TryGetCurrentPlan(out _, out RobotTask returnTask));
        Assert.AreEqual(RobotTaskType.GoToMachine, returnTask.Type);
        Assert.AreEqual(postWaypoint, returnTask.Payload);

        manager.HandleGuardConnectedToSecurityMachine(post, guard);
        Assert.IsFalse(manager.HasActiveReactivationJob);

        Object.DestroyImmediate(guardObject);
        Object.DestroyImmediate(target.gameObject);
        Object.DestroyImmediate(postObject);
        Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void ReactivationCompletion_WithAssignedPostOff_UsesRestFallbackAndReleasesJob()
    {
        var managerObject = new GameObject("SecurityManager");
        var manager = managerObject.AddComponent<MachineSecurityManager>();
        var postObject = new GameObject("AssignedPost");
        postObject.AddComponent<MeshRenderer>();
        var postWaypoint = postObject.AddComponent<RoomWaypoint>();
        postWaypoint.type = WaypointType.Security;
        postWaypoint.IsAvailable = true;
        var post = postObject.AddComponent<SecurityMachine>();
        var target = new GameObject("RepairTarget").AddComponent<FactoryMachine>();
        var guardObject = new GameObject("Guard");
        guardObject.SetActive(false);
        var memory = guardObject.AddComponent<RobotMemoryNew>();
        var heart = guardObject.AddComponent<RobotHeartNew>();
        var guard = guardObject.AddComponent<RobotBrainNew>();
        SetField(guard, "memory", memory);
        SetField(guard, "heart", heart);
        SetField(heart, "memory", memory);
        SetField(heart, "brain", guard);
        heart.ConfigureRole(RobotRole.SecurityGuard);
        memory.InitializeWaypointAvailability(new[] { postWaypoint });
        guardObject.SetActive(true);

        manager.RegisterSecurityMachine(post);
        post.AttachRobot(guardObject);
        SetField(manager, "activeReactivationGuard", guard);
        SetField(manager, "activeReactivationMachine", target);
        post.PowerOff();

        Assert.IsTrue(manager.ReturnGuardToAssignedPost(guard));
        Assert.IsFalse(manager.HasActiveReactivationJob);
        Assert.AreEqual(MachineType.RestStation, memory.Snapshot.DesiredMachineType);
        Assert.IsTrue(guard.TryGetCurrentPlan(out _, out RobotTask fallbackTask));
        Assert.AreEqual(RobotTaskType.ReturnHome, fallbackTask.Type,
            "Without a powered Rest station, the guard must fall back to Start.");

        Object.DestroyImmediate(guardObject);
        Object.DestroyImmediate(target.gameObject);
        Object.DestroyImmediate(postObject);
        Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void ReactivationCompletion_WithAssignedPostOff_GoesToPoweredRest()
    {
        var managerObject = new GameObject("SecurityManager");
        var manager = managerObject.AddComponent<MachineSecurityManager>();
        var postObject = new GameObject("AssignedPost");
        postObject.AddComponent<MeshRenderer>();
        var postWaypoint = postObject.AddComponent<RoomWaypoint>();
        postWaypoint.type = WaypointType.Security;
        postWaypoint.IsAvailable = true;
        var post = postObject.AddComponent<SecurityMachine>();
        var target = new GameObject("RepairTarget").AddComponent<FactoryMachine>();

        var restRoomObject = new GameObject("RestRoom");
        var restRoom = restRoomObject.AddComponent<RoomManager>();
        var restWaypointObject = new GameObject("RestWaypoint");
        restWaypointObject.transform.SetParent(restRoomObject.transform);
        var restWaypoint = restWaypointObject.AddComponent<RoomWaypoint>();
        restWaypoint.type = WaypointType.Rest;
        restWaypoint.IsAvailable = true;
        restWaypoint.parentRoom = restRoom;
        var restMachineObject = new GameObject("RestMachine");
        restMachineObject.transform.SetParent(restRoomObject.transform);
        restMachineObject.AddComponent<MeshRenderer>();
        var restMachine = restMachineObject.AddComponent<RestingMachine>();
        restRoom.restingMachinesInRoom.Add(restMachine);

        var guardObject = new GameObject("Guard");
        guardObject.SetActive(false);
        var memory = guardObject.AddComponent<RobotMemoryNew>();
        var heart = guardObject.AddComponent<RobotHeartNew>();
        var guard = guardObject.AddComponent<RobotBrainNew>();
        SetField(guard, "memory", memory);
        SetField(guard, "heart", heart);
        SetField(heart, "memory", memory);
        SetField(heart, "brain", guard);
        heart.ConfigureRole(RobotRole.SecurityGuard);
        memory.InitializeWaypointAvailability(new[] { postWaypoint, restWaypoint });
        guardObject.SetActive(true);

        manager.RegisterSecurityMachine(post);
        post.AttachRobot(guardObject);
        SetField(manager, "activeReactivationGuard", guard);
        SetField(manager, "activeReactivationMachine", target);
        post.PowerOff();

        Assert.IsTrue(manager.ReturnGuardToAssignedPost(guard));
        Assert.IsFalse(manager.HasActiveReactivationJob);
        Assert.IsTrue(guard.TryGetCurrentPlan(out _, out RobotTask fallbackTask));
        Assert.AreEqual(RobotTaskType.GoToMachine, fallbackTask.Type);
        Assert.AreEqual(restWaypoint, fallbackTask.Payload);

        Object.DestroyImmediate(guardObject);
        Object.DestroyImmediate(target.gameObject);
        Object.DestroyImmediate(postObject);
        Object.DestroyImmediate(restRoomObject);
        Object.DestroyImmediate(managerObject);
    }

    [Test]
    public void SpawningMachine_PowerOffFaintsAndReleases_ThenPowerOnReconnectsNearbyWorker()
    {
        var alarm = ScriptableObject.CreateInstance<FactoryAlarmStatus>();
        var machineObject = new GameObject("SpawningMachine");
        machineObject.AddComponent<MeshRenderer>();
        var trigger = machineObject.AddComponent<BoxCollider2D>();
        trigger.size = Vector2.one * 4f;
        var machine = machineObject.AddComponent<SpawningMachine>();
        SetField(machine, "factoryAlarmStatus", alarm);
        SetField(machine, "trigger", trigger);

        var workerObject = new GameObject("WorkerSpawner");
        workerObject.transform.position = machineObject.transform.position;
        workerObject.AddComponent<RobotMemoryNew>();
        var heart = workerObject.AddComponent<RobotHeartNew>();
        workerObject.AddComponent<RobotBrainNew>();
        var state = workerObject.AddComponent<RobotStateController>();
        heart.ConfigureRole(RobotRole.WorkerSpawner);

        machine.AttachRobot(workerObject);
        Assert.IsTrue(machine.HasWorker);

        machine.PowerOff();
        Assert.IsFalse(machine.HasWorker);
        Assert.IsFalse(machine.IsOccupied);
        Assert.AreEqual(RobotState.Faint, state.CurrentState);

        machine.PowerOn();
        Assert.IsTrue(machine.HasWorker);
        Assert.IsTrue(machine.IsOccupied);
        Assert.AreEqual(RobotState.Alive, state.CurrentState);

        Object.DestroyImmediate(workerObject);
        Object.DestroyImmediate(machineObject);
        Object.DestroyImmediate(alarm);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Missing field {name}");
        field.SetValue(target, value);
    }
}
