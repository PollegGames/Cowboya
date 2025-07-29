using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class WaypointPathFinderTests
{
    [Test]
    public void FindWorldPath_NullWaypoint_ReturnsEmpty()
    {
        var go = new GameObject();
        var finder = go.AddComponent<WaypointPathFinder>();

        List<RoomWaypoint> result = null;
        Assert.DoesNotThrow(() => result = finder.FindWorldPath(null, null));
        Assert.IsNotNull(result);
        Assert.IsEmpty(result);
    }
}
