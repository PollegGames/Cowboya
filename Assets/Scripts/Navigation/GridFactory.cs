using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
                var cell = new Cell(new Vector2(x, y), UsageType.Empty);
                cell.cellProperties.GridPosition = new Vector2Int(x, y);
                cell.cellProperties.poiType = POIType.None;
                grid[new Vector2(x, y)] = cell;
            }
        }

        cellDataGrid = grid;
    }

    public void AssignStartAndEndCells(EndpointsFactory endpointsFactory, int gridWidth, int gridHeight)
    {
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

        // Collect all eligible positions (empty cells only)
        var eligibleCells = cellDataGrid
            .Where(kvp => kvp.Value.cellProperties.usageType == UsageType.Empty && !excluded.Contains(kvp.Key))
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

        // Pick the first N as POIs
        poiPositions = eligibleCells.Take(pointsOfInterestCount).ToList();

        for (int i = 0; i < poiPositions.Count; i++)
        {
            cellDataGrid[poiPositions[i]].cellProperties.usageType = UsageType.POI;

            if (i == 0)
            {
                cellDataGrid[poiPositions[i]].cellProperties.poiType = POIType.Reception;
            }
            else if (i > 0 && i < 5)
            {
                cellDataGrid[poiPositions[i]].cellProperties.poiType = POIType.Security;
            }
            else
            {
                cellDataGrid[poiPositions[i]].cellProperties.poiType = POIType.None;
            }
        }

    }

    public void AssignBlockedCells(int wallCount)
    {
        // 1. Collect all empty cells
        var emptyCells = cellDataGrid.Values
            .Where(cell => cell.cellProperties.usageType == UsageType.Empty)
            .ToList();

        // 2. Shuffle the list
        for (int i = emptyCells.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = emptyCells[i];
            emptyCells[i] = emptyCells[j];
            emptyCells[j] = temp;
        }

        // 3. Assign blocked type to the first wallCount cells
        int blockedCount = 0;
        foreach (var cell in emptyCells)
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
                    if (cellDataGrid[pos].cellProperties.usageType == UsageType.Empty)
                        cellDataGrid[pos].cellProperties.usageType = UsageType.PathToPOI;
                }
            }
        }
    }
}