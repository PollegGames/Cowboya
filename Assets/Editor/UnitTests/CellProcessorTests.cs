using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class CellProcessorTests
{
    private class DummyProcessor : CellProcessor
    {
        public DummyProcessor() : base(1,1) {}
        public override void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid) {}
    }

    private DummyProcessor _processor;

    [SetUp]
    public void SetUp()
    {
        _processor = new DummyProcessor();
    }

    [Test]
    public void ProcessCells_Implemented()
    {
        Assert.DoesNotThrow(() => _processor.ProcessCells(new Dictionary<Vector2, Cell>()));
    }

    [TestCase(0, true, false)]
    [TestCase(2, false, true)]
    public void LockEndRoomDoorProcessor_MarksOnlyLockedEndRoomEdgeAsVictory(
        int endX,
        bool expectLeftVictory,
        bool expectRightVictory)
    {
        var end = new Cell(new Vector2(endX, 0), UsageType.End);
        end.cellProperties.GridPosition = new Vector2Int(endX, 0);
        int otherX = endX == 0 ? 2 : 0;
        var otherEdge = new Cell(new Vector2(otherX, 0), UsageType.Work);
        otherEdge.cellProperties.GridPosition = new Vector2Int(otherX, 0);
        var cells = new Dictionary<Vector2, Cell>
        {
            { end.position, end },
            { otherEdge.position, otherEdge }
        };

        new LockEndRoomDoorProcessor(3, 1, false).ProcessCells(cells);

        Assert.AreEqual(expectLeftVictory, end.cellProperties.IsVictoryDoorLeft);
        Assert.AreEqual(expectRightVictory, end.cellProperties.IsVictoryDoorRight);
        Assert.IsTrue(expectLeftVictory
            ? end.cellProperties.HasLeftDoorLocked
            : end.cellProperties.HasRightDoorLocked);
        Assert.IsFalse(otherEdge.cellProperties.IsVictoryDoorLeft);
        Assert.IsFalse(otherEdge.cellProperties.IsVictoryDoorRight);
    }
}
