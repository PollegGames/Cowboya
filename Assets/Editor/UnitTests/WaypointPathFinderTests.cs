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

    [Test]
    public void FindWorldPath_DoesNotUseStartRoomAsTransit()
    {
        var finderObject = new GameObject("PathFinder");
        var finder = finderObject.AddComponent<WaypointPathFinder>();
        RoomWaypoint origin = CreateWaypoint("Origin", new Vector3(0f, 0f), UsageType.Work);
        RoomWaypoint startRoom = CreateWaypoint("StartRoom", new Vector3(1f, 0f), UsageType.Start);
        RoomWaypoint detour = CreateWaypoint("Detour", new Vector3(0f, 1f), UsageType.Work);
        RoomWaypoint destination = CreateWaypoint("Destination", new Vector3(2f, 0f), UsageType.Work);

        origin.Neighbors.Add(startRoom);
        origin.Neighbors.Add(detour);
        startRoom.Neighbors.Add(destination);
        detour.Neighbors.Add(destination);

        List<RoomWaypoint> result = finder.FindWorldPath(origin, destination);

        CollectionAssert.AreEqual(new[] { origin, detour, destination }, result);

        Object.DestroyImmediate(origin.parentRoom.gameObject);
        Object.DestroyImmediate(startRoom.parentRoom.gameObject);
        Object.DestroyImmediate(detour.parentRoom.gameObject);
        Object.DestroyImmediate(destination.parentRoom.gameObject);
        Object.DestroyImmediate(finderObject);
    }

    private static RoomWaypoint CreateWaypoint(string name, Vector3 position, UsageType usageType)
    {
        var roomObject = new GameObject(name + "Room");
        var properties = roomObject.AddComponent<RoomProperties>();
        properties.usageType = usageType;
        var room = roomObject.AddComponent<RoomManager>();
        room.roomProperties = properties;

        var waypointObject = new GameObject(name);
        waypointObject.transform.SetParent(roomObject.transform);
        waypointObject.transform.position = position;
        var waypoint = waypointObject.AddComponent<RoomWaypoint>();
        waypoint.parentRoom = room;
        waypoint.IsAvailable = true;
        return waypoint;
    }
}
