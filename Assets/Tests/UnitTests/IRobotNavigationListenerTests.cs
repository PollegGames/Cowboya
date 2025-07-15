using NUnit.Framework;
using UnityEngine;

public class IRobotNavigationListenerTests
{
    private class DummyListener : IRobotNavigationListener
    {
        public void OnPathObsoleted(RoomWaypoint blockedWaypoint) {}
    }

    private IRobotNavigationListener _listener;

    [SetUp]
    public void SetUp()
    {
        _listener = new DummyListener();
    }

    [Test]
    public void OnPathObsoleted_CalledOnChange()
    {
        var waypointGO = new GameObject();
        var waypoint = waypointGO.AddComponent<RoomWaypoint>();

        Assert.DoesNotThrow(() => _listener.OnPathObsoleted(waypoint));
    }
}
