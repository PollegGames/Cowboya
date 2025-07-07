using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaypointRegistry : MonoBehaviour, IWaypointRegistry
{
    private readonly Dictionary<RoomManager, List<RoomWaypoint>> roomWaypoints = new();

    public void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints)
    {
        roomWaypoints[room] = waypoints.ToList();
    }

    public void UnregisterRoomWaypoints(RoomManager room)
    {
        roomWaypoints.Remove(room);
    }

    public List<RoomWaypoint> GetAllWaypoints() =>
        roomWaypoints.Values.SelectMany(l => l).ToList();

    public List<RoomWaypoint> GetActiveWaypoints() =>
        roomWaypoints.Values.SelectMany(l => l).Where(wp => wp.IsAvailable).ToList();
}
