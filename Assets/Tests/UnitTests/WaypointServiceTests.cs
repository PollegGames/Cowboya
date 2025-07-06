using NUnit.Framework;
using UnityEngine;

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

    [Test]
    public void RegisterRoomWaypoints_StoresWaypoints()
    {
        // TODO: Assert registration
    }

    [Test]
    public void UnregisterRoomWaypoints_RemovesWaypoints()
    {
        // TODO: Assert unregistration
    }

    [Test]
    public void NotifyWaypointStatusChanged_NotifiesRobots()
    {
        // TODO: Assert notification
    }

    [Test]
    public void FindWorldPath_ReturnsPath()
    {
        // TODO: Assert path finding
    }

    [Test]
    public void GetLeastUsedFreeWorkPoint_ReturnsPoint()
    {
        // TODO: Assert work point
    }

    [Test]
    public void GetClosestWaypoint_ReturnsClosest()
    {
        // TODO: Assert closest waypoint
    }
}
