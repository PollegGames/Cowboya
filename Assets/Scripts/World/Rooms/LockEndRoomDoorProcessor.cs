using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Processes lock end room door data.
/// </summary>
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
                // The trigger reads this flag from the End room's RoomProperties.
                // Keep it on the same badge-locked door instead of marking every
                // room that happens to touch the edge of the generated map.
                MarkVictoryDoorIfOnEdge(cell);
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
        }
    }
}
