using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

public class SecurityCameraTests
{
    private class DummyWaypointService : IWaypointService
    {
        public List<RoomWaypoint> GetAllWaypoints() => new List<RoomWaypoint>();
        public List<RoomWaypoint> GetActiveWaypoints() => new List<RoomWaypoint>();
        public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end) => new List<RoomWaypoint>();
        public RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false) => null;
        public RoomWaypoint GetEndPoint() => null;
        public RoomWaypoint GetStartPoint() => null;
        public void UpdateClosestWaypointToPlayer(Vector2 playerPosition) { }
        public RoomWaypoint ClosestWaypointToPlayer => null;
        public void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints) { }
        public void UnregisterRoomWaypoints(RoomManager room) { }
        public void BuildAllNeighbors(bool includeUnavailable = false) { }
        public RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetFirstFreeSecurityPoint() => null;
        public RoomWaypoint GetSecurityOrRestPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetBlockedRoomSecuritySpawning(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetBlockedRoomCenter(RoomWaypoint exclude = null) => null;
        public void ReleasePOI(RoomWaypoint poi) { }
        public FactoryMachine ReserveFreeMachine(RoomManager room, EnemyWorkerController worker) => null;
        public void ReleaseMachine(FactoryMachine machine) { }
        public bool IsMachineReserved(FactoryMachine machine) => false;
        public void Subscribe(IRobotNavigationListener robot) { }
        public void Unsubscribe(IRobotNavigationListener robot) { }
        public void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable) { }
    }

    [Test]
    public void PlayerWithHighMoralityTriggersWantedState()
    {
        var cameraGO = new GameObject();
        var camera = cameraGO.AddComponent<SecurityCamera>();

        var roomGO = new GameObject();
        var roomManager = roomGO.AddComponent<RoomManager>();
        camera.roomManager = roomManager;

        var factoryGO = new GameObject();
        var factoryManager = factoryGO.AddComponent<FactoryManager>();
        typeof(RoomManager).GetProperty("FactoryManager", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(roomManager, factoryManager);

        var alarmStatus = ScriptableObject.CreateInstance<FactoryAlarmStatus>();
        factoryManager.factoryAlarmStatus = alarmStatus;

        roomManager.waypointService = new DummyWaypointService();

        var playerGO = new GameObject();
        var headGO = new GameObject();
        headGO.transform.position = new Vector3(5f, 0f, 0f);

        var controller = playerGO.AddComponent<RobotStateController>();
        controller.Stats = new RobotStats();
        controller.Stats.Morality = 10f;

        factoryManager.SetPlayerInstanceHead(playerGO, headGO.transform);

        var collider = playerGO.AddComponent<BoxCollider2D>();

        typeof(SecurityCamera).GetMethod("OnPlayerEnterZone", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(camera, new object[] { collider });

        Assert.AreEqual(AlarmState.Wanted, alarmStatus.CurrentAlarmState);
        Assert.AreEqual(headGO.transform.position, alarmStatus.LastPlayerPosition);
    }
}

