using System.Collections.Generic;
using UnityEngine;

public class GridManager
{
    private int gridWidth, gridHeight, cellWidth, cellHeight, pointsOfInterestCount;
    private float morality;
    private GridFactory gridFactory;

    public GridManager(int gridWidth, int gridHeight, int cellWidth, int cellHeight, int pointsOfInterestCount, float morality = 0f)
    {
        this.gridWidth = gridWidth;
        this.gridHeight = gridHeight;
        this.cellWidth = cellWidth;
        this.cellHeight = cellHeight;
        this.pointsOfInterestCount = pointsOfInterestCount;
        this.morality = morality;
        gridFactory = new GridFactory();
    }

    public Dictionary<Vector2, Cell> CreateAndInitializeGrid()
    {
        gridFactory.CreateGrid(gridWidth, gridHeight);
        gridFactory.AssignStartAndEndCells(new EndpointsFactory(), gridWidth, gridHeight);
        gridFactory.AssignPOICells(new EndpointsFactory(), pointsOfInterestCount, gridWidth, gridHeight, morality);
        gridFactory.SolvePaths(new PathSolver(gridFactory, new PathFinder()), gridHeight, gridWidth);

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
                roomProps.poiType = cell.cellProperties.poiType;
                roomProps.POISlot = cell.cellProperties.POISlot;
                roomProps.HasLeftDoor = cell.cellProperties.HasLeftDoor;
                roomProps.HasRightDoor = cell.cellProperties.HasRightDoor;
                roomProps.HasLeftDoorLocked = cell.cellProperties.HasLeftDoorLocked;
                roomProps.HasRightDoorLocked = cell.cellProperties.HasRightDoorLocked;
                roomProps.IsVictoryDoorLeft = cell.cellProperties.IsVictoryDoorLeft;
                roomProps.IsVictoryDoorRight = cell.cellProperties.IsVictoryDoorRight;
                roomProps.HasLiftUp = cell.cellProperties.HasLiftUpBlocked;
                roomProps.HasLiftDown = cell.cellProperties.HasLiftDownBlocked;
                roomProps.GridPosition = cell.cellProperties.GridPosition;
            }
        }
    }
}
