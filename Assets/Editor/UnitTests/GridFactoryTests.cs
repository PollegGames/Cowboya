using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.TestTools;

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
        _factory.CreateGrid(2, 2);
        Assert.AreEqual(4, _factory.cellDataGrid.Count);
    }

    [Test]
    public void AssignStartAndEndCells_SetsCells()
    {
        _factory.CreateGrid(2, 2);
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
        _factory.CreateGrid(3, 3);
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
        _factory.CreateGrid(5, 4);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 5, 4);
        _factory.AssignPOICells(new EndpointsFactory(), count, 5, 4);

        var positions = (List<Vector2>)typeof(GridFactory)
            .GetField("poiPositions", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(_factory);
        var types = positions.Select(position => _factory.cellDataGrid[position].cellProperties.poiType).ToList();

        Assert.AreEqual(count, types.Count);
        if (count >= 1) Assert.AreEqual(POIType.Resting, types[0]);
        if (count >= 2) Assert.AreEqual(POIType.Security, types[1]);
        if (count >= 3) Assert.AreEqual(POIType.Reception, types[2]);
        if (count >= 4) Assert.AreEqual(POIType.Spawning, types[3]);
        if (count >= 5) Assert.AreEqual(POIType.Garage, types[4]);
        if (count >= 6)
            Assert.AreEqual(POIType.Conveyor, types[5]);
        for (int i = 6; i < count; i++)
            Assert.Contains(types[i], new[]
            {
                POIType.Resting, POIType.Security, POIType.Spawning, POIType.Furnace,
                POIType.Junks, POIType.Deads, POIType.CubeCollector
            });
        Assert.LessOrEqual(types.Count(type => type == POIType.Reception), 1);
        Assert.LessOrEqual(types.Count(type => type == POIType.Garage), 1);
        Assert.LessOrEqual(types.Count(type => type == POIType.Conveyor), 1);
    }

    [Test]
    public void POIRoomTypeSelector_ReceptionCanOnlyAppearInThirdSlot()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            Random.InitState(seed);
            Assert.AreEqual(POIType.Reception, POIRoomTypeSelector.Select(3));

            for (int slot = 1; slot <= 20; slot++)
            {
                if (slot != 3)
                    Assert.AreNotEqual(POIType.Reception, POIRoomTypeSelector.Select(slot));
            }
        }
    }

    [Test]
    public void POIRoomTypeSelector_MoralityUsesAsymmetricThresholds()
    {
        foreach (POIType type in new[]
        {
            POIType.Resting, POIType.Security, POIType.Spawning, POIType.Furnace,
            POIType.Junks, POIType.Deads, POIType.CubeCollector
        })
        {
            Assert.AreEqual(1f, POIRoomTypeSelector.GetWeight(type, 0f));
        }

        Assert.AreEqual(POIRoomTypeSelector.GetWeight(POIType.Security, 5f),
            POIRoomTypeSelector.GetWeight(POIType.Security, 100f));
        Assert.AreEqual(POIRoomTypeSelector.GetWeight(POIType.Spawning, -20f),
            POIRoomTypeSelector.GetWeight(POIType.Spawning, -100f));

        Assert.Greater(POIRoomTypeSelector.GetWeight(POIType.Security, 5f), 1f);
        Assert.Greater(POIRoomTypeSelector.GetWeight(POIType.CubeCollector, 5f), 1f);
        Assert.Greater(POIRoomTypeSelector.GetWeight(POIType.Junks, 5f), 1f);
        Assert.Greater(POIRoomTypeSelector.GetWeight(POIType.Spawning, -20f), 1f);
        Assert.Greater(POIRoomTypeSelector.GetWeight(POIType.Furnace, -20f), 1f);
        Assert.Greater(POIRoomTypeSelector.GetWeight(POIType.Deads, -20f), 1f);
        Assert.Less(POIRoomTypeSelector.GetWeight(POIType.CubeCollector, -20f), 1f);
    }

    [Test]
    public void POIRoomTypeSelector_DescribesEveryRandomRoomWeight()
    {
        string description = POIRoomTypeSelector.DescribeWeights(5f);

        foreach (POIType type in new[]
        {
            POIType.Resting, POIType.Security, POIType.Spawning, POIType.Furnace,
            POIType.Junks, POIType.Deads, POIType.CubeCollector
        })
            StringAssert.Contains(type.ToString(), description);
    }

    [Test]
    public void AssignPOICells_ClampsToAvailableWorkCellsWithoutReplacingEndpoints()
    {
        LogAssert.Expect(LogType.Warning,
            "Grid capacity provides only 2 POI cells; all 6 fixed POIs cannot be generated.");
        _factory.CreateGrid(2, 2);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 2, 2);
        _factory.AssignPOICells(new EndpointsFactory(), 20, 2, 2);

        Assert.AreEqual(2, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.POI));
        Assert.AreEqual(1, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.Start));
        Assert.AreEqual(1, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.End));
    }

    [Test]
    public void AssignPOICells_NegativeCountCreatesNoPOIs()
    {
        _factory.CreateGrid(2, 2);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 2, 2);
        _factory.AssignPOICells(new EndpointsFactory(), -1, 2, 2);

        Assert.IsFalse(_factory.cellDataGrid.Values.Any(cell => cell.cellProperties.usageType == UsageType.POI));
    }

    [Test]
    public void AssignStartAndEndCells_RejectsSingleCellGrid()
    {
        _factory.CreateGrid(1, 1);
        Assert.Throws<System.InvalidOperationException>(() =>
            _factory.AssignStartAndEndCells(new EndpointsFactory(), 1, 1));
    }

    [Test]
    public void AssignStartAndEndCells_UsesDistinctEndpointsOnOneByTwoGrid()
    {
        _factory.CreateGrid(1, 2);
        _factory.AssignStartAndEndCells(new EndpointsFactory(), 1, 2);

        Assert.AreEqual(1, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.Start));
        Assert.AreEqual(1, _factory.cellDataGrid.Values.Count(cell => cell.cellProperties.usageType == UsageType.End));
    }

    [Test]
    public void CreateGrid_DoesNotCreateBlockedCells()
    {
        _factory.CreateGrid(3, 3);

        Assert.IsFalse(_factory.cellDataGrid.Values.Any(cell =>
            cell.cellProperties.usageType == UsageType.Blocked));
    }

    [Test]
    public void SolvePaths_CalculatesPaths()
    {
        _factory.CreateGrid(3, 2);
        var start = new Vector2(0f, 0f);
        var end = new Vector2(2f, 0f);
        typeof(GridFactory).GetField("startPosition", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(_factory, start);
        typeof(GridFactory).GetField("endPosition", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(_factory, end);
        _factory.cellDataGrid[start].cellProperties.usageType = UsageType.Start;
        _factory.cellDataGrid[end].cellProperties.usageType = UsageType.End;

        _factory.SolvePaths(new PathSolver(_factory, new PathFinder()), 3, 2);

        // Expect that some cells are marked as path
        bool anyPath = false;
        foreach (var cell in _factory.cellDataGrid.Values)
            if (cell.cellProperties.usageType == UsageType.PathToPOI)
                anyPath = true;

        Assert.IsTrue(anyPath);
    }
}
