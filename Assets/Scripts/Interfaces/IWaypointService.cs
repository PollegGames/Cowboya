using System.Collections.Generic;
using UnityEngine;

public interface IWaypointService
{
    void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints);
    void UnregisterRoomWaypoints(RoomManager room);
    void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable);

    void Subscribe(IRobotNavigationListener robot);
    void Unsubscribe(IRobotNavigationListener robot);

    void BuildAllNeighbors();
    List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end);

    List<RoomWaypoint> GetActiveWaypoints();
    RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetFirstFreeSecurityPoint();
    RoomWaypoint GetEndPoint();
    void ReleasePOI(RoomWaypoint poi);

    void UpdateClosestWaypointToPlayer(Vector2 playerPosition);
    RoomWaypoint ClosestWaypointToPlayer { get; }
}
