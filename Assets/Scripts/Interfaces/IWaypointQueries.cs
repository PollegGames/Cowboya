using System.Collections.Generic;
using UnityEngine;

public interface IWaypointQueries
{
    List<RoomWaypoint> GetActiveWaypoints();
    List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end);
    RoomWaypoint GetClosestWaypoint(Vector2 position);
    RoomWaypoint GetEndPoint();
    void UpdateClosestWaypointToPlayer(Vector2 playerPosition);
    RoomWaypoint ClosestWaypointToPlayer { get; }
}
