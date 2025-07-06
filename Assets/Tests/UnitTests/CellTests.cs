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
        // TODO: Assert cell initialization
    }
}
