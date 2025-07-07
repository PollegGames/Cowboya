using System.Collections.Generic;
using UnityEngine;

public interface IRoomRenderer
{
    Dictionary<Vector2, GameObject> RenderRooms(
        Dictionary<Vector2, Cell> cellDataGrid,
        Dictionary<UsageType, GameObject> usageMapping,
        Dictionary<POIType, GameObject> poiMapping,
        Vector2 cellSize,
        Vector3 offset,
        Transform parent,
        GameObject defaultPrefab);
}
