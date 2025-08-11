using System.Collections.Generic;
using UnityEngine;

public class RoomLiftPerFloorProcessor : CellProcessor
{
    public RoomLiftPerFloorProcessor(int gridWidth, int gridHeight) : base(gridWidth, gridHeight) { }

    public override void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid)
    {
        for (int y = 0; y < height; y++)
        {
            bool hasPath = false;
            foreach (var kvp in cellDataGrid)
            {
                Vector2 pos = kvp.Key;
                if (pos.y == y && kvp.Value.cellProperties.usageType == UsageType.PathToPOI)
                {
                    hasPath = true;
                    break;
                }
            }

            if (hasPath)
            {
                continue;
            }

            Cell candidate = FindCandidate(cellDataGrid, y, UsageType.Blocked);
            if (candidate == null)
            {
                candidate = FindCandidate(cellDataGrid, y, UsageType.POI);
            }
            if (candidate == null)
            {
                candidate = FindCandidate(cellDataGrid, y, UsageType.Empty);
            }

            if (candidate != null)
            {
                candidate.cellProperties.usageType = UsageType.PathToPOI;
            }
            else
            {
                Debug.LogError($"No suitable cell found on level {y} to set PathToPOI");
            }
        }
    }

    private Cell FindCandidate(Dictionary<Vector2, Cell> grid, int yLevel, UsageType type)
    {
        foreach (var kvp in grid)
        {
            Vector2 pos = kvp.Key;
            if (pos.y == yLevel && kvp.Value.cellProperties.usageType == type)
            {
                return kvp.Value;
            }
        }
        return null;
    }
}
