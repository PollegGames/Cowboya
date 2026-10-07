using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Reusable garage destination that owns delivery reservations and batch notifications.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorDropOffProvider : MonoBehaviour
{
    [SerializeField] private RoomWaypoint approachWaypoint;
    [SerializeField] private Transform deliveryPoint;
    [SerializeField] private Transform waitPoint;
    [SerializeField] private GarageCubeStorage storage;
    [SerializeField] private GarageCubeProcessor processor;

    private WorkerCollectorBodyController batchWaitingCollector;
    private WorkerCollectorMissionAssignment batchWaitingAssignment;
    private GarageCubeCoordinator coordinator;
    private Coroutine registrationRoutine;
    private readonly Dictionary<int, WorkerCollectorBodyController> reservingCollectors =
        new Dictionary<int, WorkerCollectorBodyController>();

    public RoomWaypoint ApproachWaypoint => approachWaypoint;
    public Transform DeliveryPoint => deliveryPoint;
    public Transform WaitPoint => waitPoint;
    public Vector3 DeliveryPosition => deliveryPoint != null ? deliveryPoint.position : transform.position;
    public Vector3 WaitPosition => waitPoint != null ? waitPoint.position : transform.position;
    public GarageCubeStorage Storage => storage;
    public GarageCubeProcessor Processor => processor;
    public bool IsGarageDestination => storage != null && processor != null;
    public bool CanAcceptDelivery => isActiveAndEnabled && IsGarageDestination
        && storage.isActiveAndEnabled && processor.IsAccepting;

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
        coordinator = GetComponent<GarageCubeCoordinator>();
        registrationRoutine = StartCoroutine(RegisterWhenGarageReady());
    }

    private void OnDisable()
    {
        WorkerCollectorMissionService.UnregisterDropOff(this);
        if (registrationRoutine != null)
        {
            StopCoroutine(registrationRoutine);
            registrationRoutine = null;
        }
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

    private System.Collections.IEnumerator RegisterWhenGarageReady()
    {
        while (isActiveAndEnabled)
        {
            if (coordinator == null)
                coordinator = GetComponent<GarageCubeCoordinator>();
            // Standalone configured garages can expose the same capability without
            // requiring the optional authored-prefab startup coordinator.
            if (coordinator != null ? coordinator.IsReady
                : IsGarageDestination && storage.isActiveAndEnabled && processor.isActiveAndEnabled)
            {
                WorkerCollectorMissionService.RegisterDropOff(this);
                registrationRoutine = null;
                yield break;
            }
            yield return null;
        }
    }

    public void Configure(RoomWaypoint waypoint, Transform configuredDeliveryPoint, Transform configuredWaitPoint,
        GarageCubeStorage configuredStorage, GarageCubeProcessor configuredProcessor)
    {
        approachWaypoint = waypoint;
        deliveryPoint = configuredDeliveryPoint;
        waitPoint = configuredWaitPoint;
        storage = configuredStorage;
        processor = configuredProcessor;
    }

    public bool TryReserve(WorkerCollectorBodyController collector, out GarageSlotReservation reservation)
    {
        reservation = default;
        if (collector == null)
        {
            Debug.LogWarning("Worker Collector garage reservation failed: collector is null.", this);
            return false;
        }

        if (!CanAcceptDelivery)
        {
            Debug.LogWarning(
                $"Worker Collector garage reservation failed: providerActive={isActiveAndEnabled}, "
                + $"providerValid={IsGarageDestination}, processorActive={processor != null && processor.isActiveAndEnabled}, "
                + $"processorState={(processor != null ? processor.State.ToString() : "null")}, "
                + $"doorOpen={(processor != null && processor.IsDoorOpen)}, "
                + $"storageActive={(storage != null && storage.isActiveAndEnabled)}, "
                + $"occupied={(storage != null ? storage.OccupiedCount.ToString() : "null")}, "
                + $"full={(storage != null && storage.IsFull)}.", this);
            return false;
        }

        if (!storage.TryReserve(collector, out reservation))
        {
            Debug.LogWarning(
                $"Worker Collector garage reservation failed: storage has no usable slot. "
                + $"occupied={storage.OccupiedCount}, full={storage.IsFull}, slots={storage.Slots.Count}.", this);
            return false;
        }

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
        if (deliveryPoint == null)
            deliveryPoint = transform.Find("DeliveryPoint");
        if (deliveryPoint == null)
            deliveryPoint = approachWaypoint != null ? approachWaypoint.transform : transform;
        if (waitPoint == null)
            waitPoint = transform.Find("WorkerWaitPoint");
        if (waitPoint == null)
            waitPoint = deliveryPoint;
        if (storage == null)
            storage = GetComponent<GarageCubeStorage>() ?? GetComponentInChildren<GarageCubeStorage>(true);
        if (processor == null)
            processor = GetComponent<GarageCubeProcessor>() ?? GetComponentInChildren<GarageCubeProcessor>(true);
    }
}
