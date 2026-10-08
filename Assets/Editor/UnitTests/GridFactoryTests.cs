using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;

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

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(12)]
    public void AssignPOICells_FollowsSlotRules(int count)
    {
        Random.InitState(12345);
        _factory.CreateGrid(5, 4, 0);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 5, 4);
        _factory.AssignPOICells(new EndpointsFactory(), count, 5, 4);

        var positions = (List<Vector2>)typeof(GridFactory)
            .GetField("poiPositions", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(_factory);
        var types = positions.Select(position => _factory.cellDataGrid[position].cellProperties.poiType).ToList();

        Assert.AreEqual(count, types.Count);
        if (count >= 1) Assert.AreEqual(POIType.Reception, types[0]);
        if (count >= 2) Assert.AreEqual(POIType.Security, types[1]);
        for (int i = 2; i < Mathf.Min(5, count); i++)
            Assert.Contains(types[i], new[] { POIType.Reception, POIType.Security });
        if (count >= 6)
            Assert.IsFalse(new[] { POIType.Reception, POIType.Security }.Contains(types[5]));

        var expandedTypes = types.Skip(5).ToList();
        Assert.AreEqual(expandedTypes.Count, expandedTypes.Distinct().Count(),
            "Expanded slots must use every unused type before duplicating one.");
    }

    [Test]
    public void AssignPOICells_ClampsToAvailableWorkCellsWithoutReplacingEndpoints()
    {
        _factory.CreateGrid(2, 2, 0);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 2, 2);
        _factory.AssignPOICells(new EndpointsFactory(), 20, 2, 2);

        Assert.AreEqual(2, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.POI));
        Assert.AreEqual(1, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.Start));
        Assert.AreEqual(1, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.End));
    }

    [Test]
    public void AssignPOICells_NegativeCountCreatesNoPOIs()
    {
        _factory.CreateGrid(2, 2, 0);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 2, 2);
        _factory.AssignPOICells(new EndpointsFactory(), -1, 2, 2);

        Assert.IsFalse(_factory.cellDataGrid.Values.Any(cell => cell.cellProperties.usageType == UsageType.POI));
    }

    [Test]
    public void AssignStartAndEndCells_RejectsSingleCellGrid()
    {
        _factory.CreateGrid(1, 1, 0);
        Assert.Throws<System.InvalidOperationException>(() =>
            _factory.AssignStartAndEndCells(new EndpointsFactory(), 1, 1));
    }

    [Test]
    public void AssignStartAndEndCells_UsesDistinctEndpointsOnOneByTwoGrid()
    {
        _factory.CreateGrid(1, 2, 0);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 1, 2);

        Assert.AreEqual(1, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.Start));
        Assert.AreEqual(1, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.End));
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
        _factory.CreateGrid(3, 2, 0);
        var start = new Vector2(0f, 0f);
        var end = new Vector2(2f, 0f);
        typeof(GridFactory).GetField("startPosition", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(_factory, start);
        typeof(GridFactory).GetField("endPosition", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(_factory, end);
        _factory.cellDataGrid[start].cellProperties.usageType = UsageType.Start;
        _factory.cellDataGrid[end].cellProperties.usageType = UsageType.End;

        _factory.SolvePaths(0, 0, new PathSolver(_factory, new PathFinder()), 3, 2);

        // Expect that some cells are marked as path
        bool anyPath = false;
        foreach (var cell in _factory.cellDataGrid.Values)
            if (cell.cellProperties.usageType == UsageType.PathToPOI)
                anyPath = true;

        Assert.IsTrue(anyPath);
    }
}
