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
        var pos = new Vector3(1, 2, 0);
        _gameObject.transform.position = pos;

        Assert.AreEqual(pos, _waypoint.WorldPos);
    }

    [Test]
    public void Neighbors_Initialized()
    {
        Assert.IsNotNull(_waypoint.Neighbors);
        Assert.IsEmpty(_waypoint.Neighbors);
    }
}
