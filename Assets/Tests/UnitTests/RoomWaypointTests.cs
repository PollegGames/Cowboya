using NUnit.Framework;
using UnityEngine;

public class RoomWaypointTests
{
    private GameObject _gameObject;
    private RoomWaypoint _waypoint;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _waypoint = _gameObject.AddComponent<RoomWaypoint>();
    }

    [Test]
    public void WorldPos_ReturnsPosition()
    {
        // TODO: Assert world position
    }

    [Test]
    public void Neighbors_Initialized()
    {
        // TODO: Assert neighbors list
    }
}
