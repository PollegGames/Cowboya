using NUnit.Framework;
using UnityEngine;
using System.Reflection;

public class FactoryManagerTests
{
    private GameObject _gameObject;
    private FactoryManager _factoryManager;

    [SetUp]
    public void SetUp()
    {
        _gameObject = new GameObject();
        _factoryManager = _gameObject.AddComponent<FactoryManager>();
    }

    [Test]
    public void Initialize_SetsUpFactory()
    {
        var mapGO = new GameObject();
        var mapManager = mapGO.AddComponent<MapManager>();
        mapManager.Construct(new DummyGridBuilder(), new DummyRoomRenderer(), new DummyRoomProcessor());
        var waypointService = mapGO.AddComponent<WaypointService>();
        var vs = ScriptableObject.CreateInstance<VictorySetup>();
        var spawner = new DummyEnemiesSpawner();

        Assert.DoesNotThrow(() => _factoryManager.Initialize(mapManager, waypointService, vs, spawner));
        Assert.AreEqual(waypointService, _factoryManager.GetWayPointService());
    }

    [Test]
    public void GetStartCellWorldPosition_ReturnsPosition()
    {
        var mapManager = new GameObject().AddComponent<MapManager>();
        var startGO = new GameObject();
        startGO.transform.position = new Vector3(2, 3, 0);
        var props = startGO.AddComponent<RoomProperties>();
        props.usageType = UsageType.Start;
        var dict = new System.Collections.Generic.Dictionary<Vector2, GameObject> { { Vector2.zero, startGO } };
        typeof(MapManager).GetField("roomInstances", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mapManager, dict);
        typeof(FactoryManager).GetField("mapManager", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_factoryManager, mapManager);

        var pos = _factoryManager.GetStartCellWorldPosition();
        Assert.AreEqual(startGO.transform.position, pos);
    }

    [Test]
    public void SetPlayerInstanceHead_AssignsHead()
    {
        var player = new GameObject();
        var head = new GameObject().transform;
        _factoryManager.SetPlayerInstanceHead(player, head);

        Assert.AreEqual(player, _factoryManager.playerInstance);
        Assert.AreEqual(head, _factoryManager.playerHeadTransform);
    }

    [Test]
    public void OnRobotSaved_RaisesEvent()
    {
        var vs = ScriptableObject.CreateInstance<VictorySetup>();
        typeof(FactoryManager).GetField("victorySetup", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_factoryManager, vs);

        _factoryManager.OnRobotSaved();
        Assert.AreEqual(1, vs.currentSaved);
    }

    [Test]
    public void OnRobotKilled_RaisesEvent()
    {
        var vs = ScriptableObject.CreateInstance<VictorySetup>();
        typeof(FactoryManager).GetField("victorySetup", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_factoryManager, vs);

        _factoryManager.OnRobotKilled();
        Assert.AreEqual(1, vs.currentKilled);
    }

    private class DummyGridBuilder : IGridBuilder
    {
        public System.Collections.Generic.Dictionary<Vector2, Cell> BuildGrid(int width, int height, int wallCount, int poiCount)
        {
            return new System.Collections.Generic.Dictionary<Vector2, Cell>();
        }
    }
    private class DummyRoomRenderer : IRoomRenderer
    {
        public System.Collections.Generic.Dictionary<Vector2, GameObject> RenderRooms(System.Collections.Generic.Dictionary<Vector2, Cell> cellDataGrid, System.Collections.Generic.Dictionary<UsageType, GameObject> usageMapping, System.Collections.Generic.Dictionary<POIType, GameObject> poiMapping, Vector2 cellSize, Vector3 offset, Transform parent, GameObject defaultPrefab)
        {
            return new System.Collections.Generic.Dictionary<Vector2, GameObject>();
        }
    }
    private class DummyRoomProcessor : IRoomProcessor
    {
        public void ProcessRooms(System.Collections.Generic.Dictionary<Vector2, Cell> cellDataGrid, int width, int height) { }
    }
    private class DummyEnemiesSpawner : IEnemiesSpawner
    {
        public void Initialize(MapManager mapManager, IWaypointService waypointService, GameUIViewModel viewModel, IRobotRespawnService respawnService, MachineSecurityManager securityManager) { }
        public void SetDropContainer(Transform container) { }
        public void CreateWorkers(int workersToSpawn) { }
        public void CreateEnemies(int enemiesToSpawn) { }
        public void CreateBoss() { }
        public GameObject CreateAngGetFollowerGuard() => null;
        public void CreateWorkersSpawner(int workersToSpawn) { }
        public void SpreadEnemies() { }
        public void SpawnEnemyAtRandom() { }
        public void SpawnBossAtRandom() { }
    }
}
