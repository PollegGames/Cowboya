using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AxisNeighborConnector : INeighborConnector
{
    private readonly Axis axis;
    private readonly Bidirection bidirection;
    private readonly bool invert;
    private readonly Func<RoomWaypoint, bool> filter;

    public AxisNeighborConnector(Axis axis, Bidirection bidirection, bool invert = false,
        Func<RoomWaypoint, bool> filter = null)
    {
        this.axis = axis;
        this.bidirection = bidirection;
        this.invert = invert;
        this.filter = filter ?? (_ => true);
    }

    public void Connect(IEnumerable<RoomWaypoint> allWaypoints)
    {
        var waypoints = allWaypoints.Where(filter).ToList();

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
}
