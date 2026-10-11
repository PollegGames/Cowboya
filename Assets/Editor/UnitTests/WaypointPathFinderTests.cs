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

    [Test]
    public void FindWorldPath_CanLeaveThroughWaypointsInOriginStartRoom()
    {
        var finderObject = new GameObject("PathFinder");
        var finder = finderObject.AddComponent<WaypointPathFinder>();
        var startRoomObject = new GameObject("StartRoom");
        var properties = startRoomObject.AddComponent<RoomProperties>();
        properties.usageType = UsageType.Start;
        var startRoom = startRoomObject.AddComponent<RoomManager>();
        startRoom.roomProperties = properties;
        RoomWaypoint origin = CreateWaypointInRoom("StartWaypoint", startRoom, Vector3.zero);
        RoomWaypoint exit = CreateWaypointInRoom("StartExit", startRoom, Vector3.right);
        RoomWaypoint destination = CreateWaypoint("Destination", Vector3.right * 2f, UsageType.Work);

        origin.Neighbors.Add(exit);
        exit.Neighbors.Add(destination);

        List<RoomWaypoint> result = finder.FindWorldPath(origin, destination);

        CollectionAssert.AreEqual(new[] { origin, exit, destination }, result);

        Object.DestroyImmediate(startRoomObject);
        Object.DestroyImmediate(destination.parentRoom.gameObject);
        Object.DestroyImmediate(finderObject);
    }

    [Test]
    public void FindWorldPath_CanEnterThroughWaypointsInDestinationStartRoom()
    {
        var finderObject = new GameObject("PathFinder");
        var finder = finderObject.AddComponent<WaypointPathFinder>();
        RoomWaypoint origin = CreateWaypoint("Origin", Vector3.zero, UsageType.Work);
        var startRoomObject = new GameObject("DestinationStartRoom");
        var properties = startRoomObject.AddComponent<RoomProperties>();
        properties.usageType = UsageType.Start;
        var startRoom = startRoomObject.AddComponent<RoomManager>();
        startRoom.roomProperties = properties;
        RoomWaypoint entrance = CreateWaypointInRoom("StartEntrance", startRoom, Vector3.right);
        RoomWaypoint destination = CreateWaypointInRoom(
            "StartWorkWaypoint", startRoom, Vector3.right * 2f);

        origin.Neighbors.Add(entrance);
        entrance.Neighbors.Add(destination);

        List<RoomWaypoint> result = finder.FindWorldPath(origin, destination);

        CollectionAssert.AreEqual(new[] { origin, entrance, destination }, result);

        Object.DestroyImmediate(origin.parentRoom.gameObject);
        Object.DestroyImmediate(startRoomObject);
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

    private static RoomWaypoint CreateWaypointInRoom(
        string name, RoomManager room, Vector3 position)
    {
        var waypointObject = new GameObject(name);
        waypointObject.transform.SetParent(room.transform);
        waypointObject.transform.position = position;
        var waypoint = waypointObject.AddComponent<RoomWaypoint>();
        waypoint.parentRoom = room;
        waypoint.IsAvailable = true;
        return waypoint;
    }
}
