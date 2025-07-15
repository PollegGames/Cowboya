using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

public class WaypointServiceTests
{
    private GameObject _gameObject;
    private WaypointService _service;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _service = _gameObject.AddComponent<WaypointService>();
    }

    private class DummyRegistry : MonoBehaviour, IWaypointRegistry
    {
        public bool Registered; public bool Unregistered;
        public List<RoomWaypoint> active = new();
        public void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints) { Registered = true; }
        public void UnregisterRoomWaypoints(RoomManager room) { Unregistered = true; }
        public List<RoomWaypoint> GetAllWaypoints() => new();
        public List<RoomWaypoint> GetActiveWaypoints() => active;
    }
    private class DummyPathFinder : MonoBehaviour, IPathFinder
    {
        public List<RoomWaypoint> path = new();
        public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end) => path;
        public void BuildAllNeighbors() { }
    }
    private class DummyListener : IRobotNavigationListener
    {
        public bool Called;
        public void OnPathObsoleted(RoomWaypoint blockedWaypoint) { Called = true; }
    }

    private void InitService(DummyRegistry reg, DummyPathFinder finder)
    {
        typeof(WaypointService).GetField("registryBehaviour", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_service, reg);
        typeof(WaypointService).GetField("pathFinderBehaviour", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_service, finder);
        typeof(WaypointService).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(_service, null);
    }

    [Test]
    public void RegisterRoomWaypoints_StoresWaypoints()
    {
        var reg = new GameObject().AddComponent<DummyRegistry>();
        var finder = new GameObject().AddComponent<DummyPathFinder>();
        InitService(reg, finder);

        _service.RegisterRoomWaypoints(null, null);

        Assert.IsTrue(reg.Registered);
    }

    [Test]
    public void UnregisterRoomWaypoints_RemovesWaypoints()
    {
        var reg = new GameObject().AddComponent<DummyRegistry>();
        var finder = new GameObject().AddComponent<DummyPathFinder>();
        InitService(reg, finder);

        _service.UnregisterRoomWaypoints(null);

        Assert.IsTrue(reg.Unregistered);
    }

    [Test]
    public void NotifyWaypointStatusChanged_NotifiesRobots()
    {
        var reg = new GameObject().AddComponent<DummyRegistry>();
        var finder = new GameObject().AddComponent<DummyPathFinder>();
        InitService(reg, finder);

        var listener = new DummyListener();
        _service.Subscribe(listener);
        var wp = new GameObject().AddComponent<RoomWaypoint>();

        _service.NotifyWaypointStatusChanged(wp, false);

        Assert.IsTrue(listener.Called);
    }

    [Test]
    public void FindWorldPath_ReturnsPath()
    {
        var reg = new GameObject().AddComponent<DummyRegistry>();
        var finder = new GameObject().AddComponent<DummyPathFinder>();
        var wp1 = new GameObject().AddComponent<RoomWaypoint>();
        var wp2 = new GameObject().AddComponent<RoomWaypoint>();
        finder.path = new List<RoomWaypoint> { wp1, wp2 };
        InitService(reg, finder);

        var result = _service.FindWorldPath(wp1, wp2);
        Assert.AreEqual(finder.path, result);
    }

    [Test]
    public void GetLeastUsedFreeWorkPoint_ReturnsPoint()
    {
        var reg = new GameObject().AddComponent<DummyRegistry>();
        var finder = new GameObject().AddComponent<DummyPathFinder>();
        reg.active.Clear();
        InitService(reg, finder);

        var result = _service.GetLeastUsedFreeWorkPoint();
        Assert.IsNull(result);
    }

    [Test]
    public void GetClosestWaypoint_ReturnsClosest()
    {
        var reg = new GameObject().AddComponent<DummyRegistry>();
        var finder = new GameObject().AddComponent<DummyPathFinder>();
        var wp1 = new GameObject().AddComponent<RoomWaypoint>();
        wp1.transform.position = Vector3.zero;
        var wp2 = new GameObject().AddComponent<RoomWaypoint>();
        wp2.transform.position = new Vector3(5,0,0);
        reg.active = new List<RoomWaypoint> { wp1, wp2 };
        InitService(reg, finder);

        var closest = _service.GetClosestWaypoint(Vector2.one);
        Assert.AreEqual(wp1, closest);
    }
}
