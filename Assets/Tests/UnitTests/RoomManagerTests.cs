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
        var factory = new GameObject().AddComponent<FactoryManager>();
        _roomManager.Initialize(factory, null, null, null);

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
}
