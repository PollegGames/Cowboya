using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum Axis { Horizontal, Vertical }
public enum Bidirection { Both, Forward }

public class WaypointService : MonoBehaviour
{
    private readonly Dictionary<RoomManager, List<RoomWaypoint>> roomWaypoints = new();
    private readonly HashSet<IRobotNavigationListener> robots = new();
    private readonly HashSet<RoomWaypoint> _reservedWaypoints = new();

    // Usage tracking
    private readonly Dictionary<RoomWaypoint, int> _workUsageCounts = new();
    private readonly Dictionary<RoomWaypoint, int> _securityUsageCounts = new();

    #region Registration & Notification
    public void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints)
    {
        roomWaypoints[room] = waypoints.ToList();
    }

    public void UnregisterRoomWaypoints(RoomManager room)
    {
        roomWaypoints.Remove(room);
    }

    public void Subscribe(IRobotNavigationListener robot) => robots.Add(robot);
    public void Unsubscribe(IRobotNavigationListener robot) => robots.Remove(robot);

    public void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable)
    {
        if (!isAvailable)
            foreach (var robot in robots)
                robot.OnPathObsoleted(changed);
    }
    #endregion

    #region Waypoint Queries & Pathfinding
    public List<RoomWaypoint> GetActiveWaypoints() =>
        roomWaypoints.Values.SelectMany(l => l).Where(wp => wp.IsAvailable).ToList();

    public List<RoomWaypoint> GetAllWaypoints() =>
        roomWaypoints.Values.SelectMany(l => l).ToList();

    public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end)
    {
        var queue = new Queue<RoomWaypoint>();
        var cameFrom = new Dictionary<RoomWaypoint, RoomWaypoint>();
        var visited = new HashSet<RoomWaypoint>();

        queue.Enqueue(start);
        visited.Add(start);
        cameFrom[start] = null;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbor in current.Neighbors)
            {
                if (visited.Contains(neighbor)) continue;
                queue.Enqueue(neighbor);
                visited.Add(neighbor);
                cameFrom[neighbor] = current;
                if (neighbor == end)
                {
                    var path = new List<RoomWaypoint>();
                    for (var step = neighbor; step != null; step = cameFrom[step])
                        path.Add(step);
                    path.Reverse();
                    return path;
                }
            }
        }
        return new List<RoomWaypoint>();
    }

    public void BuildAllNeighbors()
    {
        var allWaypoints = GetAllWaypoints();
        foreach (var wp in allWaypoints)
            wp.Neighbors.Clear();

        var activeWaypoints = GetActiveWaypoints();

        ConnectByAxis(allWaypoints, Axis.Horizontal, Bidirection.Both);
        var liftsUp = allWaypoints.Where(wp => wp.type == WaypointType.LiftGoingUp).ToList();
        ConnectByAxis(liftsUp, Axis.Vertical, Bidirection.Forward);
        var liftsDown = allWaypoints.Where(wp => wp.type == WaypointType.LiftGoingDown).ToList();
        ConnectByAxis(liftsDown, Axis.Vertical, Bidirection.Forward, invert: true);

        var groups = activeWaypoints.GroupBy(wp => wp.parentRoom);
        foreach (var group in groups)
        {
            var waypoints = group.ToList();
            for (int i = 0; i < waypoints.Count; i++)
                for (int j = i + 1; j < waypoints.Count; j++)
                {
                    var wpA = waypoints[i];
                    var wpB = waypoints[j];
                    if (!wpA.Neighbors.Contains(wpB)) wpA.Neighbors.Add(wpB);
                    if (!wpB.Neighbors.Contains(wpA)) wpB.Neighbors.Add(wpA);
                }
        }
    }

    private void ConnectByAxis(List<RoomWaypoint> waypoints, Axis axis, Bidirection bidirection, bool invert = false)
    {
        Func<RoomWaypoint, int> cellSelector = axis == Axis.Horizontal
            ? wp => Mathf.RoundToInt(wp.WorldPos.y)
            : wp => Mathf.RoundToInt(wp.WorldPos.x);

        Func<RoomWaypoint, float> sortSelector = axis == Axis.Horizontal
            ? wp => wp.WorldPos.x
            : wp => wp.WorldPos.y;

        var groups = waypoints.GroupBy(cellSelector);
        foreach (var group in groups)
        {
            var sorted = invert
                ? group.OrderByDescending(sortSelector).ToList()
                : group.OrderBy(sortSelector).ToList();

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var current = sorted[i];
                var next = sorted[i + 1];
                if (current.IsAvailable && next.IsAvailable)
                {
                    if (!current.Neighbors.Contains(next)) current.Neighbors.Add(next);
                    if (bidirection == Bidirection.Both && !next.Neighbors.Contains(current))
                        next.Neighbors.Add(current);
                }
            }
        }
    }
    #endregion

    #region Work & Rest Assignment
    /// <summary>
    /// Assigns the least-used work waypoint (empty usageType) and reserves it.
    /// </summary>
    public RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null)
    {
        var works = GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                         && wp.type == WaypointType.Work
                         && wp != exclude
                         && !_reservedWaypoints.Contains(wp))
            .ToList();
        if (!works.Any())
        {
            works = GetActiveWaypoints()
                .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                            && wp.type == WaypointType.Work
                         && wp != exclude)
                .ToList();
        }

        var best = works.OrderBy(wp => _workUsageCounts.TryGetValue(wp, out var c) ? c : 0).First();
        _workUsageCounts[best] = _workUsageCounts.TryGetValue(best, out var count) ? count + 1 : 1;
        _reservedWaypoints.Add(best);
        Debug.Log($"[WaypointService] Assigned EMPTY WORK '{best.name}' (count={_workUsageCounts[best]}).");
        return best;
    }

    public RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null)
    {
        var works = GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.Empty
                         && wp.type == WaypointType.Center
                            && wp != exclude
                         && !_reservedWaypoints.Contains(wp))
            .ToList();

        if (works.Any())
        {
            // Return the least-used available work point
            var best = works.OrderBy(wp => _workUsageCounts.TryGetValue(wp, out var c) ? c : 0).First();
            _workUsageCounts[best] = _workUsageCounts.TryGetValue(best, out var count) ? count + 1 : 1;
            _reservedWaypoints.Add(best);
            Debug.Log($"[WaypointService] Assigned EMPTY WORK '{best.name}' (count={_workUsageCounts[best]}).");
            return best;
        }

        // Otherwise, fallback to rest point
        var rest = GetFirstRestPoint(exclude);
        if (rest != null)
        {
            _reservedWaypoints.Add(rest);
            Debug.Log($"[WaypointService] Assigned REST '{rest.name}'.");
        }
        return rest;
    }

    /// <summary>
    /// Returns the least-used rest waypoint (POI usageType) and reserves it.
    /// </summary>
    public RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null)
    {
        var rests = GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.POI
                         && wp.type == WaypointType.Rest
                         && wp != exclude)
            .ToList();
        return rests.FirstOrDefault();
    }

    public void ReleasePOI(RoomWaypoint poi)
    {
        if (poi == null) return;
        _reservedWaypoints.Remove(poi);
        if (_workUsageCounts.TryGetValue(poi, out var wc) && wc > 0) _workUsageCounts[poi] = wc - 1;
        if (_securityUsageCounts.TryGetValue(poi, out var sc) && sc > 0) _securityUsageCounts[poi] = sc - 1;
        Debug.Log($"[WaypointService] Released POI '{poi.name}'.");

        OnPOIReleased?.Invoke(poi);
    }
    #endregion

    #region Security Assignment
    /// <summary>
    /// Finds the first free security work waypoint.
    /// </summary>
    public RoomWaypoint GetFirstFreeSecurityPoint()
    {
        var security = GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.POI
                         && wp.type == WaypointType.Security
                         && !_reservedWaypoints.Contains(wp))
            .ToList();
        if (!security.Any())
        {
            return GetFirstRestPoint();
        }

        var best = security.OrderBy(wp => _securityUsageCounts.TryGetValue(wp, out var c) ? c : 0).First();
        _securityUsageCounts[best] = _securityUsageCounts.TryGetValue(best, out var count) ? count + 1 : 1;
        _reservedWaypoints.Add(best);
        Debug.Log($"[WaypointService] Assigned SECURITY point '{best.name}' (count={_securityUsageCounts[best]}).");
        return best;
    }

    #endregion

    #region Utility
    public RoomWaypoint GetClosestWaypoint(Vector2 position)
    {
        return GetActiveWaypoints()
            .OrderBy(wp => Vector2.Distance(wp.WorldPos, position))
            .FirstOrDefault();
    }

    public void UpdateClosestWaypointToPlayer(Vector2 playerPosition)
    {
        ClosestWaypointToPlayer = GetClosestWaypoint(playerPosition);
    }

    public RoomWaypoint GetEndPoint()
    {
        var endPoints = GetActiveWaypoints()
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.End
            && wp.type == WaypointType.Center)
            .ToList();

        if (endPoints.Count == 0)
        {
            Debug.LogWarning("[WaypointService] No end points found.");
            return null;
        }

        // Return the first end point found
        return endPoints[0];
    }

    public event Action<RoomWaypoint> OnPOIReleased;


    public RoomWaypoint ClosestWaypointToPlayer { get; private set; }
    #endregion
}
