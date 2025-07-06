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

    [Test]
    public void CreateAndInitializeGrid_ReturnsGrid()
    {
        // TODO: Assert grid creation
    }

    [Test]
    public void GetRandomPointOnGrid_ReturnsPoint()
    {
        // TODO: Assert random point
    }

    [Test]
    public void ProcessRooms_ProcessesProcessors()
    {
        // TODO: Assert processing
    }

    [Test]
    public void AssignRoomProperties_AssignsProperties()
    {
        // TODO: Assert property assignment
    }
}
