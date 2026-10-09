using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Creates grid instances.
/// </summary>
public class GridFactory
{
    public Dictionary<Vector2, Cell> cellDataGrid;
    private Vector2 startPosition, endPosition;
    private List<Vector2> poiPositions = new();
    private List<List<Vector2>> poiPaths = new();
    private List<bool> poiSuccess;

    public void CreateGrid(int width, int height)
    {
        var grid = new Dictionary<Vector2, Cell>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var cell = new Cell(new Vector2(x, y), UsageType.Work);
                cell.cellProperties.GridPosition = new Vector2Int(x, y);
                cell.cellProperties.poiType = POIType.None;
                grid[new Vector2(x, y)] = cell;
            }
        }

        cellDataGrid = grid;
    }

    public void AssignStartAndEndCells(EndpointsFactory endpointsFactory, int gridWidth, int gridHeight)
    {
        if (cellDataGrid == null || cellDataGrid.Count < 2)
            throw new System.InvalidOperationException("A generated map requires at least two cells for distinct Start and End positions.");

        endpointsFactory.GetCornerEndpoints(gridWidth, gridHeight, out startPosition, out endPosition);
        cellDataGrid[startPosition].cellProperties.usageType = UsageType.Start;
        cellDataGrid[endPosition].cellProperties.usageType = UsageType.End;
    }

    public void AssignPOICells(
        EndpointsFactory endpointsFactory,
        int pointsOfInterestCount,
        int gridWidth, int gridHeight,
        float morality = 0f)
    {
        // Exclude start and end positions from POI candidates
        var excluded = new HashSet<Vector2> { startPosition, endPosition };

        // Collect all eligible positions (work cells only)
        var eligibleCells = cellDataGrid
            .Where(kvp => kvp.Value.cellProperties.usageType == UsageType.Work && !excluded.Contains(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();

        // Shuffle eligible cells
        for (int i = eligibleCells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = eligibleCells[i];
            eligibleCells[i] = eligibleCells[j];
            eligibleCells[j] = temp;
        }

        int requestedCount = pointsOfInterestCount;
        int effectiveCount = Mathf.Clamp(requestedCount, 0, eligibleCells.Count);
        if (requestedCount < 0)
            Debug.LogWarning($"Invalid POI count {requestedCount}; using 0.");
        else if (requestedCount > eligibleCells.Count)
            Debug.LogWarning($"Requested {requestedCount} POIs, but only {effectiveCount} eligible Work cells are available; using {effectiveCount}.");

        // Pick the first N as POIs. The shuffled list and type pool both have stable order.
        poiPositions = eligibleCells.Take(effectiveCount).ToList();
        for (int i = 0; i < poiPositions.Count; i++)
        {
            cellDataGrid[poiPositions[i]].cellProperties.usageType = UsageType.POI;
            cellDataGrid[poiPositions[i]].cellProperties.POISlot = i + 1;
            var poiType = POIRoomTypeSelector.Select(i + 1, morality);
            cellDataGrid[poiPositions[i]].cellProperties.poiType = poiType;
        }

    }

    public void SolvePaths(
     PathSolver pathSolver,
     int gridWidth, int gridHeight)
    {
        // Add the endPosition to the list of POIs
        var allPOIs = new List<Vector2>(poiPositions) { endPosition };

        poiPaths = new List<List<Vector2>>(new List<Vector2>[allPOIs.Count]);
        poiSuccess = new List<bool>(new bool[allPOIs.Count]);

        // Calculate paths from start to each POI (including the endPosition)
        for (int i = 0; i < allPOIs.Count; i++)
        {
            poiSuccess[i] = pathSolver.TryFindPath(
                startPosition, allPOIs[i],
                out List<Vector2> path);

            if (poiSuccess[i])
            {
                poiPaths[i] = path;
                foreach (var pos in path)
                {
                    if (cellDataGrid[pos].cellProperties.usageType == UsageType.Work)
                        cellDataGrid[pos].cellProperties.usageType = UsageType.PathToPOI;
                }
            }
        }
    }
}

/// <summary>
/// Selects generated POI room types according to their one-based slot.
/// </summary>
public static class POIRoomTypeSelector
{
    private static readonly POIType[] RandomPool =
    {
        POIType.Resting,
        POIType.Security,
        POIType.Spawning,
        POIType.Furnace,
        POIType.Junks,
        POIType.Deads,
        POIType.CubeCollector
    };

    private static readonly float[] GoodWeights = { 0.5f, 4f, 0.25f, 0.5f, 3f, 0.5f, 4f };
    private static readonly float[] BadWeights = { 0.5f, 0.25f, 3f, 4f, 0.5f, 2f, 0.25f };

    /// <summary>Returns the room type for a one-based POI slot.</summary>
    public static POIType Select(int slot, float morality = 0f)
    {
        if (slot < 1)
            throw new System.ArgumentOutOfRangeException(nameof(slot), "POI slots are one-based.");
        if (slot == 1)
            return POIType.Resting;
        if (slot == 2)
            return POIType.Security;
        if (slot == 3)
            return POIType.Reception;
        if (slot == 4)
            return POIType.Spawning;
        if (slot == 5)
            return POIType.Garage;
        if (slot == 6)
            return POIType.Conveyor;
        return SelectWeighted(morality);
    }

    /// <summary>Returns the configured random-selection weight for a room and morality value.</summary>
    public static float GetWeight(POIType type, float morality)
    {
        int index = System.Array.IndexOf(RandomPool, type);
        if (index < 0)
            return 0f;

        float influence = morality >= 0f
            ? Mathf.Clamp01(morality / 5f)
            : Mathf.Clamp01(-morality / 20f);
        float targetWeight = morality >= 0f ? GoodWeights[index] : BadWeights[index];
        return Mathf.Lerp(1f, targetWeight, influence);
    }

    private static POIType SelectWeighted(float morality)
    {
        float totalWeight = 0f;
        for (int i = 0; i < RandomPool.Length; i++)
            totalWeight += GetWeight(RandomPool[i], morality);

        float choice = UnityEngine.Random.value * totalWeight;
        for (int i = 0; i < RandomPool.Length; i++)
        {
            choice -= GetWeight(RandomPool[i], morality);
            if (choice <= 0f)
                return RandomPool[i];
        }

        return RandomPool[RandomPool.Length - 1];
    }
}
