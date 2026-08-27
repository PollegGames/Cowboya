using System;
using UnityEngine;

public enum WhiteCubeCargoState
{
    Available = 0,
    Claimed = 1,
    Carried = 2,
    Stored = 3,
    HeldExternally = 4
}

/// <summary>
/// Opaque ownership handle for one white-cube claim.
/// </summary>
public readonly struct WhiteCubeClaim : IEquatable<WhiteCubeClaim>
{
    public WhiteCubeClaim(int cubeInstanceId, int claimantInstanceId, int version)
    {
        CubeInstanceId = cubeInstanceId;
        ClaimantInstanceId = claimantInstanceId;
        Version = version;
    }

    public int CubeInstanceId { get; }
    public int ClaimantInstanceId { get; }
    public int Version { get; }
    public bool IsValid => CubeInstanceId != 0 && ClaimantInstanceId != 0 && Version > 0;

    public bool Equals(WhiteCubeClaim other) =>
        CubeInstanceId == other.CubeInstanceId
        && ClaimantInstanceId == other.ClaimantInstanceId
        && Version == other.Version;

    public override bool Equals(object obj) => obj is WhiteCubeClaim other && Equals(other);
    public override int GetHashCode() => (CubeInstanceId, ClaimantInstanceId, Version).GetHashCode();
    public static bool operator ==(WhiteCubeClaim left, WhiteCubeClaim right) => left.Equals(right);
    public static bool operator !=(WhiteCubeClaim left, WhiteCubeClaim right) => !left.Equals(right);
}

/// <summary>
/// Identifies a normal white cube and owns its exclusive Worker Collector claim.
/// Player grabbing always takes precedence over a worker claim.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CubePickup))]
public sealed class WhiteCubeCargo : MonoBehaviour
{
    [SerializeField] private CubePickup pickup;

    private WhiteCubeClaim currentClaim;
    private Transform expectedCarryAnchor;
    private int claimVersion;
    private bool externallyHeld;
    private bool subscribed;

    public event Action<WhiteCubeCargoState> OnStateChanged;
    public event Action<WhiteCubeClaim> OnClaimLost;
    public event Action<WhiteCubeCargo> OnDestroyed;

    public CubePickup Pickup
    {
        get
        {
            ResolvePickup();
            return pickup;
        }
    }
    public WhiteCubeCargoState State { get; private set; } = WhiteCubeCargoState.Available;
    public bool IsAvailable => isActiveAndEnabled && State == WhiteCubeCargoState.Available;
    public WhiteCubeClaim CurrentClaim => currentClaim;

    private void Awake()
    {
        ResolvePickup();
    }

    private void OnEnable()
    {
        EnsureSubscribed();

        if (!currentClaim.IsValid && !externallyHeld)
            SetState(WhiteCubeCargoState.Available);
    }

    private void OnDisable()
    {
        if (pickup != null)
        {
            pickup.OnGrabbed -= HandleGrabbed;
            pickup.OnReleased -= HandleReleased;
        }
        subscribed = false;

        LoseClaim();
    }

    private void OnDestroy()
    {
        OnDestroyed?.Invoke(this);
    }

    /// <summary>
    /// Atomically claims this loose cube for one owner.
    /// </summary>
    public bool TryClaim(UnityEngine.Object claimant, out WhiteCubeClaim claim)
    {
        claim = default;
        EnsureSubscribed();
        if (claimant == null || !IsAvailable)
            return false;

        claimVersion++;
        currentClaim = new WhiteCubeClaim(gameObject.GetInstanceID(), claimant.GetInstanceID(), claimVersion);
        claim = currentClaim;
        SetState(WhiteCubeCargoState.Claimed);
        return true;
    }

    /// <summary>
    /// Validates that the supplied handle still owns this cube.
    /// </summary>
    public bool IsClaimValid(WhiteCubeClaim claim) => claim.IsValid && claim == currentClaim;

    /// <summary>
    /// Marks the expected worker carry anchor before CubePickup performs its normal grab.
    /// </summary>
    public bool TryBeginCarry(WhiteCubeClaim claim, Transform carryAnchor)
    {
        EnsureSubscribed();
        if (!IsClaimValid(claim) || carryAnchor == null || pickup == null)
            return false;

        expectedCarryAnchor = carryAnchor;
        externallyHeld = false;
        SetState(WhiteCubeCargoState.Carried);
        pickup.OnGrab(carryAnchor);

        if (pickup == null || pickup.transform.parent != carryAnchor)
        {
            LoseClaim();
            return false;
        }

        return IsClaimValid(claim) && State == WhiteCubeCargoState.Carried;
    }

    /// <summary>
    /// Releases a matching worker claim without affecting player ownership.
    /// </summary>
    public bool ReleaseClaim(WhiteCubeClaim claim)
    {
        if (!IsClaimValid(claim))
            return false;

        LoseClaim();
        if (!externallyHeld)
            SetState(WhiteCubeCargoState.Available);
        return true;
    }

    /// <summary>
    /// Transfers a worker-owned cube into an authoritative garage slot.
    /// </summary>
    public bool TryStore(WhiteCubeClaim claim, Transform slot)
    {
        EnsureSubscribed();
        if (!IsClaimValid(claim) || slot == null || pickup == null)
            return false;

        pickup.OnRelease(Vector2.zero);
        currentClaim = default;
        expectedCarryAnchor = null;
        externallyHeld = false;
        transform.SetParent(slot, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.simulated = true;
        }

        SetState(WhiteCubeCargoState.Stored);
        return true;
    }

    /// <summary>
    /// Restores dynamic physics after a stored cube leaves its garage slot.
    /// </summary>
    public void RestoreLoosePhysics()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Dynamic;
            body.simulated = true;
        }
    }

    private void HandleGrabbed(CubePickup grabbed)
    {
        if (grabbed == null)
            return;

        if (currentClaim.IsValid
            && expectedCarryAnchor != null
            && grabbed.transform.parent == expectedCarryAnchor)
        {
            externallyHeld = false;
            SetState(WhiteCubeCargoState.Carried);
            return;
        }

        externallyHeld = true;
        RestoreLoosePhysics();
        LoseClaim();
        SetState(WhiteCubeCargoState.HeldExternally);
    }

    private void HandleReleased(CubePickup released)
    {
        _ = released;
        externallyHeld = false;
        expectedCarryAnchor = null;
        if (!currentClaim.IsValid)
            SetState(WhiteCubeCargoState.Available);
    }

    private void LoseClaim()
    {
        if (!currentClaim.IsValid)
            return;

        WhiteCubeClaim lost = currentClaim;
        currentClaim = default;
        expectedCarryAnchor = null;
        OnClaimLost?.Invoke(lost);
    }

    private void SetState(WhiteCubeCargoState next)
    {
        if (State == next)
            return;
        State = next;
        OnStateChanged?.Invoke(next);
    }

    private void ResolvePickup()
    {
        if (pickup == null)
            pickup = GetComponent<CubePickup>();
    }

    private void EnsureSubscribed()
    {
        ResolvePickup();
        if (subscribed || pickup == null)
            return;
        pickup.OnGrabbed += HandleGrabbed;
        pickup.OnReleased += HandleReleased;
        subscribed = true;
    }
}
