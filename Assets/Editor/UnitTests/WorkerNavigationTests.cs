using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class WorkerNavigationTests
{
    private class RecordingMover : IMover
    {
        private readonly Transform body;
        private readonly float speed;

        public float LastHorizontal { get; private set; }
        public float LastVertical { get; private set; }

        public RecordingMover(Transform body, float speed = 4f)
        {
            this.body = body;
            this.speed = speed;
        }

        public void SetMovement(float direction)
        {
            LastHorizontal = direction;
        }

        public void SetVerticalMovement(float direction)
        {
            LastVertical = direction;
        }

        public void Apply(float deltaTime)
        {
            Vector3 delta = new Vector3(LastHorizontal, LastVertical, 0f) * speed * deltaTime;
            body.position += delta;
        }
    }

    private class DummyWaypointQueries : IWaypointQueries
    {
        private readonly RoomWaypoint start;
        private readonly RoomWaypoint target;

        public DummyWaypointQueries(RoomWaypoint start, RoomWaypoint target)
        {
            this.start = start;
            this.target = target;
        }

        public List<RoomWaypoint> FindWorldPath(RoomWaypoint s, RoomWaypoint e) =>
            new List<RoomWaypoint> { start, target };

        public List<RoomWaypoint> GetActiveWaypoints() => new List<RoomWaypoint> { start, target };

        public List<RoomWaypoint> GetAllWaypoints() => GetActiveWaypoints();

        public RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false) => start;

        public RoomWaypoint GetEndPoint() => target;

        public RoomWaypoint GetStartPoint() => start;

        public void UpdateClosestWaypointToPlayer(Vector2 playerPosition) { }

        public RoomWaypoint ClosestWaypointToPlayer => start;
    }

    private class StubWaypointService : IWaypointService
    {
        public RoomWaypoint WorkPoint { get; set; }
        public RoomWaypoint RestPoint { get; set; }

        public RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null) => WorkPoint;

        public RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null) => RestPoint;

        public void ReleasePOI(RoomWaypoint poi) { }

        public void Subscribe(IRobotNavigationListener robot) { }

        public void Unsubscribe(IRobotNavigationListener robot) { }

        public void NotifyWaypointStatusChanged(RoomWaypoint changed, bool isAvailable) { }

        public void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints) { }

        public void UnregisterRoomWaypoints(RoomManager room) { }

        public void BuildAllNeighbors(bool includeUnavailable = false) { }

        public RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null) => WorkPoint ?? RestPoint;

        public RoomWaypoint GetFirstFreeSecurityPoint() => null;

        public RoomWaypoint GetSecurityOrRestPoint(RoomWaypoint exclude = null) => null;

        public RoomWaypoint GetBlockedRoomSecuritySpawning(RoomWaypoint exclude = null) => null;

        public RoomWaypoint GetBlockedRoomCenter(RoomWaypoint exclude = null) => null;

        public FactoryMachine ReserveFreeMachine(RoomManager room, EnemyWorkerController worker) => null;

        public void ReleaseMachine(FactoryMachine machine) { }

        public bool IsMachineReserved(FactoryMachine machine) => false;

        public List<RoomWaypoint> GetAllWaypoints() => new List<RoomWaypoint> { WorkPoint, RestPoint };

        public List<RoomWaypoint> GetActiveWaypoints() => GetAllWaypoints();

        public List<RoomWaypoint> FindWorldPath(RoomWaypoint start, RoomWaypoint end) =>
            new List<RoomWaypoint> { start, end };

        public RoomWaypoint GetClosestWaypoint(Vector2 position, bool includeUnavailable = false) => WorkPoint;

        public RoomWaypoint GetEndPoint() => RestPoint;

        public RoomWaypoint GetStartPoint() => WorkPoint;

        public void UpdateClosestWaypointToPlayer(Vector2 playerPosition) { }

        public RoomWaypoint ClosestWaypointToPlayer => WorkPoint;
    }

    private class StubMemory : IRobotMemory
    {
        public Vector3 LastKnownPlayerPosition => Vector3.zero;

        public bool WasRecentlyAttacked => false;

        public RoomWaypoint LastVisitedPoint { get; private set; }

        public void SetRespawnService(IRobotRespawnService service) { }

        public void SetLastVisitedPoint(RoomWaypoint point)
        {
            LastVisitedPoint = point;
        }

        public void OnStuck(EnemyWorkerController controller) { }

        public void OnBossStuck(EnemyController controller) { }

        public void RememberPlayerPosition(Vector3 playerPosition) { }

        public void ClearPlayerPosition() { }

        public void RegisterAttack() { }

        public void ResetAttackMemory() { }
    }

    private class StubRespawnService : IRobotRespawnService
    {
        public void RespawnBoss() { }

        public void RespawnFollower() { }

        public void RespawnPlayer() { }

        public void RespawnWorker() { }
    }

    private class TestEnemyWorkerController : EnemyWorkerController
    {
        public RoomWaypoint LastDestination { get; private set; }
        public bool Arrived { get; set; }
        public float LastHorizontal { get; private set; }
        public float LastVertical { get; private set; }

        protected override void Awake()
        {
            // Tests configure dependencies directly.
            bodyReference = transform;
        }

        public override void SetMovement(float direction)
        {
            LastHorizontal = direction;
        }

        public override void SetVerticalMovement(float direction)
        {
            LastVertical = direction;
        }

        public override void SetDestination(RoomWaypoint target, bool includeUnavailable = false)
        {
            LastDestination = target;
        }

        public override bool HasArrivedAtDestination()
        {
            return Arrived;
        }

        public void InjectMemory(IRobotMemory injectedMemory)
        {
            memory = injectedMemory;
        }
    }

    [UnityTest]
    public IEnumerator WaypointPathFollower_ReachesDestination()
    {
        var body = new GameObject("Body");
        body.transform.position = Vector3.zero;

        var startGO = new GameObject("StartWaypoint");
        startGO.transform.position = Vector3.zero;
        var startWp = startGO.AddComponent<RoomWaypoint>();

        var targetGO = new GameObject("TargetWaypoint");
        targetGO.transform.position = new Vector3(6f, 0f, 0f);
        var targetWp = targetGO.AddComponent<RoomWaypoint>();

        var queries = new DummyWaypointQueries(startWp, targetWp);
        var mover = new RecordingMover(body.transform, 8f);

        var follower = new WaypointPathFollower(body.transform, mover, queries, arrivalThresholdX: 0.1f, arrivalThresholdY: 0.1f);
        follower.SetDestination(targetWp);

        const float deltaTime = 0.1f;
        int safety = 0;
        while (!follower.HasArrived && safety++ < 200)
        {
            follower.Update(deltaTime);
            mover.Apply(deltaTime);
            yield return null;
        }

        Assert.IsTrue(follower.HasArrived, "Path follower never reported arrival.");
        Assert.That(body.transform.position.x, Is.GreaterThanOrEqualTo(targetWp.WorldPos.x - 0.1f));

        Object.DestroyImmediate(body);
        Object.DestroyImmediate(startGO);
        Object.DestroyImmediate(targetGO);
    }

    [UnityTest]
    public IEnumerator WorkerState_MovesFromWorkPrepToRestingPath()
    {
        var workerGO = new GameObject("Worker");
        var stateMachine = workerGO.AddComponent<WorkerStateMachine>();
        var worker = workerGO.AddComponent<TestEnemyWorkerController>();
        var memory = new StubMemory();
        worker.InjectMemory(memory);

        var workGO = new GameObject("WorkPoint");
        var workPoint = workGO.AddComponent<RoomWaypoint>();
        var restGO = new GameObject("RestPoint");
        var restPoint = restGO.AddComponent<RoomWaypoint>();

        var waypointService = new StubWaypointService
        {
            WorkPoint = workPoint,
            RestPoint = restPoint
        };

        worker.stateMachine = stateMachine;
        worker.waypointService = waypointService;
        worker.Initialize(waypointService, waypointService, new StubRespawnService(), null, spawnInitialPickups: false);

        var goToWork = new Worker_GoingToLeastWorkedStation(worker, stateMachine, waypointService);
        goToWork.EnterState();

        Assert.AreEqual(WorkerStatus.GoingToWork, worker.workerState);
        Assert.AreEqual(workPoint, worker.LastDestination);

        worker.Arrived = true;
        goToWork.UpdateState();

        Assert.AreEqual(WorkerStatus.ReadyToWork, worker.workerState);
        Assert.AreEqual(workPoint, memory.LastVisitedPoint);

        yield return new WaitForSeconds(10.1f);
        goToWork.UpdateState();

        Assert.IsInstanceOf<Worker_GoingToRestStation>(stateMachine.enemyState);
        Assert.AreEqual(WorkerStatus.GoingToRest, worker.workerState);
        Assert.AreEqual(restPoint, worker.LastDestination);

        Object.DestroyImmediate(workerGO);
        Object.DestroyImmediate(workGO);
        Object.DestroyImmediate(restGO);
    }
}
