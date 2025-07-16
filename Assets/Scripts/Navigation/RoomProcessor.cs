using System.Collections.Generic;
using UnityEngine;

public class RoomProcessor : MonoBehaviour, IRoomProcessor
{
    public void ProcessRooms(Dictionary<Vector2, Cell> cellDataGrid, int width, int height)
    {
        var processors = new List<ICellProcessor>
        {
            new PathCellProcessor(width, height, UsageType.PathToPOI),
            new BlockedCellProcessor(width, height),
            new LockEndRoomDoorProcessor(width, height),
            new EdgeCellProcessor(width, height),
        };

        foreach (var p in processors)
            p.ProcessCells(cellDataGrid);
    }
}
