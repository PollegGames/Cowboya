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

            // If this cell itself is blocked, lock both its left and right doors
            if (cell.cellProperties.usageType == UsageType.Blocked)
            {
                LockDoor(cell, DoorDirection.Left);
                LockDoor(cell, DoorDirection.Right);
            }

            // Check right neighbor for blocked cells
            Vector2 rightPos = new Vector2(pos.x + 1, pos.y);
            if (cellDataGrid.TryGetValue(rightPos, out Cell rightCell)
                && rightCell.cellProperties.usageType == UsageType.Blocked)
            {
                // Lock the door on this cell facing right
                LockDoor(cell, DoorDirection.Right);
                // Also lock the corresponding left door on the blocked neighbor
                LockDoor(rightCell, DoorDirection.Left);
            }

            // Check left neighbor for blocked cells
            Vector2 leftPos = new Vector2(pos.x - 1, pos.y);
            if (cellDataGrid.TryGetValue(leftPos, out Cell leftCell)
                && leftCell.cellProperties.usageType == UsageType.Blocked)
            {
                // Lock the door on this cell facing left
                LockDoor(cell, DoorDirection.Left);
                // Also lock the corresponding right door on the blocked neighbor
                LockDoor(leftCell, DoorDirection.Right);
            }
        }
    }
}
