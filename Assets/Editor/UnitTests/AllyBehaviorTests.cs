using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.TestTools;
using System.Collections;

public class AllyBehaviorTests
{
    private class MockWaypointService : IWaypointService
    {
        public List<IRobotNavigationListener> listeners = new();
        public bool FindWorldPathCalled { get; private set; }
        public List<RoomWaypoint> path = new();
        public List<RoomWaypoint> active = new();

        public void Subscribe(IRobotNavigationListener robot) => listeners.Add(robot);
        public void Unsubscribe(IRobotNavigationListener robot) => listeners.Remove(robot);
        public void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable)
        {
            foreach (var l in listeners)
                l.OnPathObsoleted(changed);
        }

        public List<RoomWaypoint> GetAllWaypoints() => active;
        public List<RoomWaypoint> GetActiveWaypoints() => active;
        public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end)
        {
            FindWorldPathCalled = true;
            return path;
        }
        public RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false)
        {
            return active.Count > 0 ? active[0] : null;
        }
        public RoomWaypoint GetEndPoint() => null;
        public RoomWaypoint GetStartPoint() => null;
        public void UpdateClosestWaypointToPlayer(Vector2 playerPosition) {}
        public RoomWaypoint ClosestWaypointToPlayer => null;

        public void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints) {}
        public void UnregisterRoomWaypoints(RoomManager room) {}
        public void BuildAllNeighbors(bool includeUnavailable = false) {}
        public RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetFirstFreeSecurityPoint() => null;
        public RoomWaypoint GetSecurityOrRestPoint(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetBlockedRoomSecuritySpawning(RoomWaypoint exclude = null) => null;
        public RoomWaypoint GetBlockedRoomCenter(RoomWaypoint exclude = null) => null;
        public void ReleasePOI(RoomWaypoint poi) {}
        public FactoryMachine ReserveFreeMachine(RoomManager room, EnemyWorkerController worker) => null;
        public void ReleaseMachine(FactoryMachine machine) {}
        public bool IsMachineReserved(FactoryMachine machine) => false;
    }

    private class DummyMover : IMover
    {
        public float Horizontal { get; private set; }
        public float Vertical { get; private set; }
        public void SetMovement(float direction) { Horizontal = direction; }
        public void SetVerticalMovement(float direction) { Vertical = direction; }
    }

    private class TestPathFollower : WaypointPathFollower, IRobotNavigationListener
    {
        public bool Notified { get; private set; }
        public TestPathFollower(Transform body, IMover mover, IWaypointQueries queries)
            : base(body, mover, queries) {}

        public new void OnPathObsoleted(RoomWaypoint blockedWaypoint)
        {
            Notified = true;
        }
    }

    [Test]
    public void WaypointNavigation_UsesServiceAndNotifiesListener()
    {
        var body = new GameObject().transform;
        var mover = new DummyMover();
        var service = new MockWaypointService();

        var start = new GameObject().AddComponent<RoomWaypoint>();
        start.transform.position = Vector3.zero;
        var target = new GameObject().AddComponent<RoomWaypoint>();
        target.transform.position = new Vector3(5f, 0f, 0f);

        service.active = new List<RoomWaypoint> { start, target };
        service.path = new List<RoomWaypoint> { start, target };

        var follower = new TestPathFollower(body, mover, service);

        follower.SetDestination(target);
        Assert.IsTrue(service.FindWorldPathCalled);

        follower.Update(0.1f);
        Assert.AreNotEqual(0f, mover.Horizontal);

        service.Subscribe(follower);
        service.NotifyWaypointStatusChanged(target, false);
        Assert.IsTrue(follower.Notified);
    }

    [Test]
    public void EnemyPunching_TracksPlayerPosition()
    {
        var go = new GameObject();
        var punch = go.AddComponent<EnemyPunchAttack>();

        var targetHandler = new GameObject().AddComponent<FollowPlayerTriggerHandler>();
        targetHandler.transform.position = new Vector3(1f, 2f, 0f);
        typeof(FollowPlayerTriggerHandler).GetField("playerBodyReferencePosition", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(targetHandler, new Vector3(1f, 2f, 0f));
        typeof(FollowPlayerTriggerHandler).GetField("isFacingRight", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(targetHandler, true);

        var punchTarget = new GameObject().transform;
        punch.punchTarget = punchTarget;

        punch.rightRestPosition = new GameObject().transform;
        punch.rightArmTarget = new GameObject().transform;
        punch.rightArmHitbox = new GameObject().AddComponent<AttackHitbox>();
        punch.arcControlRight = new GameObject().transform;

        typeof(EnemyPunchAttack).GetField("playerInAttackZone", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(punch, true);
        typeof(EnemyPunchAttack).GetField("targetToFollow", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(punch, targetHandler);

        typeof(EnemyPunchAttack).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(punch, null);

        Assert.AreEqual(targetHandler.transform.position, punchTarget.position);
    }

    [UnityTest]
    public IEnumerator EnemyPunching_RightArmReturnsToRightRest()
    {
        var go = new GameObject();
        var punch = go.AddComponent<EnemyPunchAttack>();

        punch.punchDuration = 0.01f;
        punch.returnSpeed = 100f;

        var targetHandler = new GameObject().AddComponent<FollowPlayerTriggerHandler>();
        typeof(FollowPlayerTriggerHandler).GetField("isFacingRight", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(targetHandler, false);

        punch.punchTarget = new GameObject().transform;
        punch.punchTarget.position = new Vector3(1f, 0f, 0f);

        punch.rightArmTarget = new GameObject().transform;
        punch.rightRestPosition = new GameObject().transform;
        punch.rightRestPosition.position = new Vector3(2f, 0f, 0f);
        punch.rightArmTarget.position = punch.rightRestPosition.position;

        punch.leftArmTarget = new GameObject().transform;
        punch.leftRestPosition = new GameObject().transform;

        var control = new GameObject().transform;
        punch.arcControlLeft = control;
        punch.arcControlRight = control;

        typeof(EnemyPunchAttack).GetField("targetToFollow", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(punch, targetHandler);

        punch.rightArmHitbox = new GameObject().AddComponent<AttackHitbox>();

        var method = typeof(EnemyPunchAttack).GetMethod("PunchSequence", BindingFlags.NonPublic | BindingFlags.Instance);
        yield return punch.StartCoroutine((IEnumerator)method.Invoke(punch, new object[] { punch.rightArmTarget, punch.rightArmHitbox }));

        Assert.Less(Vector3.Distance(punch.rightArmTarget.position, punch.rightRestPosition.position), 0.01f);
    }

    [UnityTest]
    public IEnumerator EnemyPunching_LeftArmReturnsToLeftRest()
    {
        var go = new GameObject();
        var punch = go.AddComponent<EnemyPunchAttack>();

        punch.punchDuration = 0.01f;
        punch.returnSpeed = 100f;

        var targetHandler = new GameObject().AddComponent<FollowPlayerTriggerHandler>();
        typeof(FollowPlayerTriggerHandler).GetField("isFacingRight", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(targetHandler, true);

        punch.punchTarget = new GameObject().transform;
        punch.punchTarget.position = new Vector3(-1f, 0f, 0f);

        punch.leftArmTarget = new GameObject().transform;
        punch.leftRestPosition = new GameObject().transform;
        punch.leftRestPosition.position = new Vector3(-2f, 0f, 0f);
        punch.leftArmTarget.position = punch.leftRestPosition.position;

        punch.rightArmTarget = new GameObject().transform;
        punch.rightRestPosition = new GameObject().transform;

        var control = new GameObject().transform;
        punch.arcControlLeft = control;
        punch.arcControlRight = control;

        typeof(EnemyPunchAttack).GetField("targetToFollow", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(punch, targetHandler);

        punch.leftArmHitbox = new GameObject().AddComponent<AttackHitbox>();

        var method = typeof(EnemyPunchAttack).GetMethod("PunchSequence", BindingFlags.NonPublic | BindingFlags.Instance);
        yield return punch.StartCoroutine((IEnumerator)method.Invoke(punch, new object[] { punch.leftArmTarget, punch.leftArmHitbox }));

        Assert.Less(Vector3.Distance(punch.leftArmTarget.position, punch.leftRestPosition.position), 0.01f);
    }

    [Test]
    public void MachineShutdown_TurnsOffAndRaisesEvents()
    {
        var go = new GameObject();
        var machine = go.AddComponent<FactoryMachine>();

        bool stateChanged = false;
        bool poweredOff = false;
        bool turningOff = false;

        machine.OnMachineStateChanged += (m, on) => stateChanged = !on;
        machine.OnPoweredOff += m => poweredOff = true;
        machine.OnMachineTurningOff += (m, worker) => turningOff = true;

        machine.PowerOff();

        Assert.IsFalse(machine.IsOn);
        Assert.IsTrue(stateChanged);
        Assert.IsTrue(poweredOff);
        Assert.IsTrue(turningOff);
    }
}

