using System.Collections.Generic;
using UnityEngine;

public class EdgeCellProcessor : CellProcessor
{
    public EdgeCellProcessor(int gridWidth, int gridHeight) : base(gridWidth, gridHeight) { }

    public override void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid)
    {
        foreach (var kvp in cellDataGrid)
        {
            Vector2 pos = kvp.Key;
            Cell roomCell = kvp.Value;

            if (pos.x == 0) BlockDoor(roomCell, DoorDirection.Left);
            if (pos.x == width - 1)
            {
                BlockDoor(roomCell, DoorDirection.Right);
                // Debug.Log($"Locked right door at {pos}");
            }
            if (pos.y == 0) BlockLift(roomCell, LiftDirection.Down);
            if (pos.y == height - 1) BlockLift(roomCell, LiftDirection.Up);
        }
    }
}