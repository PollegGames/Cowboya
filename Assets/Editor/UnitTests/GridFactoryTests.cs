using NUnit.Framework;
using UnityEngine;

public class GridFactoryTests
{
    private GridFactory _factory;

    [SetUp]
    public void SetUp()
    {
        _factory = new GridFactory();
    }

    [Test]
    public void CreateGrid_CreatesCells()
    {
        _factory.CreateGrid(2, 2, 0);
        Assert.AreEqual(4, _factory.cellDataGrid.Count);
    }

    [Test]
    public void AssignStartAndEndCells_SetsCells()
    {
        _factory.CreateGrid(2, 2, 0);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 2, 2);

        int start = 0, end = 0;
        foreach (var cell in _factory.cellDataGrid.Values)
        {
            if (cell.cellProperties.usageType == UsageType.Start) start++;
            if (cell.cellProperties.usageType == UsageType.End) end++;
        }

        Assert.AreEqual(1, start);
        Assert.AreEqual(1, end);
    }

    [Test]
    public void AssignPOICells_AssignsPOIs()
    {
        _factory.CreateGrid(3, 3, 0);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 3, 3);
        _factory.AssignPOICells(new EndpointsFactory(), 1, 3, 3);

        int poi = 0;
        foreach (var cell in _factory.cellDataGrid.Values)
            if (cell.cellProperties.usageType == UsageType.POI) poi++;

        Assert.AreEqual(1, poi);
    }

    [Test]
    public void AssignBlockedCells_MarksCells()
    {
        _factory.CreateGrid(3, 3, 0);
        _factory.AssignBlockedCells(2);

        int blocked = 0;
        foreach (var cell in _factory.cellDataGrid.Values)
            if (cell.cellProperties.usageType == UsageType.Blocked) blocked++;

        Assert.AreEqual(2, blocked);
    }

    [Test]
    public void SolvePaths_CalculatesPaths()
    {
        _factory.CreateGrid(2, 2, 0);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 2, 2);
        _factory.SolvePaths(0, 0, new PathSolver(_factory, new PathFinder()), 2, 2);

        // Expect that some cells are marked as path
        bool anyPath = false;
        foreach (var cell in _factory.cellDataGrid.Values)
            if (cell.cellProperties.usageType == UsageType.PathToPOI)
                anyPath = true;

        Assert.IsTrue(anyPath);
    }
}
