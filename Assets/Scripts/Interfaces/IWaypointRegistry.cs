using System.Collections.Generic;

public interface IWaypointRegistry
{
    void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints);
    void UnregisterRoomWaypoints(RoomManager room);
    List<RoomWaypoint> GetAllWaypoints();
    List<RoomWaypoint> GetActiveWaypoints();
}
