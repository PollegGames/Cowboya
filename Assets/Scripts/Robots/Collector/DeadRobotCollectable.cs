using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the claim and physical-part contract for one dead robot lifecycle.
/// </summary>
public sealed class DeadRobotCollectable : MonoBehaviour, IPooledObject
{
    private const string SecurityBadgeTag = "BadgeSecurity";
    private static readonly Rigidbody2D[] EmptyParts = Array.Empty<Rigidbody2D>();

    [SerializeField] private RobotStateController stateController;

    private readonly List<Rigidbody2D> requiredParts = new();
    private UnityEngine.Object claimOwner;
    private CollectorTargetClaim activeClaim;
    private int targetGeneration;
    private int claimVersion;
    private bool generationPrepared;
    private bool collectible;
    private bool completed;
    private bool subscribed;

    public event Action<CollectorTargetClaim> OnInvalidated;
    public event Action<CollectorTargetClaim> OnClaimLost;
    public event Action<CollectorTargetClaim> OnRequiredPartsChanged;

    public int TargetGeneration => targetGeneration;
    public int RequiredPartCount
    {
        get
        {
            PruneMissingParts();
            return requiredParts.Count;
        }
    }
    public bool IsCollectible
    {
        get
        {
            if (collectible)
                PruneMissingParts();
            return collectible && !completed;
        }
    }
    public bool IsCompleted => completed;
    public bool HasActiveClaim => IsClaimValid(activeClaim);
    public CollectorTargetClaim ActiveClaim => HasActiveClaim ? activeClaim : default;

    /// <summary>
    /// Gets or safely adds the collectible contract to a robot root.
    /// </summary>
    public static DeadRobotCollectable EnsureFor(RobotStateController state)
    {
        if (state == null)
            return null;

        DeadRobotCollectable collectable = state.GetComponent<DeadRobotCollectable>();
        if (collectable == null)
            collectable = state.gameObject.AddComponent<DeadRobotCollectable>();

        collectable.stateController = state;
        collectable.EnsureSubscribed();
        collectable.ReconcileState();
        return collectable;
    }

    private void Awake()
    {
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();
    }

    private void OnEnable()
    {
        EnsureSubscribed();
        ReconcileState();
    }

    private void OnDisable()
    {
        Unsubscribe();
        InvalidateLifecycle();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        InvalidateLifecycle();
    }

    /// <summary>
    /// Atomically claims the current dead lifecycle for one mission owner.
    /// </summary>
    public bool TryClaim(
        int missionId,
        UnityEngine.Object owner,
        out CollectorTargetClaim claim)
    {
        claim = default;
        if (missionId <= 0 || !IsClaimOwnerAlive(owner) || !collectible || completed)
            return false;

        ReleaseInvalidOwnerClaim();
        if (activeClaim.IsValid)
            return false;

        CacheRequiredParts();
        if (requiredParts.Count == 0)
        {
            collectible = false;
            return false;
        }

        claimVersion = NextPositive(claimVersion);
        activeClaim = new CollectorTargetClaim(GetInstanceID(), targetGeneration, claimVersion);
        claimOwner = owner;
        claim = activeClaim;
        return true;
    }

    /// <summary>
    /// Returns whether a claim still owns this exact target generation.
    /// </summary>
    public bool IsClaimValid(CollectorTargetClaim claim)
    {
        if (!claim.IsValid || !collectible || completed)
            return false;

        if (!IsClaimOwnerAlive(claimOwner))
        {
            ReleaseInvalidOwnerClaim();
            return false;
        }

        return claim == activeClaim
            && claim.TargetInstanceId == GetInstanceID()
            && claim.TargetGeneration == targetGeneration;
    }

    /// <summary>
    /// Releases a matching claim without invalidating the corpse lifecycle.
    /// </summary>
    public void ReleaseClaim(CollectorTargetClaim claim)
    {
        if (claim != activeClaim || !activeClaim.IsValid)
            return;

        CollectorTargetClaim releasedClaim = activeClaim;
        activeClaim = default;
        claimOwner = null;
        OnClaimLost?.Invoke(releasedClaim);
    }

    /// <summary>
    /// Gets the current non-loot dynamic parts for a valid claim.
    /// </summary>
    public IReadOnlyList<Rigidbody2D> GetRequiredParts(CollectorTargetClaim claim)
    {
        if (!IsClaimValid(claim))
            return EmptyParts;

        PruneMissingParts();
        return IsClaimValid(claim) ? requiredParts : EmptyParts;
    }

    /// <summary>
    /// Calculates a live centre from the physical parts instead of the corpse root.
    /// </summary>
    public Vector2 GetLiveCollectionCenter(CollectorTargetClaim claim)
    {
        IReadOnlyList<Rigidbody2D> parts = GetRequiredParts(claim);
        if (parts.Count == 0)
            return transform.position;

        Vector2 total = Vector2.zero;
        for (int i = 0; i < parts.Count; i++)
            total += parts[i].worldCenterOfMass;

        return total / parts.Count;
    }

    /// <summary>
    /// Returns whether every current required part centre is inside the intake collider.
    /// </summary>
    public bool AreAllRequiredPartsInside(Collider2D intake, CollectorTargetClaim claim)
    {
        if (intake == null)
            return false;

        IReadOnlyList<Rigidbody2D> parts = GetRequiredParts(claim);
        if (parts.Count == 0)
            return false;

        for (int i = 0; i < parts.Count; i++)
        {
            if (!intake.OverlapPoint(parts[i].worldCenterOfMass))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns whether every required part is inside, or within a controlled margin of,
    /// the machine intake. The margin accommodates the physical cargo rack surrounding
    /// a Collector while still rejecting pieces left behind in the room.
    /// </summary>
    public bool AreAllRequiredPartsWithinIntake(
        Collider2D intake,
        CollectorTargetClaim claim,
        float margin)
    {
        if (intake == null)
            return false;

        IReadOnlyList<Rigidbody2D> parts = GetRequiredParts(claim);
        if (parts.Count == 0)
            return false;

        float safeMargin = Mathf.Max(0f, margin);
        float maximumDistanceSquared = safeMargin * safeMargin;
        for (int i = 0; i < parts.Count; i++)
        {
            Vector2 center = parts[i].worldCenterOfMass;
            if (intake.OverlapPoint(center))
                continue;

            Vector2 closestPoint = intake.ClosestPoint(center);
            if ((center - closestPoint).sqrMagnitude > maximumDistanceSquared)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Marks a matching collection complete and detaches excluded loot.
    /// The owning machine remains responsible for the final pool release.
    /// </summary>
    public bool CompleteCollection(CollectorTargetClaim claim)
    {
        if (!IsClaimValid(claim) || requiredParts.Count == 0)
            return false;

        DetachExcludedLoot();
        CollectorTargetClaim completedClaim = activeClaim;
        completed = true;
        collectible = false;
        activeClaim = default;
        claimOwner = null;
        OnInvalidated?.Invoke(completedClaim);
        return true;
    }

    public void OnAcquireFromPool()
    {
        InvalidateLifecycle();
        PrepareGeneration();
        subscribed = false;
        EnsureSubscribed();
    }

    public void OnReleaseToPool()
    {
        InvalidateLifecycle();
        OnInvalidated = null;
        OnClaimLost = null;
        OnRequiredPartsChanged = null;
    }

    private void EnsureSubscribed()
    {
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();
        if (stateController == null)
            return;

        stateController.OnStateChanged -= HandleRobotStateChanged;
        stateController.OnStateChanged += HandleRobotStateChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (stateController != null)
            stateController.OnStateChanged -= HandleRobotStateChanged;
        subscribed = false;
    }

    private void HandleRobotStateChanged(RobotState state)
    {
        if (state == RobotState.Dead)
            ActivateDeadLifecycle();
        else
            InvalidateLifecycle();
    }

    private void ReconcileState()
    {
        if (stateController == null)
            return;

        if (stateController.CurrentState == RobotState.Dead)
            ActivateDeadLifecycle();
        else if (collectible)
            InvalidateLifecycle();
    }

    private void ActivateDeadLifecycle()
    {
        if (collectible && !completed)
            return;

        if (IsCollectorRobot())
        {
            InvalidateLifecycle();
            return;
        }

        if (!generationPrepared)
            targetGeneration = NextPositive(targetGeneration);

        generationPrepared = false;
        completed = false;
        collectible = true;
        activeClaim = default;
        claimOwner = null;
        CacheRequiredParts();
        if (requiredParts.Count == 0)
            collectible = false;
    }

    private bool IsCollectorRobot()
    {
        RobotHeartNew heart = GetComponent<RobotHeartNew>();
        return heart != null && heart.Role == RobotRole.Collector;
    }

    private void PrepareGeneration()
    {
        targetGeneration = NextPositive(targetGeneration);
        generationPrepared = true;
        completed = false;
        collectible = false;
        requiredParts.Clear();
    }

    private void InvalidateLifecycle()
    {
        CollectorTargetClaim invalidatedClaim = activeClaim;
        bool hadClaim = invalidatedClaim.IsValid;
        bool wasCollectible = collectible;
        activeClaim = default;
        claimOwner = null;
        collectible = false;
        completed = false;
        requiredParts.Clear();

        if (wasCollectible || hadClaim)
            OnInvalidated?.Invoke(invalidatedClaim);
    }

    private void ReleaseInvalidOwnerClaim()
    {
        if (!activeClaim.IsValid || IsClaimOwnerAlive(claimOwner))
            return;

        CollectorTargetClaim releasedClaim = activeClaim;
        activeClaim = default;
        claimOwner = null;
        OnClaimLost?.Invoke(releasedClaim);
    }

    private static bool IsClaimOwnerAlive(UnityEngine.Object owner)
    {
        if (owner == null)
            return false;
        if (owner is Behaviour behaviour)
            return behaviour.isActiveAndEnabled;
        if (owner is Component component)
            return component.gameObject.activeInHierarchy;
        if (owner is GameObject gameObject)
            return gameObject.activeInHierarchy;
        return true;
    }

    private void CacheRequiredParts()
    {
        List<Rigidbody2D> previousParts = new(requiredParts);
        requiredParts.Clear();

        Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D body = bodies[i];
            if (IsEligiblePart(body))
                requiredParts.Add(body);
        }

        if (!HaveSameParts(previousParts, requiredParts) && activeClaim.IsValid)
            OnRequiredPartsChanged?.Invoke(activeClaim);
    }

    private void PruneMissingParts()
    {
        bool changed = false;
        for (int i = requiredParts.Count - 1; i >= 0; i--)
        {
            if (requiredParts[i] == null)
            {
                requiredParts.RemoveAt(i);
                changed = true;
            }
        }

        if (!changed)
            return;

        if (activeClaim.IsValid)
        {
            CollectorTargetClaim claim = activeClaim;
            OnRequiredPartsChanged?.Invoke(claim);
        }

        if (requiredParts.Count == 0 && collectible)
            InvalidateLifecycle();
    }

    private bool IsEligiblePart(Rigidbody2D body)
    {
        if (body == null || body.bodyType != RigidbodyType2D.Dynamic || !body.simulated)
            return false;
        if (HasExcludedAncestor(body.transform))
            return false;

        Collider2D[] colliders = body.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null
                && collider.enabled
                && !collider.isTrigger
                && collider.attachedRigidbody == body)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasExcludedAncestor(Transform part)
    {
        Transform current = part;
        while (current != null)
        {
            if (current.GetComponent<SecurityBadgePickup>() != null
                || current.GetComponent<CollectorCargoExclusion>() != null
                || IsTaggedSecurityBadge(current.gameObject))
            {
                return true;
            }

            if (current == transform)
                break;
            current = current.parent;
        }

        return false;
    }

    private void DetachExcludedLoot()
    {
        SecurityBadgePickup[] badges = GetComponentsInChildren<SecurityBadgePickup>(true);
        Inventory[] inventories = GetComponentsInChildren<Inventory>(true);
        for (int i = 0; i < badges.Length; i++)
        {
            SecurityBadgePickup badge = badges[i];
            if (badge == null)
                continue;

            for (int j = 0; j < inventories.Length; j++)
            {
                Inventory inventory = inventories[j];
                if (inventory != null
                    && ReferenceEquals(inventory.GetItem(PickupType.SecurityBadge), badge))
                {
                    inventory.RemoveItem(PickupType.SecurityBadge);
                }
            }

            badge.OnRelease(Vector2.zero);
        }

        CollectorCargoExclusion[] exclusions =
            GetComponentsInChildren<CollectorCargoExclusion>(true);
        for (int i = 0; i < exclusions.Length; i++)
        {
            CollectorCargoExclusion exclusion = exclusions[i];
            if (exclusion != null
                && exclusion.DetachOnCollection
                && exclusion.transform != transform)
            {
                exclusion.transform.SetParent(null, true);
            }
        }

        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform descendant = descendants[i];
            if (descendant == null
                || descendant == transform
                || !IsTaggedSecurityBadge(descendant.gameObject))
            {
                continue;
            }

            Joint2D[] badgeJoints = descendant.GetComponents<Joint2D>();
            for (int j = 0; j < badgeJoints.Length; j++)
            {
                if (badgeJoints[j] != null)
                    badgeJoints[j].enabled = false;
            }

            descendant.SetParent(null, true);
        }
    }

    private static bool IsTaggedSecurityBadge(GameObject candidate)
    {
        return candidate != null && candidate.tag == SecurityBadgeTag;
    }

    private static bool HaveSameParts(
        IReadOnlyList<Rigidbody2D> first,
        IReadOnlyList<Rigidbody2D> second)
    {
        if (first.Count != second.Count)
            return false;

        for (int i = 0; i < first.Count; i++)
        {
            if (first[i] != second[i])
                return false;
        }

        return true;
    }

    private static int NextPositive(int value)
    {
        return value == int.MaxValue ? 1 : value + 1;
    }
}
