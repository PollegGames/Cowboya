using NUnit.Framework;
using UnityEngine;

public class CellTests
{
    private Cell _cell;

    [SetUp]
    public void SetUp()
    {
        _cell = new Cell(Vector2.zero);
    }

    [Test]
    public void Constructor_InitializesValues()
    {
        Assert.AreEqual(Vector2.zero, _cell.position);
        Assert.IsNotNull(_cell.cellProperties);
        Assert.AreEqual(int.MaxValue, _cell.gCost);
    }
}
