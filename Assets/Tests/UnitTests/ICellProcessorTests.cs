using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;

public class ICellProcessorTests
{
    private class DummyProcessor : ICellProcessor
    {
        public void ProcessCells(Dictionary<Vector2, Cell> cellDataGrid) {}
    }

    private ICellProcessor _processor;

    [SetUp]
    public void SetUp()
    {
        _processor = new DummyProcessor();
    }

    [Test]
    public void ProcessCells_ProcessesData()
    {
        Assert.DoesNotThrow(() => _processor.ProcessCells(new Dictionary<Vector2, Cell>()));
    }
}
