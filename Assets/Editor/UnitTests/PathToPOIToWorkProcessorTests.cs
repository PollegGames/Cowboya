using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class PathToPOIToWorkProcessorTests
{
    [Test]
    public void ProcessCells_IsolatedPathBecomesWork()
    {
        var grid = new Dictionary<Vector2, Cell>();

        var isolated = new Cell(new Vector2(0, 1), UsageType.PathToPOI);
        grid[isolated.position] = isolated;

        var above = new Cell(new Vector2(0, 2), UsageType.Work);
        grid[above.position] = above;

        var below = new Cell(new Vector2(0, 0), UsageType.Work);
        grid[below.position] = below;

        var processor = new PathToPOIToWorkProcessor(1, 3);
        processor.ProcessCells(grid);

        Assert.AreEqual(UsageType.Work, isolated.cellProperties.usageType);
    }

    [Test]
    public void ProcessCells_ConnectedPathRemainsPath()
    {
        var grid = new Dictionary<Vector2, Cell>();

        var path = new Cell(new Vector2(1, 1), UsageType.PathToPOI);
        var neighbor = new Cell(new Vector2(1, 2), UsageType.PathToPOI);
        grid[path.position] = path;
        grid[neighbor.position] = neighbor;

        var processor = new PathToPOIToWorkProcessor(2, 3);
        processor.ProcessCells(grid);

        Assert.AreEqual(UsageType.PathToPOI, path.cellProperties.usageType);
        Assert.AreEqual(UsageType.PathToPOI, neighbor.cellProperties.usageType);
    }
}
