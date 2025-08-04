using UnityEngine;

/// <summary>
/// Controls allied robot navigation and interactions with machines.
/// Uses <see cref="WaypointPathFollower"/> for navigation while avoiding any
/// player specific logic.
/// </summary>
public class AllyWorkerController : AnimatorBaseAgentController
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

     protected override void Update()
    {
        base.Update();
        TryFlip(direction);
        if (updateLoop == UpdateLoop.Update)
            pathFollower?.Update(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (updateLoop == UpdateLoop.FixedUpdate)
            pathFollower?.Update(Time.fixedDeltaTime);
    }

    /// <summary>
    /// Sets a waypoint destination for the ally.
    /// </summary>
    public void SetDestination(RoomWaypoint target, bool includeUnavailable = false) =>
        pathFollower?.SetDestination(target, includeUnavailable);

    /// <summary>
    /// Notifies the controller that a path has become blocked.
    /// </summary>
    public void OnPathObsoleted(RoomWaypoint blockedWaypoint) =>
        pathFollower?.OnPathObsoleted(blockedWaypoint);

    /// <summary>
    /// Gets the closest waypoint to the ally.
    /// </summary>
    public RoomWaypoint GetClosestWaypoint(RoomWaypoint exclude = null) =>
        pathFollower?.GetClosestWaypoint(exclude);

    private void OnTriggerEnter2D(Collider2D collision) => HandleMachineTrigger(collision);

    private void OnTriggerExit2D(Collider2D collision) => HandleMachineTrigger(collision);

    private void HandleMachineTrigger(Collider2D collision)
    {
        var machine = collision.GetComponentInParent<BaseMachine>();
        if (machine == null)
            return;

        machine.SetState(false);
        if (waypointService == null)
            return;

        if (machine is FactoryMachine factory)
        {
            waypointService.ReleaseMachine(factory);
        }
        else
        {
            var poi = waypointService.GetClosestWaypoint(machine.transform.position);
            waypointService.ReleasePOI(poi);
        }
    }
}

