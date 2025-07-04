using System.Collections.Generic;
using UnityEngine;

public class PathSolver
{
    private readonly GridFactory gridFactory;
    private readonly PathFinder pathFinder;

    public PathSolver(GridFactory factory, PathFinder finder)
    {
        gridFactory = factory;
        pathFinder = finder;
    }

    public bool TryFindPath(
         Vector2 start, 
         Vector2 target, 
         out List<Vector2> path)
    {
        path = new List<Vector2>();
        const int maxAttempts = 30;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var cells = gridFactory.cellDataGrid;

            var tempPath = new List<Cell>();
            if (pathFinder.FindPath(cells, start, target, tempPath))
            {
                // Add the path cells as PathToPOI (green cells)
                foreach (var c in tempPath)
                {
                    path.Add(c.position);
                }
                return true;
            }
        }
        return false;
    }
}
