using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class GridManagerTests
{
    private GridManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = new GridManager(1,1,1,1,0,0);
    }

    private class DummyProcessor : ICellProcessor
    {
        public bool Called;
        public void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid)
        {
            Called = true;
        }
    }

    [Test]
    public void CreateAndInitializeGrid_ReturnsGrid()
    {
        var grid = _manager.CreateAndInitializeGrid();

        Assert.IsNotNull(grid);
        Assert.Greater(grid.Count, 0);
    }

    [Test]
    public void GetRandomPointOnGrid_ReturnsPoint()
    {
        var point = _manager.GetRandomPointOnGrid();
        Assert.AreEqual(Vector3.zero, point);
    }

    [Test]
    public void ProcessRooms_ProcessesProcessors()
    {
        var grid = new Dictionary<Vector2, Cell> { { Vector2.zero, new Cell(Vector2.zero) } };
        var processor = new DummyProcessor();
        _manager.ProcessRooms(new List<ICellProcessor> { processor }, grid);

        Assert.IsTrue(processor.Called);
    }

    [Test]
    public void AssignRoomProperties_AssignsProperties()
    {
        var cell = new Cell(Vector2.zero, UsageType.Blocked);
        cell.cellProperties.GridPosition = Vector2Int.zero;
        var grid = new Dictionary<Vector2, Cell> { { Vector2.zero, cell } };

        var roomGO = new GameObject();
        var instances = new Dictionary<Vector2, GameObject> { { Vector2.zero, roomGO } };

        _manager.AssignRoomProperties(instances, grid);

        var props = roomGO.GetComponent<RoomProperties>();
        Assert.IsNotNull(props);
        Assert.AreEqual(UsageType.Blocked, props.usageType);
        Assert.AreEqual(Vector2Int.zero, props.GridPosition);
    }
}
