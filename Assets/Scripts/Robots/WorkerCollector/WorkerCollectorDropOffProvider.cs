using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Reusable garage destination that owns delivery reservations and batch notifications.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorDropOffProvider : MonoBehaviour
{
    [SerializeField] private RoomWaypoint approachWaypoint;
    [SerializeField] private Transform waitPoint;
    [SerializeField] private GarageCubeStorage storage;
    [SerializeField] private GarageCubeProcessor processor;

    private WorkerCollectorBodyController batchWaitingCollector;
    private WorkerCollectorMissionAssignment batchWaitingAssignment;
    private readonly Dictionary<int, WorkerCollectorBodyController> reservingCollectors =
        new Dictionary<int, WorkerCollectorBodyController>();

    public RoomWaypoint ApproachWaypoint => approachWaypoint;
    public Vector3 WaitPosition => waitPoint != null ? waitPoint.position : transform.position;
    public GarageCubeStorage Storage => storage;
    public GarageCubeProcessor Processor => processor;
    public bool IsGarageDestination => storage != null && processor != null;
    public bool CanAcceptDelivery => IsGarageDestination && processor.IsAccepting;

    private void Awake() => ResolveReferences();
    private void OnEnable()
    {
        ResolveReferences();
        if (processor != null)
        {
            processor.OnBatchCompleted -= HandleBatchCompleted;
            processor.OnBatchCompleted += HandleBatchCompleted;
            processor.OnBatchInterrupted -= HandleBatchInterrupted;
            processor.OnBatchInterrupted += HandleBatchInterrupted;
        }
        WorkerCollectorMissionService.RegisterDropOff(this);
    }

    private void OnDisable()
    {
        WorkerCollectorMissionService.UnregisterDropOff(this);
        if (processor != null)
        {
            processor.OnBatchCompleted -= HandleBatchCompleted;
            processor.OnBatchInterrupted -= HandleBatchInterrupted;
        }
        batchWaitingCollector?.ReportBatchInterrupted(batchWaitingAssignment);
        batchWaitingCollector = null;
        batchWaitingAssignment = null;
        WorkerCollectorBodyController[] affected = new WorkerCollectorBodyController[reservingCollectors.Count];
        reservingCollectors.Values.CopyTo(affected, 0);
        reservingCollectors.Clear();
        for (int i = 0; i < affected.Length; i++)
            affected[i]?.ReportDestinationUnavailable();
    }

    public void Configure(RoomWaypoint waypoint, Transform configuredWaitPoint,
        GarageCubeStorage configuredStorage, GarageCubeProcessor configuredProcessor)
    {
        approachWaypoint = waypoint;
        waitPoint = configuredWaitPoint;
        storage = configuredStorage;
        processor = configuredProcessor;
    }

    public bool TryReserve(WorkerCollectorBodyController collector, out GarageSlotReservation reservation)
    {
        reservation = default;
        if (collector == null || !CanAcceptDelivery || !storage.TryReserve(collector, out reservation))
            return false;
        reservingCollectors[collector.GetInstanceID()] = collector;
        return true;
    }

    public bool ReleaseReservation(GarageSlotReservation reservation)
    {
        reservingCollectors.Remove(reservation.OwnerInstanceId);
        return storage != null && storage.ReleaseReservation(reservation);
    }

    public bool TryAccept(WorkerCollectorBodyController collector, WorkerCollectorMissionAssignment assignment,
        out bool waitingForBatch)
    {
        waitingForBatch = false;
        if (collector == null || assignment == null || !CanAcceptDelivery
            || !storage.TryAccept(assignment.Target, assignment.Claim, assignment.Reservation))
            return false;

        waitingForBatch = storage.IsFull;
        reservingCollectors.Remove(collector.GetInstanceID());
        if (waitingForBatch)
        {
            batchWaitingCollector = collector;
            batchWaitingAssignment = assignment;
        }
        return true;
    }

    private void HandleBatchCompleted(int _)
    {
        WorkerCollectorBodyController collector = batchWaitingCollector;
        WorkerCollectorMissionAssignment assignment = batchWaitingAssignment;
        batchWaitingCollector = null;
        batchWaitingAssignment = null;
        collector?.ReportBatchCompleted(assignment);
    }

    private void HandleBatchInterrupted()
    {
        WorkerCollectorBodyController collector = batchWaitingCollector;
        WorkerCollectorMissionAssignment assignment = batchWaitingAssignment;
        batchWaitingCollector = null;
        batchWaitingAssignment = null;
        collector?.ReportBatchInterrupted(assignment);
    }

    private void ResolveReferences()
    {
        if (approachWaypoint == null)
        {
            RoomManager room = GetComponentInParent<RoomManager>();
            RoomWaypoint[] waypoints = room != null
                ? room.GetComponentsInChildren<RoomWaypoint>(true)
                : GetComponentsInChildren<RoomWaypoint>(true);
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null && waypoints[i].type == WaypointType.Work)
                {
                    approachWaypoint = waypoints[i];
                    break;
                }
            }
            if (approachWaypoint == null && waypoints.Length > 0)
                approachWaypoint = waypoints[0];
        }
        if (waitPoint == null)
            waitPoint = approachWaypoint != null ? approachWaypoint.transform : transform;
        if (storage == null)
            storage = GetComponent<GarageCubeStorage>() ?? GetComponentInChildren<GarageCubeStorage>(true);
        if (processor == null)
            processor = GetComponent<GarageCubeProcessor>() ?? GetComponentInChildren<GarageCubeProcessor>(true);
    }
}
