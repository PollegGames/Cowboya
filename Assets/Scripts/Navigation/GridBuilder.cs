using System.Collections.Generic;
using UnityEngine;

public class GridBuilder : MonoBehaviour, IGridBuilder
{
    public Dictionary<Vector2, Cell> BuildGrid(int width, int height, int wallCount, int poiCount)
    {
        var gridFactory = new GridFactory();
        gridFactory.CreateGrid(width, height, 0);
        gridFactory.AssignStartAndEndCells(new EndpointsFactory(), width, height);
        gridFactory.AssignPOICells(new EndpointsFactory(), poiCount, width, height);
        gridFactory.SolvePaths(poiCount, wallCount, new PathSolver(gridFactory, new PathFinder()), width, height);
        gridFactory.AssignBlockedCells(wallCount);
        return gridFactory.cellDataGrid;
    }
}
