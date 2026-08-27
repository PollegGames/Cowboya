using UnityEngine;

/// <summary>
/// Temporary Part 3 destination used to verify carrying before garage storage exists.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorDropOffProvider : MonoBehaviour
{
    [SerializeField] private RoomWaypoint approachWaypoint;
    [SerializeField] private Transform waitPoint;

    public RoomWaypoint ApproachWaypoint => approachWaypoint;
    public Vector3 WaitPosition => waitPoint != null ? waitPoint.position : transform.position;

    private void Awake() => ResolveReferences();
    private void OnEnable()
    {
        ResolveReferences();
        WorkerCollectorMissionService.RegisterDropOff(this);
    }

    private void OnDisable() => WorkerCollectorMissionService.UnregisterDropOff(this);

    private void ResolveReferences()
    {
        if (approachWaypoint == null)
        {
            RoomWaypoint[] waypoints = GetComponentsInChildren<RoomWaypoint>(true);
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
    }
}
