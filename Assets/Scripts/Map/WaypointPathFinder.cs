using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaypointPathFinder
{
    private readonly WaypointRegistry registry;

    public WaypointPathFinder(WaypointRegistry registry)
    {
        this.registry = registry;
    }

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
        var allWaypoints = registry.GetAllWaypoints();
        foreach (var wp in allWaypoints)
            wp.Neighbors.Clear();

        var activeWaypoints = registry.GetActiveWaypoints();

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

    public void ConnectByAxis(List<RoomWaypoint> waypoints, Axis axis, Bidirection bidirection, bool invert = false)
    {
        System.Func<RoomWaypoint, int> cellSelector = axis == Axis.Horizontal
            ? wp => Mathf.RoundToInt(wp.WorldPos.y)
            : wp => Mathf.RoundToInt(wp.WorldPos.x);

        System.Func<RoomWaypoint, float> sortSelector = axis == Axis.Horizontal
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
}
