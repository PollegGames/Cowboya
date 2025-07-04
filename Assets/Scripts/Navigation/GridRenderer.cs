using System.Collections.Generic;
using UnityEngine;

public class GridRenderer
{
    private readonly Dictionary<UsageType, GameObject> usageTypeToPrefab;
    private readonly Dictionary<POIType, GameObject> poiTypeToPrefab;
    private readonly Transform gridParent;
    private readonly Dictionary<Vector2, GameObject> instances = new();

    public GridRenderer(Dictionary<UsageType, GameObject> mapping, Dictionary<POIType, GameObject> poiMapping, Transform parent)
    {
        usageTypeToPrefab = mapping;
        poiTypeToPrefab = poiMapping;
        gridParent = parent;
        Debug.Log("GridRenderer initialized with parent: " + parent.name + " and prefab mapping: " + string.Join(", ", mapping.Keys));

    }

    public Dictionary<Vector2, GameObject> Render(
        Dictionary<Vector2, Cell> cells,
        Vector2 cellSize,
        Vector3 offset,
        GameObject defaultPrefab)
    {
        instances.Clear();
        foreach (var kvp in cells)
        {
            // Place at the center of the cell, not the bottom-left
            var spawnPos = new Vector3(
                kvp.Key.x * cellSize.x + cellSize.x * 0.5f,
                kvp.Key.y * cellSize.y + cellSize.y * 0.5f,
                0f
            ) + offset;
            if (kvp.Value.cellProperties.usageType == UsageType.POI)
            {
                // For POI, use the POIType to determine the prefab
                var poiPrefab = poiTypeToPrefab.TryGetValue(kvp.Value.cellProperties.poiType, out var poiGo) ? poiGo : defaultPrefab;
                var tile = Object.Instantiate(poiPrefab, spawnPos, poiPrefab.transform.rotation, gridParent);
                instances[kvp.Key] = tile;
            }
            else
            {

                var prefab = usageTypeToPrefab.TryGetValue(kvp.Value.cellProperties.usageType, out var go) ? go : defaultPrefab;
                var tile = Object.Instantiate(prefab, spawnPos, prefab.transform.rotation, gridParent);
                instances[kvp.Key] = tile;
            }

        }
        return instances;
    }

}