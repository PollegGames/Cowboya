using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Processes path to poi to work data.
/// </summary>
public class PathToPOIToWorkProcessor : CellProcessor
{
    public PathToPOIToWorkProcessor(int gridWidth, int gridHeight) : base(gridWidth, gridHeight) { }

    public override void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid)
    {
        var pathPositions = PrecomputePathPositions(cellDataGrid);

        foreach (var pos in pathPositions)
        {
            bool hasAbove = pathPositions.Contains(new Vector2(pos.x, pos.y + 1));
            bool hasBelow = pathPositions.Contains(new Vector2(pos.x, pos.y - 1));

            if (!hasAbove && !hasBelow)
            {
                Cell cell = cellDataGrid[pos];
                cell.cellProperties.usageType = UsageType.Work;
            }
        }
    }

    private HashSet<Vector2> PrecomputePathPositions(Dictionary<Vector2, Cell> cellDataGrid)
    {
        var positions = new HashSet<Vector2>();
        foreach (var kvp in cellDataGrid)
        {
            if (kvp.Value.cellProperties.usageType == UsageType.PathToPOI)
            {
                positions.Add(kvp.Key);
            }
        }
        return positions;
    }
}
