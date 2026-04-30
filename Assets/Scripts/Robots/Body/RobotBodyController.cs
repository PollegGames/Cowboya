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

    [SerializeField] private RobotBodyMaintenance bodyMaintenance;
    [Header("Combat")]
    [SerializeField] private RobotAttackController attackController;
    [SerializeField] private bool isBoss;

    private RobotHeartNew heart;
    private WaypointPathFollower pathFollower;
    private IWaypointQueries waypointQueries;
    private IWaypointNotifier waypointNotifier;
    private System.Action stuckHandler;

    protected override void Awake()
    {
        base.Awake();
        heart = GetComponent<RobotHeartNew>();
        if (bodyMaintenance == null)
            bodyMaintenance = GetComponent<RobotBodyMaintenance>();
        if (attackController == null)
            attackController = GetComponent<RobotAttackController>();

        if (heart != null && heart.Role == RobotRole.Follower)
        {
            // Follower prefab sometimes leaves bodyReference at root while hipRb moves; align to hip for correct path origin.
            if (bodyReference == transform && hipRb != null && hipRb.transform != transform)
                bodyReference = hipRb.transform;
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

        AlignBodyReferenceForRole();
        if (pathFollower == null)
            SetupPathFollower();
        if (this.waypointNotifier != null && pathFollower != null)
            this.waypointNotifier.Subscribe(pathFollower);
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

    private void AlignBodyReferenceForRole()
    {
        if (heart == null)
            heart = GetComponent<RobotHeartNew>();

        if (heart == null || heart.Role != RobotRole.Follower)
            return;
        if (hipRb == null)
            return;
        if (bodyReference != transform)
            return;

        bodyReference = hipRb.transform;
        RobotEcosystemProbe.RecordBodyNavigationReference(this, bodyReference, "follower_hip_reference");
    }

    private void HandlePathFollowerStuck()
    {
        bodyMaintenance?.OnStuck(this, isBoss);
    }

    public void SetDestination(RoomWaypoint target, bool includeUnavailable = false)
    {
        pathFollower?.SetDestination(target, includeUnavailable);
    }

    public void SetDestination(RoomWaypoint target, Vector3? finalPosition, bool includeUnavailable = false)
    {
        pathFollower?.SetDestination(target, finalPosition, includeUnavailable);
    }

    public void SetDestination(Vector3 worldPosition, bool includeUnavailable = false)
    {
        if (waypointQueries == null)
        {
            return;
        }
        Vector2 position2D = worldPosition;
        RoomWaypoint waypoint = waypointQueries.GetClosestWaypoint(position2D, includeUnavailable);
        if (waypoint != null)
            pathFollower?.SetDestination(waypoint, worldPosition, includeUnavailable);
    }

    public bool HasArrivedAtDestination() => pathFollower != null && pathFollower.HasArrived;
    public RoomWaypoint CurrentTarget => pathFollower != null ? pathFollower.CurrentTarget : null;
    public RoomWaypoint StartPoint => waypointQueries?.GetStartPoint();
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
        StopMovement();
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
        SetMovement(0f);
        SetVerticalMovement(0f);
        if (pathFollower == null && waypointQueries != null)
        {
            AlignBodyReferenceForRole();
            SetupPathFollower();
        }
    }

    public void StopMovement()
    {
        pathFollower?.ClearPath();
    }


    private void OnDrawGizmos()
    {
        pathFollower?.DrawGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        pathFollower?.DrawGizmos();
    }

}

