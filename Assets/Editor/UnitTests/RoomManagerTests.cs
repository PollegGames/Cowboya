using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class RoomManagerTests
{
    private GameObject _gameObject;
    private RoomManager _roomManager;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _roomManager = _gameObject.AddComponent<RoomManager>();
    }

    [Test]
    public void Initialize_SetsFactoryManager()
    {
        var factory = new GameObject().AddComponent<FactoryManager>();
        _roomManager.Initialize(factory, null, null, null, null);

        Assert.AreEqual(factory, _roomManager.FactoryManager);
    }

    [Test]
    public void SetWaypointStatus_UpdatesWaypoint()
    {
        var wpGO = new GameObject();
        var wp = wpGO.AddComponent<RoomWaypoint>();
        _roomManager.waypointService = new GameObject().AddComponent<WaypointService>();
        _roomManager.SetWaypointStatus(wp, true);
        Assert.IsTrue(wp.IsAvailable);
    }

    [Test]
    public void GetWaypoints_ReturnsWaypoints()
    {
        Assert.IsNotNull(_roomManager.GetWaypoints());
    }

    [Test]
    public void GetRoomBounds_ReturnsBounds()
    {
        _roomManager.triggerZone = new GameObject().AddComponent<PositionTriggerZone>();
        var bounds = _roomManager.GetRoomBounds();
        Assert.AreEqual(Vector3.zero, bounds.center);
    }

    [Test]
    public void Initialize_WhenFactoryMachinePowersOff_RaisesRoomMachineChangedPowerEvent()
    {
        var machineGo = new GameObject("FactoryMachine_Test");
        var machine = machineGo.AddComponent<FactoryMachine>();
        _roomManager.factorymMachinesInRoom = new List<FactoryMachine> { machine };

        RoomMachineChangedEvent? observed = null;
        _roomManager.OnRoomMachineChanged += evt => observed = evt;

        _roomManager.Initialize(null, null, null, null, null);
        machine.PowerOff();

        Assert.IsTrue(observed.HasValue);
        Assert.AreEqual(RoomMachineEventKind.PowerChanged, observed.Value.EventKind);
        Assert.AreSame(machine, observed.Value.Machine);
        Assert.IsTrue(observed.Value.IsOn.HasValue);
        Assert.IsFalse(observed.Value.IsOn.Value);
    }

    [Test]
    public void RaiseRoomThreat_RaisesRoomThreatChangedEvent()
    {
        RoomThreatChangedEvent? observed = null;
        _roomManager.OnRoomThreatChanged += evt => observed = evt;
        var knownPosition = new Vector3(4f, 2f, 0f);

        _roomManager.RaiseRoomThreat(
            AlarmState.Wanted,
            RoomThreatSource.SecurityCamera,
            knownPosition);

        Assert.IsTrue(observed.HasValue);
        Assert.AreEqual(AlarmState.Wanted, observed.Value.DesiredAlarmState);
        Assert.AreEqual(RoomThreatSource.SecurityCamera, observed.Value.Source);
        Assert.IsTrue(observed.Value.HasKnownPlayerPosition);
        Assert.AreEqual(knownPosition, observed.Value.KnownPlayerPosition);
    }
}
