using UnityEngine;

/// <summary>
/// Executes movement/physics for a robot. This is the Body pillar that drives locomotion
/// and reacts to navigation events, while Brain/Heart decide intent.
/// </summary>
[RequireComponent(typeof(RobotBodyMaintenance))]
public class RobotBodyController : AnimatorBaseAgentController, IPooledObject
{
    [Header("Navigation")]
    [SerializeField] private float arrivalThresholdX = 2f;
    [SerializeField] private float arrivalThresholdY = 2f;
    [SerializeField] private float deadZoneX = 4f;
    [SerializeField] private float deadZoneY = 4f;
    [SerializeField] private UpdateLoop updateLoop = UpdateLoop.Update;
    [SerializeField] private bool logFollowerPathing = true;

    [SerializeField] private RobotBodyMaintenance bodyMaintenance;
    [Header("Combat")]
    [SerializeField] private RobotAttackController attackController;
    [SerializeField] private bool isBoss;

    private RobotHeart heart;
    private WaypointPathFollower pathFollower;
    private IWaypointQueries waypointQueries;
    private IWaypointNotifier waypointNotifier;
    private System.Action stuckHandler;

    protected override void Awake()
    {
        base.Awake();
        heart = GetComponent<RobotHeart>();
        if (bodyMaintenance == null)
            bodyMaintenance = GetComponent<RobotBodyMaintenance>();
        if (attackController == null)
            attackController = GetComponent<RobotAttackController>();

        if (heart != null && heart.Role == RobotRole.Follower)
        {
            // Follower prefab sometimes leaves bodyReference at root while hipRb moves; align to hip for correct path origin.
            if (bodyReference == transform && hipRb != null && hipRb.transform != transform)
                bodyReference = hipRb.transform;
            LogFollower($"Awake bodyReference={bodyReference?.name} hipRb={hipRb?.name} attackController={attackController?.name}");
        }
    }

    public void Initialize(
        IWaypointQueries waypointQueries,
        IWaypointNotifier waypointNotifier,
        IRobotRespawnService respawnService)
    {
        this.waypointQueries = waypointQueries;
        this.waypointNotifier = waypointNotifier;
        if (bodyMaintenance != null && respawnService != null)
            bodyMaintenance.SetRespawnService(respawnService);

        if (pathFollower == null)
            SetupPathFollower();
        if (this.waypointNotifier != null && pathFollower != null)
            this.waypointNotifier.Subscribe(pathFollower);
        LogFollower($"Initialize waypointQueries={waypointQueries?.GetType().Name} waypointNotifier={waypointNotifier?.GetType().Name} pathFollower={(pathFollower != null ? "ready" : "null")} bodyReference={bodyReference?.name}");

    }

    public void SetIsBoss(bool value)
    {
        isBoss = value;
    }

    private void Update()
    {
        if (updateLoop == UpdateLoop.Update)
            pathFollower?.Update(Time.deltaTime);

        // If we arrived this frame, clear movement so the bot stays put until a new task.
        if (HasArrivedAtDestination())
            StopMovement();
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
        if (updateLoop == UpdateLoop.FixedUpdate)
            pathFollower?.Update(Time.fixedDeltaTime);

        if (HasArrivedAtDestination())
            StopMovement();
    }

    private void SetupPathFollower()
    {
        pathFollower = new WaypointPathFollower(bodyReference, this, waypointQueries,
            arrivalThresholdX, arrivalThresholdY, deadZoneX, deadZoneY);
        stuckHandler = HandlePathFollowerStuck;
        pathFollower.OnStuck += stuckHandler;
    }

    private void HandlePathFollowerStuck()
    {
        bodyMaintenance?.OnStuck(this, isBoss);
    }

    public void SetDestination(RoomWaypoint target, bool includeUnavailable = false)
    {
        pathFollower?.SetDestination(target, includeUnavailable);
        LogFollower($"SetDestination waypoint={DescribeWaypoint(target)} includeUnavailable={includeUnavailable}");
        LogPathState("PathState");
    }

    public void SetDestination(Vector3 worldPosition, bool includeUnavailable = false)
    {
        if (waypointQueries == null)
        {
            LogFollower($"SetDestination worldPosition={worldPosition} includeUnavailable={includeUnavailable} but waypointQueries is null");
            return;
        }
        Vector2 position2D = worldPosition;
        RoomWaypoint waypoint = waypointQueries.GetClosestWaypoint(position2D, includeUnavailable);
        if (waypoint != null)
            pathFollower?.SetDestination(waypoint, worldPosition, includeUnavailable);
        LogFollower($"SetDestination worldPosition={worldPosition} includeUnavailable={includeUnavailable} closestWaypoint={DescribeWaypoint(waypoint)}");
        LogPathState("PathState");
    }

    public bool HasArrivedAtDestination() => pathFollower != null && pathFollower.HasArrived;
    public bool HasActivePath =>
        pathFollower != null
        && pathFollower.CurrentPathCount > 0
        && pathFollower.PathIndex < pathFollower.CurrentPathCount;

    public void OnPathObsoleted(RoomWaypoint blockedWaypoint) =>
        pathFollower?.OnPathObsoleted(blockedWaypoint);

    public RoomWaypoint GetClosestWaypoint(RoomWaypoint exclude = null) =>
        pathFollower != null ? pathFollower.GetClosestWaypoint(exclude) : null;

    public RobotAttackController AttackController => attackController;

    public void OnReleaseToPool()
    {
        if (pathFollower != null)
        {
            pathFollower.OnStuck -= stuckHandler;
            waypointNotifier?.Unsubscribe(pathFollower);
        }
        pathFollower = null;
        stuckHandler = null;
    }

    public void OnAcquireFromPool()
    {
        if (pathFollower == null && waypointQueries != null)
            SetupPathFollower();
    }

    public void StopMovement()
    {
        pathFollower?.ClearPath();
        LogFollower("StopMovement (clearing path)");
    }

    private void OnDrawGizmos()
    {
        pathFollower?.DrawGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        pathFollower?.DrawGizmos();
    }

    private bool ShouldLogFollower => logFollowerPathing && heart != null && heart.Role == RobotRole.Follower;

    private void LogFollower(string message)
    {
        if (!ShouldLogFollower)
            return;
        Debug.Log($"[Follower][Body] {message}", this);
    }

    private void LogPathState(string prefix)
    {
        if (!ShouldLogFollower || pathFollower == null)
            return;
        string target = pathFollower.CurrentTarget != null ? pathFollower.CurrentTarget.name : "null";
        string lastAttempted = pathFollower.LastAttemptedWaypoint != null ? pathFollower.LastAttemptedWaypoint.name : "null";
        Debug.Log(
            $"[Follower][Body] {prefix} pathCount={pathFollower.CurrentPathCount} waypointCount={pathFollower.CurrentWaypointsCount} pathIndex={pathFollower.PathIndex} target={target} lastAttempted={lastAttempted}",
            this);
    }

    private static string DescribeWaypoint(RoomWaypoint waypoint)
    {
        if (waypoint == null)
            return "null";
        string room = waypoint.parentRoom != null ? waypoint.parentRoom.name : "null";
        return $"{waypoint.name} type={waypoint.type} parentRoom={room} isAvailable={waypoint.IsAvailable} worldPos={waypoint.WorldPos}";
    }

}
