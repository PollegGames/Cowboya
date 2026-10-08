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

    public void CreateGrid(int width, int height, int wallCount)
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
        int gridWidth, int gridHeight)
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
        var usedTypes = new HashSet<POIType>();

        for (int i = 0; i < poiPositions.Count; i++)
        {
            cellDataGrid[poiPositions[i]].cellProperties.usageType = UsageType.POI;
            var poiType = POIRoomTypeSelector.Select(i + 1, usedTypes);
            cellDataGrid[poiPositions[i]].cellProperties.poiType = poiType;
            usedTypes.Add(poiType);
        }

    }

    public void AssignBlockedCells(int wallCount)
    {
        // 1. Collect all work cells
        var workCells = cellDataGrid.Values
            .Where(cell => cell.cellProperties.usageType == UsageType.Work)
            .ToList();

        // 2. Shuffle the list
        for (int i = workCells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = workCells[i];
            workCells[i] = workCells[j];
            workCells[j] = temp;
        }

        // 3. Assign blocked type to the first wallCount cells
        int blockedCount = 0;
        foreach (var cell in workCells)
        {
            if (blockedCount >= wallCount)
                break;
            cell.cellProperties.usageType = UsageType.Blocked;
            cell.cellProperties.HasLeftDoor = false;
            cell.cellProperties.HasRightDoor = false;
            blockedCount++;
        }

    }

    public void SolvePaths(
     int pointsOfInterestCount,
     int wallCount,
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
    private static readonly POIType[] CompactPool = { POIType.Reception, POIType.Security };
    private static readonly POIType[] CompletePool =
    {
        POIType.Reception,
        POIType.Security,
        POIType.Resting,
        POIType.Spawning,
        POIType.Garage,
        POIType.Conveyor,
        POIType.Furnace,
        POIType.Junks,
        POIType.Deads,
        POIType.CubeCollector
    };

    /// <summary>Returns the room type for a one-based POI slot.</summary>
    public static POIType Select(int slot, ISet<POIType> usedTypes)
    {
        if (slot < 1)
            throw new System.ArgumentOutOfRangeException(nameof(slot), "POI slots are one-based.");
        if (usedTypes == null)
            throw new System.ArgumentNullException(nameof(usedTypes));
        if (slot == 1)
            return POIType.Reception;
        if (slot == 2)
            return POIType.Security;
        if (slot <= 5)
            return CompactPool[UnityEngine.Random.Range(0, CompactPool.Length)];

        var candidates = CompletePool.Where(type => !usedTypes.Contains(type)).ToArray();
        if (candidates.Length == 0)
            candidates = CompletePool;
        return candidates[UnityEngine.Random.Range(0, candidates.Length)];
    }
}
