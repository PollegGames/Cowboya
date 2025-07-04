using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaypointPathFinder
{
    private readonly WaypointRegistry registry;
    private readonly List<INeighborConnector> connectors;

    public WaypointPathFinder(WaypointRegistry registry, params INeighborConnector[] connectors)
    {
        this.registry = registry;
        this.connectors = connectors?.ToList() ?? new List<INeighborConnector>();
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

        foreach (var connector in connectors)
            connector.Connect(allWaypoints);

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

}
