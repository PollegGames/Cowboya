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
}
