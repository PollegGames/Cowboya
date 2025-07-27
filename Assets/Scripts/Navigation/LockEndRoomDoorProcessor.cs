using System.Collections.Generic;
using UnityEngine;

public class LockEndRoomDoorProcessor : CellProcessor
{
    public LockEndRoomDoorProcessor(int gridWidth, int gridHeight) : base(gridWidth, gridHeight) { }

    public override void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid)
    {
        foreach (var kvp in cellDataGrid)
        {
            Vector2 pos = kvp.Key;
            Cell cell = kvp.Value;
            // Lock all doors if this cell is an End room
            if (cell.cellProperties.usageType == UsageType.End)
            {
                LockDoor(cell, DoorDirection.Left);
                LockDoor(cell, DoorDirection.Right);
            }

            // Check right neighbor
            Vector2 rightPos = new Vector2(pos.x + 1, pos.y);
            if (cellDataGrid.TryGetValue(rightPos, out Cell rightCell))
            {
                if (rightCell.cellProperties.usageType == UsageType.End)
                {
                    LockDoor(cell, DoorDirection.Right);
                }
            }

            // Check left neighbor
            Vector2 leftPos = new Vector2(pos.x - 1, pos.y);
            if (cellDataGrid.TryGetValue(leftPos, out Cell leftCell))
            {
                if (leftCell.cellProperties.usageType == UsageType.End)
                {
                    LockDoor(cell, DoorDirection.Left);
                }
            }
            // Mark the door as a victory door if this End room is at the map edge
            MarkVictoryDoorIfOnEdge(cell);
        }
    }
}