using System.Collections.Generic;
using UnityEngine;

public class RoomProcessor : MonoBehaviour, IRoomProcessor
{
    public void ProcessRooms(Dictionary<Vector2, Cell> cellDataGrid, int width, int height, bool noBlockRequiredWhenZeroEnemies)
    {
        var processors = new List<ICellProcessor>
        {
            new RoomLiftPerFloorProcessor(width, height),
            new PathToPOIToWorkProcessor(width, height),
            new PathCellProcessor(width, height, UsageType.PathToPOI),
            new BlockedCellProcessor(width, height),
            new LockEndRoomDoorProcessor(width, height, noBlockRequiredWhenZeroEnemies),
            new EdgeCellProcessor(width, height),
        };

        foreach (var p in processors)
            p.ProcessCells(cellDataGrid);
    }
}
