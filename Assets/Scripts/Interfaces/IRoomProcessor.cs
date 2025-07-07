using System.Collections.Generic;
using UnityEngine;

public interface IRoomProcessor
{
    void ProcessRooms(Dictionary<Vector2, Cell> cellDataGrid, int width, int height);
}
