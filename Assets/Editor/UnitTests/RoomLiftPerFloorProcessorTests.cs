using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections.Generic;

public class RoomLiftPerFloorProcessorTests
{
    [Test]
    public void ProcessCells_SetsPathOnHighestPriorityCandidate()
    {
        var grid = new Dictionary<Vector2, Cell>();

        var existingPath = new Cell(new Vector2(0, 0), UsageType.PathToPOI);
        grid[existingPath.position] = existingPath;

        var blocked = new Cell(new Vector2(0, 1), UsageType.Blocked);
        var poi = new Cell(new Vector2(1, 1), UsageType.POI);
        var empty = new Cell(new Vector2(2, 1), UsageType.Empty);
        grid[blocked.position] = blocked;
        grid[poi.position] = poi;
        grid[empty.position] = empty;

        var poi2 = new Cell(new Vector2(0, 2), UsageType.POI);
        var empty2 = new Cell(new Vector2(1, 2), UsageType.Empty);
        grid[poi2.position] = poi2;
        grid[empty2.position] = empty2;

        var empty3 = new Cell(new Vector2(0, 3), UsageType.Empty);
        grid[empty3.position] = empty3;

        var processor = new RoomLiftPerFloorProcessor(3, 4);
        processor.ProcessCells(grid);

        Assert.AreEqual(UsageType.PathToPOI, blocked.cellProperties.usageType);
        Assert.AreEqual(UsageType.POI, poi.cellProperties.usageType);
        Assert.AreEqual(UsageType.Empty, empty.cellProperties.usageType);

        Assert.AreEqual(UsageType.PathToPOI, poi2.cellProperties.usageType);
        Assert.AreEqual(UsageType.Empty, empty2.cellProperties.usageType);

        Assert.AreEqual(UsageType.PathToPOI, empty3.cellProperties.usageType);
        Assert.AreEqual(UsageType.PathToPOI, existingPath.cellProperties.usageType);
    }

    [Test]
    public void ProcessCells_NoCandidateOnFloor_LogsError()
    {
        var grid = new Dictionary<Vector2, Cell>();
        var start = new Cell(new Vector2(0, 0), UsageType.Start);
        grid[start.position] = start;

        var processor = new RoomLiftPerFloorProcessor(1, 1);

        LogAssert.Expect(LogType.Error, "No suitable cell found on level 0 to set PathToPOI");

        processor.ProcessCells(grid);

        Assert.AreEqual(UsageType.Start, start.cellProperties.usageType);
    }
}
