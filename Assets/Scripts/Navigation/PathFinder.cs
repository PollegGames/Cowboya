using System;
using System.Collections.Generic;
using UnityEngine;

public class PathFinder
{
    public bool FindPath(
    Dictionary<Vector2, Cell> cells,
    Vector2 startPos,
    Vector2 targetPos,
    List<Cell> finalPath)
    {
        var costs = new Dictionary<Cell, (int gCost, int hCost, int fCost, Cell connection)>();

        foreach (var cell in cells.Values)
        {
            costs[cell] = (int.MaxValue, 0, 0, null);
        }

        costs[cells[startPos]] = (0, Distance(startPos, targetPos), Distance(startPos, targetPos), null);

        var openSet = new PriorityQueue<Cell>((a, b) => costs[a].fCost.CompareTo(costs[b].fCost));
        var closedSet = new HashSet<Cell>();

        openSet.Enqueue(cells[startPos]);

        while (openSet.Count > 0)
        {
            Cell current = openSet.Dequeue();
            if (current == null) return false;

            closedSet.Add(current);

            if (current == cells[targetPos])
            {
                RetracePath(cells[startPos], cells[targetPos], finalPath, costs);
                return true;
            }

            foreach (Cell neighbor in GetNeighbors(cells, current, cells[startPos], cells[targetPos]))
            {
                if (neighbor.cellProperties.usageType == UsageType.Blocked || closedSet.Contains(neighbor))
                    continue;

                int newCost = costs[current].gCost + Distance(current.position, neighbor.position);
                if (newCost < costs[neighbor].gCost)
                {
                    costs[neighbor] = (newCost, Distance(neighbor.position, targetPos), newCost + Distance(neighbor.position, targetPos), current);

                    if (!openSet.Contains(neighbor))
                        openSet.Enqueue(neighbor);
                }
            }
        }

        return false;
    }

    private void RetracePath(Cell startCell, Cell targetCell, List<Cell> finalPath, Dictionary<Cell, (int gCost, int hCost, int fCost, Cell connection)> costs)
    {
        finalPath.Clear();
        var current = targetCell;
        while (current != startCell)
        {
            finalPath.Add(current);
            current = costs[current].connection;
        }
        finalPath.Add(startCell);
        finalPath.Reverse();
    }

    private IEnumerable<Cell> GetNeighbors(Dictionary<Vector2, Cell> cells, Cell cell, Cell startCell, Cell targetCell)
    {
        Vector2[] directions;

        // Default: all directions
        directions = new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.down };

        // If current cell is start or target, restrict to left/right
        if (cell.cellProperties.usageType == UsageType.Start
            || cell.cellProperties.usageType == UsageType.End
            || cell.cellProperties.usageType == UsageType.POI)
        {
            directions = new[] { Vector2.left, Vector2.right };
        }

        foreach (var dir in directions)
        {
            Vector2 nPos = cell.position + dir;
            if (cells.TryGetValue(nPos, out Cell neighbor))
            {
                // If neighbor is targetCell, only allow left/right connection
                if (neighbor == targetCell && (dir != Vector2.left && dir != Vector2.right))
                    continue;

                yield return neighbor;
            }
        }
    }

    private int Distance(Vector2 a, Vector2 b)
    {
        // Manhattan distance
        int dx = Mathf.Abs((int)a.x - (int)b.x);
        int dy = Mathf.Abs((int)a.y - (int)b.y);
        return 10 * (dx + dy);
    }
}
