using NUnit.Framework;
using UnityEngine;

public class StationReservationServiceTests
{
    private GameObject go;
    private StationReservationService service;

    [SetUp]
    public void SetUp()
    {
        go = new GameObject();
        service = go.AddComponent<StationReservationService>();
    }

    private class DummyMachine : BaseMachine { }

    [Test]
    public void RegisterMachine_AddsMachine()
    {
        var machineGO = new GameObject();
        var machine = machineGO.AddComponent<DummyMachine>();

        service.RegisterMachine(machine, RobotRole.Worker);

        var reserved = service.ReserveStation(RobotRole.Worker);

        Assert.AreEqual(machine, reserved);
    }

    [Test]
    public void ReserveAndReleaseStation_Works()
    {
        var machineGO = new GameObject();
        var machine = machineGO.AddComponent<DummyMachine>();
        service.RegisterMachine(machine, RobotRole.Worker);

        var first = service.ReserveStation(RobotRole.Worker);
        Assert.AreEqual(machine, first);

        service.ReleaseStation(machine);

        var second = service.ReserveStation(RobotRole.Worker);
        Assert.AreEqual(machine, second);
    }

    [Test]
    public void Events_FireCorrectly()
    {
        var machineGO = new GameObject();
        var machine = machineGO.AddComponent<DummyMachine>();
        service.RegisterMachine(machine, RobotRole.Worker);

        int freed = 0, poweredOn = 0, poweredOff = 0;
        service.OnMachineFreed += _ => freed++;
        service.OnMachinePoweredOn += _ => poweredOn++;
        service.OnMachinePoweredOff += _ => poweredOff++;

        machine.ReleaseRobot();
        machine.PowerOff();
        machine.PowerOn();

        Assert.AreEqual(1, freed);
        Assert.AreEqual(1, poweredOff);
        Assert.AreEqual(1, poweredOn);
    }

    [Test]
    public void Reservation_TracksOwner_AndRejectsReleaseByDifferentRobot()
    {
        var machine = new GameObject("Rest").AddComponent<DummyMachine>();
        var owner = new GameObject("Owner").AddComponent<RobotBrainNew>();
        var other = new GameObject("Other").AddComponent<RobotBrainNew>();
        service.RegisterMachine(machine, RobotRole.Worker);

        Assert.IsTrue(service.TryReserveStation(machine, RobotRole.Worker, owner, "rest"));
        Assert.IsTrue(service.TryGetReservation(machine, out var reservation));
        Assert.AreSame(owner, reservation.Owner);
        Assert.AreEqual("rest", reservation.Purpose);
        Assert.IsFalse(service.ReleaseStation(machine, other));
        Assert.IsTrue(service.ReleaseStation(machine, owner));
    }

    [Test]
    public void Machine_CanBeAvailableToWorkerAndSecurityGuard_ButOnlyOneCanReserveIt()
    {
        var machine = new GameObject("SharedRest").AddComponent<DummyMachine>();
        service.RegisterMachine(machine, RobotRole.Worker);
        service.RegisterMachine(machine, RobotRole.SecurityGuard);

        Assert.AreSame(machine, service.ReserveStation(RobotRole.Worker));
        Assert.IsNull(service.ReserveStation(RobotRole.SecurityGuard));
    }
}
