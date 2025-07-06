using System.Collections.Generic;
using UnityEngine;

public interface IWaypointService : IWaypointNotifier, IWaypointQueries
{
    void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints);
    void UnregisterRoomWaypoints(RoomManager room);

    void BuildAllNeighbors();

    RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetFirstFreeSecurityPoint();
    void ReleasePOI(RoomWaypoint poi);
}
