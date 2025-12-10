using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class MapManagerTests
{
    private GameObject _gameObject;
    private MapManager _manager;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _manager = _gameObject.AddComponent<MapManager>();
    }

    [Test]
    public void BuildFromConfig_AppliesConfig()
    {
        var cfg = ScriptableObject.CreateInstance<RunMapConfigSO>();
        cfg.gridWidth = 5;
        cfg.gridHeight = 4;
        cfg.blockedCount = 2;
        cfg.poiCount = 1;

        _manager.BuildFromConfig(cfg);

        var widthField = typeof(MapManager).GetField("gridWidth", BindingFlags.NonPublic | BindingFlags.Instance);
        var heightField = typeof(MapManager).GetField("gridHeight", BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.AreEqual(5, widthField.GetValue(_manager));
        Assert.AreEqual(4, heightField.GetValue(_manager));
    }

    [Test]
    public void InitializeGrid_BuildsGrid()
    {
        _manager.Construct(new DummyGridBuilder(), new DummyRoomRenderer(), new DummyRoomProcessor());
        Assert.DoesNotThrow(() => _manager.InitializeGrid());
    }

    [Test]
    public void RegisterFactoryInEachRoom_RegistersFactory()
    {
        var roomGO = new GameObject();
        var roomManager = roomGO.AddComponent<RoomManager>();
        var instances = new System.Collections.Generic.Dictionary<Vector2, GameObject> { { Vector2.zero, roomGO } };
        typeof(MapManager).GetField("roomInstances", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_manager, instances);

        var factory = new GameObject().AddComponent<FactoryManager>();
        _manager.RegisterFactoryInEachRoom(factory, null, null, null,null);

        Assert.AreEqual(factory, roomManager.FactoryManager);
    }

    [Test]
    public void GetStartCellWorldPosition_ReturnsStart()
    {
        _manager.cellWidth = 40;
        typeof(MapManager).GetField("gridWidth", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_manager, 10);

        var startGO = new GameObject();
        startGO.transform.position = new Vector3(1, 1, 0);
        var props = startGO.AddComponent<RoomProperties>();
        props.usageType = UsageType.Start;
        props.GridPosition = new Vector2Int(2, 0); // Left half of grid
        var dict = new System.Collections.Generic.Dictionary<Vector2, GameObject> { { Vector2.zero, startGO } };
        typeof(MapManager).GetField("roomInstances", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_manager, dict);

        var pos = _manager.GetStartCellWorldPosition();
        var expected = startGO.transform.position + new Vector3(_manager.cellWidth * 0.25f, 0f, 0f);
        Assert.AreEqual(expected, pos);
    }

    [Test]
    public void GetRandomPOIPosition_ReturnsPosition()
    {
        var poiGO = new GameObject();
        poiGO.transform.position = new Vector3(2, 2, 0);
        var props = poiGO.AddComponent<RoomProperties>();
        props.usageType = UsageType.POI;
        var dict = new System.Collections.Generic.Dictionary<Vector2, GameObject> { { Vector2.zero, poiGO } };
        typeof(MapManager).GetField("roomInstances", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_manager, dict);

        var pos = _manager.GetRandomPOIPosition();
        Assert.AreEqual(poiGO.transform.position, pos);
    }

    [Test]
    public void GetRandomWorkPosition_ReturnsPosition()
    {
        var workGO = new GameObject();
        workGO.transform.position = new Vector3(3, 0, 0);
        var props = workGO.AddComponent<RoomProperties>();
        props.usageType = UsageType.Work;
        var dict = new System.Collections.Generic.Dictionary<Vector2, GameObject> { { Vector2.zero, workGO } };
        typeof(MapManager).GetField("roomInstances", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_manager, dict);

        var pos = _manager.GetRandomWorkPosition();
        Assert.AreEqual(workGO.transform.position, pos);
    }

    private class DummyGridBuilder : IGridBuilder
    {
        public System.Collections.Generic.Dictionary<Vector2, Cell> BuildGrid(int width, int height, int wallCount, int poiCount)
            => new System.Collections.Generic.Dictionary<Vector2, Cell>();
    }
    private class DummyRoomRenderer : IRoomRenderer
    {
        public System.Collections.Generic.Dictionary<Vector2, GameObject> RenderRooms(System.Collections.Generic.Dictionary<Vector2, Cell> cellDataGrid, System.Collections.Generic.Dictionary<UsageType, GameObject> usageMapping, System.Collections.Generic.Dictionary<POIType, GameObject> poiMapping, Vector2 cellSize, Vector3 offset, Transform parent, GameObject defaultPrefab)
            => new System.Collections.Generic.Dictionary<Vector2, GameObject>();
    }
    private class DummyRoomProcessor : IRoomProcessor
    {
        public void ProcessRooms(System.Collections.Generic.Dictionary<Vector2, Cell> cellDataGrid, int width, int height, bool noBlockRequiredWhenZeroEnemies) { }
    }
}
