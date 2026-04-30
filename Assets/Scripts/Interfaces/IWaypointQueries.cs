using System;
using System.Collections.Generic;
using UnityEngine;

public interface IWaypointQueries
{
    event Action<RoomWaypoint, Vector2> OnClosestWaypointToPlayerChanged;
    List<RoomWaypoint> GetAllWaypoints();
    List<RoomWaypoint> GetActiveWaypoints();
    List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end);
    RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false);
    RoomWaypoint GetEndPoint();
    RoomWaypoint GetStartPoint();
    void UpdateClosestWaypointToPlayer(Vector2 playerPosition);
    RoomWaypoint ClosestWaypointToPlayer { get; }
}
