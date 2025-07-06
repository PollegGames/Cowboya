using NUnit.Framework;
using UnityEngine;

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
        // TODO: Assert FactoryManager assignment
    }

    [Test]
    public void SetWaypointStatus_UpdatesWaypoint()
    {
        // TODO: Assert waypoint status change
    }

    [Test]
    public void GetWaypoints_ReturnsWaypoints()
    {
        // TODO: Assert returned waypoints
    }

    [Test]
    public void GetRoomBounds_ReturnsBounds()
    {
        // TODO: Assert bounds
    }
}
