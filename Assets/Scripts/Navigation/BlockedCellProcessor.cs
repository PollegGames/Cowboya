using System.Collections.Generic;
using UnityEngine;

public class BlockedCellProcessor : CellProcessor
{
    public BlockedCellProcessor(int gridWidth, int gridHeight) : base(gridWidth, gridHeight) { }

    public override void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid)
    {
        foreach (var kvp in cellDataGrid)
        {
            Vector2 pos = kvp.Key;
            Cell cell = kvp.Value;

            // Check right neighbor
            Vector2 rightPos = new Vector2(pos.x + 1, pos.y);
            if (cellDataGrid.TryGetValue(rightPos, out Cell rightCell))
            {
                if (rightCell.cellProperties.usageType == UsageType.Blocked)
                {
                    BlockDoor(cell, DoorDirection.Right);
                }
            }

            // Check left neighbor
            Vector2 leftPos = new Vector2(pos.x - 1, pos.y);
            if (cellDataGrid.TryGetValue(leftPos, out Cell leftCell))
            {
                if (leftCell.cellProperties.usageType == UsageType.Blocked)
                {
                    BlockDoor(cell, DoorDirection.Left);
                }
            }
        }
    }
}