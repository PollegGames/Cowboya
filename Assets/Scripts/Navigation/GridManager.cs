using System.Collections.Generic;
using UnityEngine;

public class GridManager
{
    private int gridWidth, gridHeight, cellWidth, cellHeight, wallCount, pointsOfInterestCount;
    private GridFactory gridFactory;

    public GridManager(int gridWidth, int gridHeight, int cellWidth, int cellHeight, int wallCount, int pointsOfInterestCount)
    {
        this.gridWidth = gridWidth;
        this.gridHeight = gridHeight;
        this.cellWidth = cellWidth;
        this.cellHeight = cellHeight;
        this.wallCount = wallCount;
        this.pointsOfInterestCount = pointsOfInterestCount;
        gridFactory = new GridFactory();
    }

    public Dictionary<Vector2, Cell> CreateAndInitializeGrid()
    {
        gridFactory.CreateGrid(gridWidth, gridHeight, 0);
        gridFactory.AssignStartAndEndCells(new EndpointsFactory(), gridWidth, gridHeight);
        gridFactory.AssignPOICells(new EndpointsFactory(), pointsOfInterestCount, gridWidth, gridHeight);
        gridFactory.SolvePaths(pointsOfInterestCount, wallCount, new PathSolver(gridFactory, new PathFinder()), gridHeight, gridWidth);
        gridFactory.AssignBlockedCells(wallCount);

        return gridFactory.cellDataGrid;
    }

    public Vector3 GetRandomPointOnGrid()
    {
        int randomX = Random.Range(0, gridWidth);
        int randomY = Random.Range(0, gridHeight);

        return new Vector3(randomX * cellWidth, randomY * cellHeight, 0f);
    }

    public void ProcessRooms(List<ICellProcessor> roomProcessors, Dictionary<Vector2, Cell> cellDataGrid)
    {

        foreach (var processor in roomProcessors)
        {
            processor.ProcessCells(cellDataGrid);
        }
    }

    public void AssignRoomProperties(
        Dictionary<Vector2, GameObject> roomInstances,
        Dictionary<Vector2, Cell> cellDataGrid)
    {
        foreach (var kvp in roomInstances)
        {
            Vector2 pos = kvp.Key;
            var roomGO = kvp.Value;

            if (cellDataGrid.TryGetValue(pos, out var cell))
            {
                var roomProps = roomGO.GetComponent<RoomProperties>() ?? roomGO.AddComponent<RoomProperties>();
              
                roomProps.usageType = cell.cellProperties.usageType;
                roomProps.HasLeftDoor = cell.cellProperties.HasLeftDoor;
                roomProps.HasRightDoor = cell.cellProperties.HasRightDoor;
                roomProps.HasLeftDoorLocked = cell.cellProperties.HasLeftDoorLocked;
                roomProps.HasRightDoorLocked = cell.cellProperties.HasRightDoorLocked;
                roomProps.HasLiftUp = cell.cellProperties.HasLiftUpBlocked;
                roomProps.HasLiftDown = cell.cellProperties.HasLiftDownBlocked;
                roomProps.GridPosition = cell.cellProperties.GridPosition;
            }
        }
    }

}