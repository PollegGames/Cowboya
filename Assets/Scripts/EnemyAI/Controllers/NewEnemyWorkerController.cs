using UnityEngine;

/// <summary>
/// Controls enemy worker navigation using waypoint-based pathing.
/// </summary>
public class NewEnemyWorkerController : NewAnimatorBaseAgentController
{
    [SerializeField] private float arrivalThresholdX = 2f;
    [SerializeField] private float arrivalThresholdY = 2f;
    [SerializeField] private float deadZoneX = 5f;
    [SerializeField] private float deadZoneY = 5f;
    [SerializeField] private UpdateLoop updateLoop = UpdateLoop.Update;

    private WaypointPathFollower pathFollower;
    private IWaypointQueries waypointQueries;
    private IWaypointNotifier waypointNotifier;
    private IWaypointService waypointService;

    protected override void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        base.Awake();
    }

    /// <summary>
    /// Initializes navigation services and subscribes to waypoint updates.
    /// </summary>
    public void Initialize(IWaypointService service)
    {
        waypointService = service;
        waypointQueries = service;
        waypointNotifier = service;

        pathFollower = new WaypointPathFollower(bodyReference, this, waypointQueries,
            arrivalThresholdX, arrivalThresholdY, deadZoneX, deadZoneY);
        waypointNotifier.Subscribe(pathFollower);
    }

    private void Update()
    {
        if (updateLoop == UpdateLoop.Update)
            pathFollower?.Update(Time.deltaTime);

        TryFlip(direction);
    }

    private void FixedUpdate()
    {
        if (updateLoop == UpdateLoop.FixedUpdate)
            pathFollower?.Update(Time.fixedDeltaTime);
    }

    /// <summary>
    /// Sets a waypoint destination for the enemy worker.
    /// </summary>
    public void SetDestination(RoomWaypoint target, bool includeUnavailable = false) =>
        pathFollower?.SetDestination(target, includeUnavailable);
}

