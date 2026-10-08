using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaypointRegistry : MonoBehaviour, IWaypointRegistry
{
    private readonly Dictionary<RoomManager, List<RoomWaypoint>> roomWaypoints = new();

    public void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints)
    {
        if (room == null)
        {
            Debug.LogWarning("Cannot register waypoints without a room.");
            return;
        }

        roomWaypoints[room] = waypoints?
            .Where(waypoint => waypoint != null)
            .ToList() ?? new List<RoomWaypoint>();
    }

    public void UnregisterRoomWaypoints(RoomManager room)
    {
        roomWaypoints.Remove(room);
    }

    public List<RoomWaypoint> GetAllWaypoints() =>
        roomWaypoints.Values.SelectMany(list => list).Where(waypoint => waypoint != null).ToList();

    public List<RoomWaypoint> GetActiveWaypoints() =>
        roomWaypoints.Values
            .SelectMany(list => list)
            .Where(waypoint => waypoint != null && waypoint.IsAvailable)
            .ToList();
}
