using System.Collections.Generic;
using UnityEngine;

public class GridBuilder : MonoBehaviour, IGridBuilder
{
    public Dictionary<Vector2, Cell> BuildGrid(int width, int height, int poiCount, float morality)
    {
        var gridFactory = new GridFactory();
        gridFactory.CreateGrid(width, height);
        gridFactory.AssignStartAndEndCells(new EndpointsFactory(), width, height);
        gridFactory.AssignPOICells(new EndpointsFactory(), poiCount, width, height, morality);
        gridFactory.SolvePaths(new PathSolver(gridFactory, new PathFinder()), width, height);
        return gridFactory.cellDataGrid;
    }
}
