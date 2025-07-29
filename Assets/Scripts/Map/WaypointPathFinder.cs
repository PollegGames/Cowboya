using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WaypointPathFinder : MonoBehaviour, IPathFinder
{
    [SerializeField] private MonoBehaviour registryBehaviour;
    private IWaypointRegistry registry;
    private List<INeighborConnector> connectors;

    private void Awake()
    {
        registry = registryBehaviour as IWaypointRegistry;
        connectors = new List<INeighborConnector>
        {
            new AxisNeighborConnector(Axis.Horizontal, Bidirection.Both),
            new AxisNeighborConnector(Axis.Vertical, Bidirection.Forward,
                filter: wp => wp.type == WaypointType.LiftGoingUp),
            new AxisNeighborConnector(Axis.Vertical, Bidirection.Forward, invert: true,
                filter: wp => wp.type == WaypointType.LiftGoingDown)
        };
    }

    public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end)
    {
        if (start == null || end == null)
        {
            Debug.LogWarning("FindWorldPath called with a null waypoint.");
            return new List<RoomWaypoint>();
        }

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

    public void BuildAllNeighbors(bool includeUnavailable = false)
    {
        var allWaypoints = registry.GetAllWaypoints();
        foreach (var wp in allWaypoints)
            wp.Neighbors.Clear();

        foreach (var connector in connectors)
            connector.Connect(allWaypoints, includeUnavailable);

        var groupingBase = includeUnavailable ? allWaypoints : registry.GetActiveWaypoints();
        var groups = groupingBase.GroupBy(wp => wp.parentRoom);
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
