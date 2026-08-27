using UnityEngine;

/// <summary>
/// Reusable room capability that exposes loose white cubes and a navigation approach.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorWhiteCubeSourceProvider : MonoBehaviour
{
    [SerializeField] private RoomWaypoint approachWaypoint;
    [SerializeField] private Transform cubeRoot;

    public RoomWaypoint ApproachWaypoint => approachWaypoint;

    private void Awake() => ResolveReferences();
    private void OnEnable()
    {
        ResolveReferences();
        WorkerCollectorMissionService.RegisterSource(this);
    }

    private void OnDisable() => WorkerCollectorMissionService.UnregisterSource(this);

    /// <summary>
    /// Claims the first available white cargo currently owned by this source hierarchy.
    /// </summary>
    public bool TryClaimCube(Object claimant, out WhiteCubeCargo cargo, out WhiteCubeClaim claim)
    {
        cargo = null;
        claim = default;
        if (claimant == null)
            return false;

        Transform searchRoot = cubeRoot != null ? cubeRoot : transform;
        WhiteCubeCargo[] candidates = searchRoot.GetComponentsInChildren<WhiteCubeCargo>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            WhiteCubeCargo candidate = candidates[i];
            if (candidate != null && candidate.TryClaim(claimant, out claim))
            {
                cargo = candidate;
                return true;
            }
        }
        return false;
    }

    private void ResolveReferences()
    {
        if (cubeRoot == null)
            cubeRoot = transform;
        if (approachWaypoint != null)
            return;

        RoomWaypoint[] waypoints = GetComponentsInChildren<RoomWaypoint>(true);
        for (int i = 0; i < waypoints.Length; i++)
        {
            if (waypoints[i] != null && waypoints[i].type == WaypointType.Work)
            {
                approachWaypoint = waypoints[i];
                return;
            }
        }
        if (waypoints.Length > 0)
            approachWaypoint = waypoints[0];
    }
}
