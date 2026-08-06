using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Discrete cargo observation produced by the Collector magnet.
/// </summary>
public readonly struct CollectorCargoStatus
{
    public CollectorCargoStatus(
        CollectorMissionAssignment assignment,
        int requiredPartCount,
        int securedPartCount,
        bool cargoSecure,
        bool cargoLost)
    {
        Assignment = assignment;
        RequiredPartCount = requiredPartCount;
        SecuredPartCount = securedPartCount;
        CargoSecure = cargoSecure;
        CargoLost = cargoLost;
    }

    public CollectorMissionAssignment Assignment { get; }
    public int RequiredPartCount { get; }
    public int SecuredPartCount { get; }
    public bool CargoSecure { get; }
    public bool CargoLost { get; }
}

/// <summary>
/// Attracts only the required parts of one claimed corpse with owned TargetJoints.
/// </summary>
public sealed class CollectorMagnetController2D : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody2D collectorBody;
    [SerializeField] private Rigidbody2D magnetBody;

    [Header("Attraction")]
    [SerializeField, Min(0f)] private float gatherFrequency = 6f;
    [SerializeField, Min(0f)] private float carryFrequency = 9f;
    [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.9f;
    [SerializeField, Min(0f)] private float maxForcePerPart = 90f;

    [Header("Cargo Slots")]
    [SerializeField, Min(0.01f)] private float slotSpacing = 0.32f;
    [SerializeField, Min(0f)] private float firstRowOffset = 0.3f;
    [SerializeField, Min(1)] private int maximumColumns = 4;

    [Header("Secure Observation")]
    [SerializeField, Min(0.01f)] private float secureRadius = 2f;
    [SerializeField, Min(0.01f)] private float escapeRadius = 3f;
    [SerializeField, Min(0f)] private float secureRelativeSpeed = 0.65f;
    [SerializeField, Min(0f)] private float escapeRelativeSpeed = 1.1f;
    [SerializeField, Min(0f)] private float secureDwellTime = 0.45f;

    [Header("Stuck Part Recovery")]
    [SerializeField, Min(0f)] private float stuckPartRecoveryDelay = 3f;
    [SerializeField, Min(1f)] private float stuckPartForceMultiplier = 4f;

    private readonly Dictionary<Rigidbody2D, CollectorCargoLink> links = new();
    private readonly HashSet<Rigidbody2D> collectorBodies = new();
    private readonly List<Collider2D> collectorColliders = new();
    private readonly HashSet<Collider2D> collectorColliderSet = new();
    private readonly HashSet<Rigidbody2D> requiredSet = new();
    private readonly List<Rigidbody2D> removalBuffer = new();
    private CollectorMissionAssignment assignment;
    private bool gathering;
    private bool hadSecureCargo;
    private bool cargoLost;
    private int lastRequiredCount = -1;
    private int lastSecuredCount = -1;
    private bool lastCargoSecure;
    private bool lastCargoLost;

    public event Action<CollectorCargoStatus> CargoStatusChanged;

    public CollectorMissionAssignment Assignment => assignment;
    public bool IsGathering => gathering;
    public int OwnedPartCount => links.Count;

    private void Awake()
    {
        CacheCollectorBodies();
        NormalizeSettings();
    }

    private void OnValidate()
    {
        NormalizeSettings();
    }

    private void OnDisable()
    {
        ReleaseAll();
    }

    /// <summary>
    /// Wires the two approved Collector puppet bodies without hierarchy guessing.
    /// </summary>
    public void ConfigureReferences(Rigidbody2D body, Rigidbody2D magnet)
    {
        collectorBody = body;
        magnetBody = magnet;
        CacheCollectorBodies();
    }

    /// <summary>
    /// Starts or resumes acquisition for the mission's required parts.
    /// </summary>
    public void BeginGathering(CollectorMissionAssignment ownerAssignment)
    {
        if (!CanUse(ownerAssignment))
        {
            ReleaseAll();
            return;
        }

        SetAssignment(ownerAssignment);
        gathering = true;
    }

    /// <summary>
    /// Retains already-owned parts without acquiring unrelated bodies.
    /// </summary>
    public void BeginCarry(CollectorMissionAssignment ownerAssignment)
    {
        if (!CanUse(ownerAssignment))
        {
            ReleaseAll();
            return;
        }

        SetAssignment(ownerAssignment);
        gathering = false;
    }

    /// <summary>
    /// Stops new acquisition while preserving current cargo links.
    /// </summary>
    public void StopAcquisition()
    {
        gathering = false;
    }

    /// <summary>
    /// Advances attraction and emits only changed discrete cargo observations.
    /// </summary>
    public void StepPhysics(float deltaTime)
    {
        if (!CanUse(assignment))
        {
            ReleaseAll();
            return;
        }

        IReadOnlyList<Rigidbody2D> requiredParts =
            assignment.Target.GetRequiredParts(assignment.Claim);
        if (!assignment.Target.IsClaimValid(assignment.Claim) || requiredParts.Count == 0)
        {
            ReleaseAll();
            return;
        }

        SynchronizeLinks(requiredParts);
        int securedCount = 0;
        float frequency = gathering ? gatherFrequency : carryFrequency;
        Vector2 cargoCenter = magnetBody != null
            ? magnetBody.position
            : collectorBody != null
                ? collectorBody.position
                : (Vector2)transform.position;
        Vector2 cargoCenterVelocity = magnetBody != null
            ? magnetBody.GetPointVelocity(cargoCenter)
            : collectorBody != null
                ? collectorBody.GetPointVelocity(cargoCenter)
                : Vector2.zero;

        for (int i = 0; i < requiredParts.Count; i++)
        {
            Rigidbody2D body = requiredParts[i];
            if (body == null || !links.TryGetValue(body, out CollectorCargoLink link) || link == null)
                continue;

            Vector2 slot = GetSlotWorldPosition(i, requiredParts.Count);
            Vector2 slotVelocity = magnetBody != null
                ? magnetBody.GetPointVelocity(slot)
                : Vector2.zero;
            link.Step(
                assignment,
                slot,
                slotVelocity,
                cargoCenter,
                cargoCenterVelocity,
                frequency,
                dampingRatio,
                maxForcePerPart,
                secureRadius,
                escapeRadius,
                secureRelativeSpeed,
                escapeRelativeSpeed,
                secureDwellTime,
                stuckPartRecoveryDelay,
                stuckPartForceMultiplier,
                deltaTime);

            if (link.IsSecured)
                securedCount++;
        }

        int requiredCount = requiredParts.Count;
        bool cargoSecure = requiredCount > 0 && securedCount == requiredCount;
        if (cargoSecure)
        {
            hadSecureCargo = true;
            cargoLost = false;
        }
        else if (hadSecureCargo)
        {
            cargoLost = true;
        }

        PublishIfChanged(requiredCount, securedCount, cargoSecure, cargoLost);
    }

    /// <summary>
    /// Returns whether this controller currently owns the body's attraction link.
    /// </summary>
    public bool Owns(Rigidbody2D body)
    {
        return body != null
            && links.TryGetValue(body, out CollectorCargoLink link)
            && link != null
            && link.Owns(assignment);
    }

    /// <summary>
    /// Gets the live centre of required parts that are not currently secured.
    /// </summary>
    public bool TryGetUnsecuredCenter(
        CollectorMissionAssignment ownerAssignment,
        out Vector2 center)
    {
        center = Vector2.zero;
        if (!ReferenceEquals(assignment, ownerAssignment) || !CanUse(ownerAssignment))
            return false;

        IReadOnlyList<Rigidbody2D> parts =
            ownerAssignment.Target.GetRequiredParts(ownerAssignment.Claim);
        int count = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            Rigidbody2D body = parts[i];
            if (body == null)
                continue;
            if (links.TryGetValue(body, out CollectorCargoLink link)
                && link != null
                && link.IsSecured)
            {
                continue;
            }

            center += body.worldCenterOfMass;
            count++;
        }

        if (count == 0)
            return false;

        center /= count;
        return true;
    }

    /// <summary>
    /// Synchronously disables and invalidates every owned link.
    /// </summary>
    public void ReleaseAll()
    {
        foreach (CollectorCargoLink link in links.Values)
        {
            if (link != null)
                link.ReleaseOwned();
        }

        links.Clear();
        requiredSet.Clear();
        removalBuffer.Clear();
        assignment = null;
        gathering = false;
        hadSecureCargo = false;
        cargoLost = false;
        lastRequiredCount = -1;
        lastSecuredCount = -1;
        lastCargoSecure = false;
        lastCargoLost = false;
    }

    private void SetAssignment(CollectorMissionAssignment ownerAssignment)
    {
        if (ReferenceEquals(assignment, ownerAssignment))
            return;

        ReleaseAll();
        assignment = ownerAssignment;
    }

    private void SynchronizeLinks(IReadOnlyList<Rigidbody2D> requiredParts)
    {
        requiredSet.Clear();
        for (int i = 0; i < requiredParts.Count; i++)
        {
            Rigidbody2D body = requiredParts[i];
            if (body != null && !collectorBodies.Contains(body))
                requiredSet.Add(body);
        }

        removalBuffer.Clear();
        foreach (KeyValuePair<Rigidbody2D, CollectorCargoLink> pair in links)
        {
            if (pair.Key == null
                || !requiredSet.Contains(pair.Key)
                || pair.Value == null
                || !pair.Value.Owns(assignment))
            {
                if (pair.Value != null)
                    pair.Value.ReleaseOwned();
                removalBuffer.Add(pair.Key);
            }
        }

        for (int i = 0; i < removalBuffer.Count; i++)
            links.Remove(removalBuffer[i]);

        if (!gathering)
            return;

        for (int i = 0; i < requiredParts.Count; i++)
        {
            Rigidbody2D body = requiredParts[i];
            if (body == null || collectorBodies.Contains(body) || links.ContainsKey(body))
                continue;

            CollectorCargoLink link = body.gameObject.AddComponent<CollectorCargoLink>();
            if (link.TakeOwnership(
                body,
                assignment,
                GetSlotWorldPosition(i, requiredParts.Count),
                collectorColliders))
                links.Add(body, link);
            else
                DestroyComponent(link);
        }
    }

    private Vector2 GetSlotWorldPosition(int index, int count)
    {
        int columns = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, count))),
            1,
            Mathf.Max(1, maximumColumns));
        int row = index / columns;
        int column = index % columns;
        float width = Mathf.Min(columns, count - row * columns);
        float localX = (column - (width - 1f) * 0.5f) * slotSpacing;
        float localY = -firstRowOffset - row * slotSpacing;
        Vector2 slotOrigin = magnetBody != null
            ? magnetBody.position
            : (Vector2)transform.position;
        float slotRotation = magnetBody != null
            ? magnetBody.rotation
            : transform.eulerAngles.z;
        Vector2 worldOffset = Quaternion.Euler(0f, 0f, slotRotation)
            * new Vector2(localX, localY);
        return slotOrigin + worldOffset;
    }

    private void PublishIfChanged(
        int requiredCount,
        int securedCount,
        bool cargoSecure,
        bool isCargoLost)
    {
        if (requiredCount == lastRequiredCount
            && securedCount == lastSecuredCount
            && cargoSecure == lastCargoSecure
            && isCargoLost == lastCargoLost)
        {
            return;
        }

        lastRequiredCount = requiredCount;
        lastSecuredCount = securedCount;
        lastCargoSecure = cargoSecure;
        lastCargoLost = isCargoLost;
        Debug.Log(
            $"[CollectorCargo] robot={name} mode={(gathering ? "gather" : "carry")} "
            + $"secured={securedCount}/{requiredCount} secure={cargoSecure} lost={isCargoLost}",
            this);
        CargoStatusChanged?.Invoke(new CollectorCargoStatus(
            assignment,
            requiredCount,
            securedCount,
            cargoSecure,
            isCargoLost));
    }

    private bool CanUse(CollectorMissionAssignment ownerAssignment)
    {
        return ownerAssignment != null
            && ownerAssignment.Target != null
            && ownerAssignment.Claim.IsValid
            && ownerAssignment.Target.IsClaimValid(ownerAssignment.Claim);
    }

    private void CacheCollectorBodies()
    {
        collectorBodies.Clear();
        if (collectorBody != null)
            collectorBodies.Add(collectorBody);
        if (magnetBody != null)
            collectorBodies.Add(magnetBody);

        collectorColliders.Clear();
        collectorColliderSet.Clear();
        CachePhysicalColliders(collectorBody);
        CachePhysicalColliders(magnetBody);
    }

    private void CachePhysicalColliders(Rigidbody2D body)
    {
        if (body == null)
            return;

        Collider2D[] colliders = body.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null
                && !collider.isTrigger
                && collider.attachedRigidbody == body
                && collectorColliderSet.Add(collider))
            {
                collectorColliders.Add(collider);
            }
        }
    }

    private void NormalizeSettings()
    {
        escapeRadius = Mathf.Max(secureRadius, escapeRadius);
        escapeRelativeSpeed = Mathf.Max(secureRelativeSpeed, escapeRelativeSpeed);
        stuckPartForceMultiplier = Mathf.Max(1f, stuckPartForceMultiplier);
        maximumColumns = Mathf.Max(1, maximumColumns);
    }

    private static void DestroyComponent(Component component)
    {
        if (component == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(component);
            return;
        }
#endif
        Destroy(component);
    }
}
