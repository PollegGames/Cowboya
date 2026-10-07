using UnityEngine;

/// <summary>
/// Reusable room capability that exposes loose white cubes and a navigation approach.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorWhiteCubeSourceProvider : MonoBehaviour
{
    [SerializeField] private RoomWaypoint approachWaypoint;
    [SerializeField] private Transform cubeRoot;
    [UnityEngine.Serialization.FormerlySerializedAs("conveyorClaimRadius")]
    [SerializeField, Min(0.1f)] private float localPickupRadius = 3f;

    private readonly System.Collections.Generic.List<ClaimedCargo> issuedClaims =
        new System.Collections.Generic.List<ClaimedCargo>();

    private readonly struct ClaimedCargo
    {
        public ClaimedCargo(WhiteCubeCargo cargo, WhiteCubeClaim claim)
        {
            Cargo = cargo;
            Claim = claim;
        }
        public WhiteCubeCargo Cargo { get; }
        public WhiteCubeClaim Claim { get; }
    }

    public RoomWaypoint ApproachWaypoint => approachWaypoint;
    public float LocalPickupRadius => Mathf.Max(0.1f, localPickupRadius);

    /// <summary>
    /// Returns the cube position in the same world XY plane used by robot navigation and IK.
    /// </summary>
    public Vector2 GetWorkerPlanePickupPoint(WhiteCubeCargo cargo)
    {
        if (cargo == null)
            return Vector2.zero;

        Rigidbody2D body = cargo.GetComponent<Rigidbody2D>();
        return body != null ? body.position : (Vector2)cargo.transform.position;
    }

    private void Awake() => ResolveReferences();
    private void OnEnable()
    {
        ResolveReferences();
        WorkerCollectorMissionService.RegisterSource(this);
    }

    private void OnDisable()
    {
        WorkerCollectorMissionService.UnregisterSource(this);
        for (int i = 0; i < issuedClaims.Count; i++)
        {
            ClaimedCargo issued = issuedClaims[i];
            issued.Cargo?.ReleaseClaim(issued.Claim);
        }
        issuedClaims.Clear();
    }

    /// <summary>
    /// Claims the nearest eligible white cargo within the worker's local acquisition area.
    /// </summary>
    public bool TryClaimCube(Object claimant, out WhiteCubeCargo cargo, out WhiteCubeClaim claim)
    {
        cargo = null;
        claim = default;
        if (claimant == null || !isActiveAndEnabled)
            return false;

        issuedClaims.RemoveAll(issued => issued.Cargo == null
            || !issued.Cargo.IsClaimValid(issued.Claim));

        Transform searchRoot = cubeRoot != null ? cubeRoot : transform;
        WhiteCubeCargo[] candidates = searchRoot.GetComponentsInChildren<WhiteCubeCargo>(true);
        Vector2 origin = GetAcquisitionOrigin(claimant);
        float maximumDistance = GetAcquisitionRadius(claimant);
        float nearestDistanceSquared = maximumDistance * maximumDistance;
        WhiteCubeCargo nearest = null;
        for (int i = 0; i < candidates.Length; i++)
        {
            WhiteCubeCargo candidate = candidates[i];
            if (candidate == null || !candidate.IsAvailable)
                continue;
            float distanceSquared = (GetWorkerPlanePickupPoint(candidate) - origin).sqrMagnitude;
            if (distanceSquared <= nearestDistanceSquared) {
                nearest = candidate;
                nearestDistanceSquared = distanceSquared;
            }
        }
        if (nearest == null || !nearest.TryClaim(claimant, out claim))
            return false;

        cargo = nearest;
        issuedClaims.Add(new ClaimedCargo(nearest, claim));
        return true;
    }

    /// <summary>
    /// Checks whether a moving target still belongs to this source and remains in local reach.
    /// </summary>
    public bool IsTargetInCollectionArea(WorkerCollectorBodyController collector, WhiteCubeCargo target) {
        if (collector == null || target == null || !isActiveAndEnabled || !target.isActiveAndEnabled)
            return false;

        Transform searchRoot = cubeRoot != null ? cubeRoot : transform;
        return target.transform.IsChildOf(searchRoot)
            && Vector2.Distance(GetWorkerPlanePickupPoint(target), GetAcquisitionOrigin(collector))
                <= GetAcquisitionRadius(collector);
    }

    private Vector2 GetAcquisitionOrigin(Object claimant) {
        Transform claimantTransform = claimant is Component component ? component.transform
            : claimant is GameObject claimantObject ? claimantObject.transform : null;
        RobotBodyController body = claimantTransform != null
            ? claimantTransform.GetComponent<RobotBodyController>() : null;
        return body != null && body.BodyReference != null ? (Vector2)body.BodyReference.position
            : claimantTransform != null ? (Vector2)claimantTransform.position
            : approachWaypoint != null ? approachWaypoint.WorldPos : (Vector2)transform.position;
    }

    private float GetAcquisitionRadius(Object claimant) {
        WorkerCollectorBodyController collector = claimant as WorkerCollectorBodyController;
        return collector != null ? Mathf.Min(LocalPickupRadius, collector.AcquisitionRadius) : LocalPickupRadius;
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
