using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;
using System;

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
        mapManager.cellWidth = 40;
        typeof(MapManager).GetField("gridWidth", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mapManager, 10);

        var startGO = new GameObject();
        startGO.transform.position = new Vector3(2, 3, 0);
        var props = startGO.AddComponent<RoomProperties>();
        props.usageType = UsageType.Start;
        props.GridPosition = new Vector2Int(8, 0); // Right half of grid
        var dict = new System.Collections.Generic.Dictionary<Vector2, GameObject> { { Vector2.zero, startGO } };
        typeof(MapManager).GetField("roomInstances", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(mapManager, dict);
        typeof(FactoryManager).GetField("mapManager", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_factoryManager, mapManager);

        var pos = _factoryManager.GetStartCellWorldPosition();
        var expected = startGO.transform.position + new Vector3(-mapManager.cellWidth * 0.25f, 0f, 0f);
        Assert.AreEqual(expected, pos);
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
        var stats = new RobotStats();
        typeof(FactoryManager).GetField("playerStats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_factoryManager, stats);

        _factoryManager.OnRobotSaved();
        Assert.AreEqual(1, vs.currentSaved);
        Assert.AreEqual(1f, stats.Morality);
    }

    [Test]
    public void OnRobotKilled_RaisesEvent()
    {
        var vs = ScriptableObject.CreateInstance<VictorySetup>();
        typeof(FactoryManager).GetField("victorySetup", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_factoryManager, vs);
        var stats = new RobotStats();
        typeof(FactoryManager).GetField("playerStats", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_factoryManager, stats);

        _factoryManager.OnRobotKilled();
        Assert.AreEqual(1, vs.currentKilled);
        Assert.AreEqual(-1f, stats.Morality);
    }

    [Test]
    public void RoomMachineEvents_EmitFactorySummary_AndAllOffIsLatched()
    {
        var roomGo = new GameObject("Room_Test");
        var room = roomGo.AddComponent<RoomManager>();
        var machineA = new GameObject("FactoryMachine_A").AddComponent<FactoryMachine>();
        var machineB = new GameObject("FactoryMachine_B").AddComponent<FactoryMachine>();
        room.factorymMachinesInRoom = new List<FactoryMachine> { machineA, machineB };
        room.Initialize(null, null, null, null, null);

        FactoryMachinesSummaryChangedEvent? lastSummary = null;
        int allOffCount = 0;
        _factoryManager.OnFactoryMachinesSummaryChanged += evt => lastSummary = evt;
        _factoryManager.OnFactoryAllMachinesOff += () => allOffCount++;

        InvokePrivate("RegisterRooms", new List<RoomManager> { room });

        machineA.PowerOff();
        Assert.IsTrue(lastSummary.HasValue);
        Assert.AreEqual(1, lastSummary.Value.TotalOn);
        Assert.AreEqual(1, lastSummary.Value.TotalOff);
        Assert.AreEqual(2, lastSummary.Value.TotalRegistered);
        Assert.AreEqual(1, lastSummary.Value.OnByType[MachineType.WorkStation]);

        machineB.PowerOff();
        Assert.AreEqual(1, allOffCount);
        Assert.IsTrue(lastSummary.HasValue);
        Assert.AreEqual(0, lastSummary.Value.TotalOn);
        Assert.AreEqual(2, lastSummary.Value.TotalOff);

        machineA.PowerOn();
        machineA.PowerOff();
        Assert.AreEqual(2, allOffCount);
    }

    [Test]
    public void AlarmStatusChange_RaisesOnFactoryAlarmChanged_WithoutPolling()
    {
        var alarmStatus = ScriptableObject.CreateInstance<FactoryAlarmStatus>();
        alarmStatus.CurrentAlarmState = AlarmState.Normal;
        typeof(FactoryManager).GetField("factoryAlarmStatus", BindingFlags.Public | BindingFlags.Instance)
            .SetValue(_factoryManager, alarmStatus);

        AlarmState? observed = null;
        _factoryManager.OnFactoryAlarmChanged += state => observed = state;

        InvokePrivate("SetupFactoryState");
        alarmStatus.CurrentAlarmState = AlarmState.Wanted;

        Assert.IsTrue(observed.HasValue);
        Assert.AreEqual(AlarmState.Wanted, observed.Value);
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
        public void ProcessRooms(System.Collections.Generic.Dictionary<Vector2, Cell> cellDataGrid, int width, int height, bool noBlockRequiredWhenZeroEnemies) { }
    }
    private class DummyEnemiesSpawner : IEnemiesSpawner
    {
        public void Initialize(MapManager mapManager, IWaypointService waypointService, GameUIViewModel viewModel, IRobotRespawnService respawnService, IFactoryManager factoryManager, MachineSecurityManager securityManager, SecurityBadgeSpawner securityBadgeSpawner, BatterySpawner batterySpawner) { }
        public void SetDropContainer(Transform container) { }
        public void CreateWorkers(int workersToSpawn) { }
        public void CreateSecurityGuards(int enemiesToSpawn) { }
        public void CreateBoss() { }
        public void CreateAndSpawnFollowerGuard(RoomWaypoint spawnPos, FactoryAlarmStatus factoryAlarmStatus) {}
        public void CreateAndSpawnSecurityGuard(RoomWaypoint spawnPos, SecurityMachine machine) {}
        public void CreateWorkerSpawners(int workersToSpawn) { }
        public void SpreadEnemies() { }
        public void SpawnEnemyAtRandom() { }
        public void SpawnBossAtRandom() { }
    }

    private void InvokePrivate(string methodName, params object[] args)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var method = typeof(FactoryManager).GetMethod(methodName, flags);
        Assert.IsNotNull(method, $"Missing private method {methodName}");
        method.Invoke(_factoryManager, args);
    }
}
