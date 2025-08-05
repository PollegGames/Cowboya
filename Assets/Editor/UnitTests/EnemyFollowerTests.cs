using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

public class EnemyFollowerTests
{
    private class FakeMemory : MonoBehaviour, IRobotMemory
    {
        public Vector3 LastKnownPlayerPosition => new Vector3(1f, 0f, 0f);
        public bool WasRecentlyAttacked => false;
        public RoomWaypoint LastVisitedPoint => null;
        public void SetRespawnService(IRobotRespawnService service) { }
        public void SetLastVisitedPoint(RoomWaypoint point) { }
        public void OnStuck(EnemyWorkerController controller) { }
        public void OnBossStuck(EnemyController controller) { }
        public void RememberPlayerPosition(Vector3 playerPosition) { }
        public void ClearPlayerPosition() { }
        public void RegisterAttack() { }
        public void ResetAttackMemory() { }
    }

    private class MockWaypointService : IWaypointService
    {
        public bool SetDestinationCalled { get; private set; }
        public List<IRobotNavigationListener> listeners = new();
        public List<RoomWaypoint> active = new();
        public List<RoomWaypoint> path = new();
        private RoomWaypoint closest;

        public RoomWaypoint ClosestWaypointToPlayer
        {
            get => closest;
            set => closest = value;
        }

        // Extra method to detect unintended calls
        public void SetDestination(RoomWaypoint target) => SetDestinationCalled = true;

        public void Subscribe(IRobotNavigationListener robot) => listeners.Add(robot);
        public void Unsubscribe(IRobotNavigationListener robot) => listeners.Remove(robot);
        public void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable) { }
        public List<RoomWaypoint> GetAllWaypoints() => active;
        public List<RoomWaypoint> GetActiveWaypoints() => active;
        public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end) => path;
        public RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false) => active.Count > 0 ? active[0] : null;
        public RoomWaypoint GetEndPoint() => null;
        public RoomWaypoint GetStartPoint() => null;
        public void UpdateClosestWaypointToPlayer(Vector2 playerPosition) { }
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
    }

    [Test]
    public void UpdateState_MovesTowardPlayer_NoWaypointServiceSetDestination()
    {
        var enemyGO = new GameObject("enemy");
        var enemy = enemyGO.AddComponent<EnemyController>();
        var stateMachine = enemyGO.AddComponent<EnemyStateMachine>();

        var memory = enemyGO.AddComponent<FakeMemory>();
        var memProp = typeof(EnemyController).GetProperty("memory", BindingFlags.Public | BindingFlags.Instance);
        memProp.GetSetMethod(true).Invoke(enemy, new object[] { memory });

        var bodyField = typeof(EnemyController).GetField("bodyReference", BindingFlags.NonPublic | BindingFlags.Instance);
        bodyField.SetValue(enemy, enemy.transform);

        var service = new MockWaypointService();

        var startWp = new GameObject("start").AddComponent<RoomWaypoint>();
        startWp.transform.position = Vector3.zero;
        var targetWp = new GameObject("target").AddComponent<RoomWaypoint>();
        targetWp.transform.position = new Vector3(10f, 0f, 0f);

        service.active.Add(startWp);
        service.active.Add(targetWp);
        service.path = new List<RoomWaypoint> { startWp, targetWp };
        service.ClosestWaypointToPlayer = targetWp;

        enemy.Initialize(service, service, null, null, null);

        var alarmStatus = ScriptableObject.CreateInstance<FactoryAlarmStatus>();
        var follower = new Enemy_Follower(enemy, stateMachine, service, alarmStatus);

        follower.UpdateState();

        var pfField = typeof(EnemyController).GetField("pathFollower", BindingFlags.NonPublic | BindingFlags.Instance);
        var pf = pfField.GetValue(enemy);
        var cwField = typeof(WaypointPathFollower).GetField("currentWaypoints", BindingFlags.NonPublic | BindingFlags.Instance);
        var currentWaypoints = cwField.GetValue(pf) as List<RoomWaypoint>;
        Assert.IsNotNull(currentWaypoints);
        Assert.AreEqual(targetWp, currentWaypoints[^1]);

        Assert.IsFalse(service.SetDestinationCalled);
    }
}

