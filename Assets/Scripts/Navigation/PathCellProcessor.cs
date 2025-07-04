using System.Collections.Generic;
using UnityEngine;

public class PathCellProcessor : CellProcessor
{
    private readonly UsageType pathToPOIUsageType;

    public PathCellProcessor(int gridWidth, int gridHeight, UsageType pathToPOIUsageType) : base(gridWidth, gridHeight)
    {
        this.pathToPOIUsageType = pathToPOIUsageType;
    }

    public override void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid)
    {
        var pathToPOIPositions = PrecomputePathToPOIPositions(cellDataGrid);

        foreach (var pos in pathToPOIPositions)
        {
            Cell room = cellDataGrid[pos];
            bool hasPathToPOICellAbove = pathToPOIPositions.Contains(new Vector2(pos.x, pos.y + 1));
            bool hasPathToPOICellBelow = pathToPOIPositions.Contains(new Vector2(pos.x, pos.y - 1));

            if (!hasPathToPOICellAbove) { BlockLift(room, LiftDirection.Up); }
            else
            { room.cellProperties.HasLiftUpBlocked = true; }
            if (!hasPathToPOICellBelow) { BlockLift(room, LiftDirection.Down); }
            else
            { room.cellProperties.HasLiftDownBlocked = true; }
        }
    }

    private HashSet<Vector2> PrecomputePathToPOIPositions(Dictionary<Vector2, Cell> cellDataGrid)
    {
        var pathToPOIPositions = new HashSet<Vector2>();
        foreach (var kvp in cellDataGrid)
        {
            if (kvp.Value.cellProperties.usageType == pathToPOIUsageType)
            {
                pathToPOIPositions.Add(kvp.Key);
            }
        }
        return pathToPOIPositions;
    }
}