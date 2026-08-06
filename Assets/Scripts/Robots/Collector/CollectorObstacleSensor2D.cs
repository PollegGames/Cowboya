using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Produces bounded local-avoidance velocity from non-allocating 2D physics queries.
/// Mission selection and path planning deliberately remain outside this component.
/// </summary>
[DisallowMultipleComponent]
public sealed class CollectorObstacleSensor2D : MonoBehaviour {
    private const int QueryCapacity = 32;

    [Header("Query")]
    [SerializeField] private Transform collectorRoot;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private bool includeTriggers;
    [SerializeField, Min(0.01f)] private float forwardProbeDistance = 1.5f;
    [SerializeField, Min(0.01f)] private float forwardProbeRadius = 0.35f;
    [SerializeField, Min(0.01f)] private float sideProbeDistance = 0.9f;
    [SerializeField, Min(0.01f)] private float separationRadius = 0.8f;

    [Header("Response")]
    [SerializeField, Min(0f)] private float forwardAvoidanceSpeed = 3f;
    [SerializeField, Min(0f)] private float separationSpeed = 2f;
    [SerializeField, Min(0f)] private float maximumAvoidanceSpeed = 3.5f;

    private readonly HashSet<int> selfColliderIds = new HashSet<int>();
    private readonly RaycastHit2D[] castHits = new RaycastHit2D[QueryCapacity];
    private readonly Collider2D[] overlapHits = new Collider2D[QueryCapacity];
    private DeadRobotCollectable assignedTarget;
    private Func<Rigidbody2D, bool> ownedBodyPredicate;
    private ContactFilter2D contactFilter;
    private bool filterDirty = true;

    private void Awake() {
        EnsureInitialized();
    }

    private void OnValidate() {
        filterDirty = true;
    }

    /// <summary>
    /// Wires the root used to reject the Collector's own body and magnet colliders.
    /// This is editor-safe and may be called by the prefab builder.
    /// </summary>
    public void ConfigureReferences(Transform root) {
        collectorRoot = root;
        RefreshSelfColliders();
    }

    /// <summary>
    /// Updates the layers queried by the sensor without changing the project collision matrix.
    /// </summary>
    public void SetObstacleMask(LayerMask mask) {
        obstacleMask = mask;
        filterDirty = true;
    }

    /// <summary>
    /// Excludes every collider belonging to the assigned corpse from local avoidance.
    /// </summary>
    public void SetAssignedTarget(DeadRobotCollectable target) {
        assignedTarget = target;
    }

    /// <summary>
    /// Installs a live ownership check so magnet-controlled cargo is not treated as an obstacle.
    /// </summary>
    public void SetOwnedBodyPredicate(Func<Rigidbody2D, bool> predicate) {
        ownedBodyPredicate = predicate;
    }

    /// <summary>
    /// Clears mission-specific corpse and cargo filters while retaining self filtering.
    /// </summary>
    public void ClearMissionFilters() {
        assignedTarget = null;
        ownedBodyPredicate = null;
    }

    /// <summary>
    /// Rebuilds the set of authored Collector colliders that physics queries must ignore.
    /// </summary>
    public void RefreshSelfColliders() {
        selfColliderIds.Clear();
        Transform root = collectorRoot != null ? collectorRoot : transform;
        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++) {
            Collider2D collider = colliders[i];
            if (collider != null)
                selfColliderIds.Add(collider.GetInstanceID());
        }
    }

    /// <summary>
    /// Samples forward, side, and separation probes and returns a bounded velocity adjustment.
    /// </summary>
    public Vector2 SampleAvoidance(
        Vector2 position,
        Vector2 currentVelocity,
        Vector2 directTarget) {
        if (!isActiveAndEnabled)
            return Vector2.zero;

        EnsureInitialized();

        Vector2 travel = currentVelocity.sqrMagnitude > 0.04f
            ? currentVelocity
            : directTarget - position;
        if (travel.sqrMagnitude <= 0.0001f || maximumAvoidanceSpeed <= 0f)
            return Vector2.zero;

        Vector2 forward = travel.normalized;
        Vector2 avoidance = CalculateForwardAvoidance(position, forward);
        avoidance += CalculateSeparation(position);
        return Vector2.ClampMagnitude(avoidance, maximumAvoidanceSpeed);
    }

    /// <summary>
    /// Returns whether a collider is excluded as self, assigned target, or owned cargo.
    /// Exposed for focused deterministic filtering tests.
    /// </summary>
    public bool ShouldIgnore(Collider2D collider) {
        if (collider == null)
            return true;
        if (!includeTriggers && collider.isTrigger)
            return true;
        if (selfColliderIds.Contains(collider.GetInstanceID()))
            return true;

        if (assignedTarget != null
            && collider.transform != null
            && collider.transform.IsChildOf(assignedTarget.transform)) {
            return true;
        }

        Rigidbody2D attachedBody = collider.attachedRigidbody;
        if (attachedBody != null
            && ownedBodyPredicate != null
            && ownedBodyPredicate.Invoke(attachedBody)) {
            return true;
        }

        return false;
    }

    private Vector2 CalculateForwardAvoidance(Vector2 position, Vector2 forward) {
        int count = Physics2D.CircleCast(
            position,
            forwardProbeRadius,
            forward,
            GetContactFilter(),
            castHits,
            forwardProbeDistance);

        if (!TryFindNearestValidHit(count, out RaycastHit2D nearestHit))
            return Vector2.zero;

        float proximity = 1f - Mathf.Clamp01(nearestHit.distance / forwardProbeDistance);
        Vector2 left = new Vector2(-forward.y, forward.x);
        float leftClearance = SampleSideClearance(position, left);
        float rightClearance = SampleSideClearance(position, -left);
        Vector2 clearerSide = leftClearance >= rightClearance ? left : -left;

        Vector2 away = position - nearestHit.point;
        if (away.sqrMagnitude <= 0.0001f)
            away = clearerSide;
        else
            away.Normalize();

        return (clearerSide + away * 0.5f).normalized
            * forwardAvoidanceSpeed
            * proximity;
    }

    private float SampleSideClearance(Vector2 position, Vector2 direction) {
        int count = Physics2D.CircleCast(
            position,
            forwardProbeRadius * 0.75f,
            direction,
            GetContactFilter(),
            castHits,
            sideProbeDistance);

        return TryFindNearestValidHit(count, out RaycastHit2D hit)
            ? Mathf.Clamp(hit.distance, 0f, sideProbeDistance)
            : sideProbeDistance;
    }

    private Vector2 CalculateSeparation(Vector2 position) {
        int count = Physics2D.OverlapCircle(
            position,
            separationRadius,
            GetContactFilter(),
            overlapHits);

        Vector2 separation = Vector2.zero;
        for (int i = 0; i < count; i++) {
            Collider2D collider = overlapHits[i];
            if (ShouldIgnore(collider))
                continue;

            Vector2 closestPoint = collider.ClosestPoint(position);
            Vector2 away = position - closestPoint;
            if (away.sqrMagnitude <= 0.0001f)
                away = position - (Vector2)collider.bounds.center;
            if (away.sqrMagnitude <= 0.0001f)
                continue;

            float distance = Mathf.Max(0.001f, away.magnitude);
            float proximity = 1f - Mathf.Clamp01(distance / separationRadius);
            separation += away / distance * separationSpeed * proximity;
        }

        return Vector2.ClampMagnitude(separation, separationSpeed);
    }

    private bool TryFindNearestValidHit(int count, out RaycastHit2D nearestHit) {
        nearestHit = default;
        float nearestDistance = float.PositiveInfinity;
        bool found = false;

        for (int i = 0; i < count; i++) {
            RaycastHit2D hit = castHits[i];
            if (ShouldIgnore(hit.collider) || hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestHit = hit;
            found = true;
        }

        return found;
    }

    private void EnsureInitialized() {
        if (collectorRoot == null)
            collectorRoot = transform;
        if (selfColliderIds.Count == 0)
            RefreshSelfColliders();
        if (filterDirty)
            RebuildFilter();
    }

    private ContactFilter2D GetContactFilter() {
        if (filterDirty)
            RebuildFilter();
        return contactFilter;
    }

    private void RebuildFilter() {
        contactFilter = new ContactFilter2D();
        contactFilter.SetLayerMask(obstacleMask);
        contactFilter.useTriggers = includeTriggers;
        filterDirty = false;
    }
}
