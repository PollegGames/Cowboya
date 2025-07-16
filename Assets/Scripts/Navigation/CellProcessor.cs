using System.Collections.Generic;
using UnityEngine;

public abstract class CellProcessor : ICellProcessor
{
    protected readonly int width;
    protected readonly int height;

    protected CellProcessor(int gridWidth, int gridHeight)
    {
        width = gridWidth;
        height = gridHeight;
    }

    public abstract void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid);

    protected void BlockDoor(Cell cell, DoorDirection dir)
    {
        switch (dir)
        {
            case DoorDirection.Left:
                cell.cellProperties.HasLeftDoor = false;
                break;
            case DoorDirection.Right:
                cell.cellProperties.HasRightDoor = false;
                break;
            default:
                Debug.LogError($"Invalid door direction: {dir} for cell {cell.position}");
                break;
        }
    }

    protected void LockDoor(Cell cell, DoorDirection dir)
    {
        switch (dir)
        {
            case DoorDirection.Left:
                cell.cellProperties.HasLeftDoor = true;
                cell.cellProperties.HasLeftDoorLocked = true;
                break;
            case DoorDirection.Right:
                cell.cellProperties.HasRightDoor = true;
                cell.cellProperties.HasRightDoorLocked = true;
                break;
            default:
                Debug.LogError($"Invalid door direction: {dir} for cell {cell.position}");
                break;
        }
    }

    protected void BlockLift(Cell cell, LiftDirection dir)
    {
        switch (dir)
        {
            case LiftDirection.Up:
                cell.cellProperties.HasLiftUpBlocked = false;
                break;
            case LiftDirection.Down:
                cell.cellProperties.HasLiftDownBlocked = false;
                break;
            default:
                break;
        }
    }
}