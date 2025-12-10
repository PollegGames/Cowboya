using System.Collections.Generic;
using UnityEngine;

public class LockEndRoomDoorProcessor : CellProcessor
{
    private readonly bool noBlockRequiredWhenZeroEnemies;

    public LockEndRoomDoorProcessor(int gridWidth, int gridHeight, bool noBlockRequiredWhenZeroEnemies) : base(gridWidth, gridHeight)
    {
        this.noBlockRequiredWhenZeroEnemies = noBlockRequiredWhenZeroEnemies;
    }

    public override void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid)
    {
        foreach (var kvp in cellDataGrid)
        {
            Vector2 pos = kvp.Key;
            Cell cell = kvp.Value;
            // When no enemies exist we keep end-room doors unlocked instead of locking them
            var doorAction = noBlockRequiredWhenZeroEnemies ? (System.Action<Cell, DoorDirection>)UnlockDoor : LockDoor;

            // Lock all doors if this cell is an End room
            if (cell.cellProperties.usageType == UsageType.End)
            {
                doorAction(cell, DoorDirection.Left);
                doorAction(cell, DoorDirection.Right);
            }

            // Check right neighbor
            Vector2 rightPos = new Vector2(pos.x + 1, pos.y);
            if (cellDataGrid.TryGetValue(rightPos, out Cell rightCell))
            {
                if (rightCell.cellProperties.usageType == UsageType.End)
                {
                    doorAction(cell, DoorDirection.Right);
                }
            }

            // Check left neighbor
            Vector2 leftPos = new Vector2(pos.x - 1, pos.y);
            if (cellDataGrid.TryGetValue(leftPos, out Cell leftCell))
            {
                if (leftCell.cellProperties.usageType == UsageType.End)
                {
                    doorAction(cell, DoorDirection.Left);
                }
            }
            // Mark the door as a victory door if this End room is at the map edge
            MarkVictoryDoorIfOnEdge(cell);
        }
    }
}
