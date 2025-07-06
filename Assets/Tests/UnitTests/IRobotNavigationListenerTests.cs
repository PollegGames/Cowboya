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
        // TODO: Assert path obsolete handling
    }
}
