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
