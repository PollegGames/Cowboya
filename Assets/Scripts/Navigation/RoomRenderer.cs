using System.Collections.Generic;
using UnityEngine;

public class RoomRenderer : MonoBehaviour, IRoomRenderer
{
    public Dictionary<Vector2, GameObject> RenderRooms(
        Dictionary<Vector2, Cell> cellDataGrid,
        Dictionary<UsageType, GameObject> usageMapping,
        Dictionary<POIType, GameObject> poiMapping,
        Vector2 cellSize,
        Vector3 offset,
        Transform parent,
        GameObject defaultPrefab)
    {
        var renderer = new GridRenderer(usageMapping, poiMapping, parent);
        return renderer.Render(cellDataGrid, cellSize, offset, defaultPrefab);
    }
}
