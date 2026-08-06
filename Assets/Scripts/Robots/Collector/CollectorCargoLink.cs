using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns one Collector-specific TargetJoint without touching pickup joints.
/// </summary>
public sealed class CollectorCargoLink : MonoBehaviour
{
    private Rigidbody2D cargoBody;
    private TargetJoint2D ownedJoint;
    private CollectorMissionAssignment assignment;
    private CollectorTargetClaim claim;
    private float secureDwell;
    private float captureDwell;
    private float ownershipDwell;
    private bool active;
    private bool secured;
    private bool recoveryActive;
    private bool releasing;
    private readonly List<CollisionOverride> collisionOverrides = new();
    private readonly List<CollisionOverride> recoveryCollisionOverrides = new();
    private readonly List<Collider2D> recoveryContacts = new();

    public Rigidbody2D Body => active ? cargoBody : null;
    public TargetJoint2D OwnedJoint => ownedJoint;
    public bool IsActive => active && cargoBody != null && ownedJoint != null;
    public bool IsSecured => IsActive && secured;
    public bool IsRecoveryActive => IsActive && recoveryActive;
    public float LastSlotDistance { get; private set; }
    public float LastCargoCenterDistance { get; private set; }
    public int CollisionOverrideCount => collisionOverrides.Count;
    public int RecoveryCollisionOverrideCount => recoveryCollisionOverrides.Count;

    /// <summary>
    /// Creates a new joint owned exclusively by this link.
    /// </summary>
    public bool TakeOwnership(
        Rigidbody2D body,
        CollectorMissionAssignment ownerAssignment,
        Vector2 initialTarget,
        IReadOnlyList<Collider2D> collectorColliders)
    {
        if (active || body == null || ownerAssignment == null || !ownerAssignment.Claim.IsValid)
            return false;
        if (body.gameObject != gameObject)
            return false;

        cargoBody = body;
        assignment = ownerAssignment;
        claim = ownerAssignment.Claim;
        ownedJoint = gameObject.AddComponent<TargetJoint2D>();
        ownedJoint.autoConfigureTarget = false;
        ownedJoint.target = initialTarget;
        ownedJoint.enabled = false;
        active = true;
        secured = false;
        secureDwell = 0f;
        captureDwell = 0f;
        ownershipDwell = 0f;
        recoveryActive = false;
        ApplyCollisionOverrides(collectorColliders);
        return true;
    }

    /// <summary>
    /// Advances the owned attraction joint and secure-state hysteresis.
    /// </summary>
    public void Step(
        CollectorMissionAssignment ownerAssignment,
        Vector2 target,
        Vector2 targetVelocity,
        Vector2 cargoCenter,
        Vector2 cargoCenterVelocity,
        float frequency,
        float dampingRatio,
        float maxForce,
        float secureRadius,
        float escapeRadius,
        float secureSpeed,
        float escapeSpeed,
        float dwellTime,
        float recoveryDelay,
        float recoveryForceMultiplier,
        float deltaTime)
    {
        if (!Owns(ownerAssignment) || ownedJoint == null || cargoBody == null)
            return;

        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        ownershipDwell += safeDeltaTime;
        bool shouldRecover = !secured
            && ownershipDwell >= Mathf.Max(0f, recoveryDelay);
        if (shouldRecover && !recoveryActive)
        {
            recoveryActive = true;
            Debug.Log(
                $"[CollectorCargoRecovery] part={cargoBody.name} "
                + $"slotDistance={Vector2.Distance(cargoBody.worldCenterOfMass, target):0.000} "
                + $"cargoDistance={Vector2.Distance(cargoBody.worldCenterOfMass, cargoCenter):0.000}",
                this);
        }

        ownedJoint.frequency = Mathf.Max(0f, frequency);
        ownedJoint.dampingRatio = Mathf.Clamp01(dampingRatio);
        ownedJoint.maxForce = Mathf.Max(0f, maxForce)
            * (recoveryActive ? Mathf.Max(1f, recoveryForceMultiplier) : 1f);
        ownedJoint.target = target;
        ownedJoint.enabled = true;
        if (recoveryActive)
            ApplyRecoveryContactOverrides();

        LastSlotDistance = Vector2.Distance(cargoBody.worldCenterOfMass, target);
        LastCargoCenterDistance = Vector2.Distance(
            cargoBody.worldCenterOfMass,
            cargoCenter);
        float relativeSlotSpeed = (cargoBody.linearVelocity - targetVelocity).magnitude;
        float relativeCenterSpeed =
            (cargoBody.linearVelocity - cargoCenterVelocity).magnitude;
        float relativeSpeed = Mathf.Min(relativeSlotSpeed, relativeCenterSpeed);
        if (secured)
        {
            if (LastSlotDistance > escapeRadius
                && LastCargoCenterDistance > escapeRadius)
            {
                secured = false;
                secureDwell = 0f;
                captureDwell = 0f;
            }
            else
            {
                RestoreRecoveryCollisionOverrides();
            }
            return;
        }

        bool insideNormalEnvelope = LastSlotDistance <= secureRadius
            || LastCargoCenterDistance <= secureRadius;
        bool insideRecoveryEnvelope = recoveryActive
            && (LastSlotDistance <= escapeRadius
                || LastCargoCenterDistance <= escapeRadius);
        if (insideNormalEnvelope || insideRecoveryEnvelope)
        {
            captureDwell += safeDeltaTime;
            secureDwell = relativeSpeed <= secureSpeed
                ? secureDwell + safeDeltaTime
                : 0f;

            float requiredDwell = Mathf.Max(0f, dwellTime);
            float boundedCaptureDwell = requiredDwell * 3f;
            secured = secureDwell >= requiredDwell
                || captureDwell >= boundedCaptureDwell;
            if (secured)
            {
                recoveryActive = false;
                RestoreRecoveryCollisionOverrides();
            }
        }
        else
        {
            secureDwell = 0f;
            captureDwell = 0f;
        }
    }

    public bool Owns(CollectorMissionAssignment ownerAssignment)
    {
        return active
            && ReferenceEquals(assignment, ownerAssignment)
            && ownerAssignment != null
            && ownerAssignment.Claim == claim;
    }

    /// <summary>
    /// Disables ownership synchronously, then removes only this feature's components.
    /// </summary>
    public void ReleaseOwned()
    {
        if (releasing)
            return;

        releasing = true;
        active = false;
        secured = false;
        secureDwell = 0f;
        captureDwell = 0f;
        ownershipDwell = 0f;
        recoveryActive = false;
        LastSlotDistance = 0f;
        LastCargoCenterDistance = 0f;
        assignment = null;
        claim = default;
        cargoBody = null;

        TargetJoint2D jointToDestroy = ownedJoint;
        ownedJoint = null;
        if (jointToDestroy != null)
            jointToDestroy.enabled = false;

        RestoreCollisionOverrides();
        RestoreRecoveryCollisionOverrides();
        if (jointToDestroy != null)
            DestroyOwned(jointToDestroy);

        DestroyOwned(this);
    }

    private void OnDisable()
    {
        if (ownedJoint != null)
            ownedJoint.enabled = false;
        active = false;
        RestoreCollisionOverrides();
        RestoreRecoveryCollisionOverrides();
    }

    private void OnDestroy()
    {
        if (ownedJoint != null)
            ownedJoint.enabled = false;
        active = false;
        RestoreCollisionOverrides();
        RestoreRecoveryCollisionOverrides();
    }

    private void ApplyCollisionOverrides(IReadOnlyList<Collider2D> collectorColliders)
    {
        collisionOverrides.Clear();
        if (cargoBody == null || collectorColliders == null)
            return;

        Collider2D[] cargoColliders = cargoBody.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cargoColliders.Length; i++)
        {
            Collider2D cargoCollider = cargoColliders[i];
            if (cargoCollider == null
                || cargoCollider.isTrigger
                || cargoCollider.attachedRigidbody != cargoBody)
            {
                continue;
            }

            for (int j = 0; j < collectorColliders.Count; j++)
            {
                Collider2D collectorCollider = collectorColliders[j];
                if (collectorCollider == null
                    || collectorCollider.isTrigger
                    || collectorCollider == cargoCollider)
                {
                    continue;
                }

                bool wasIgnored = Physics2D.GetIgnoreCollision(cargoCollider, collectorCollider);
                collisionOverrides.Add(new CollisionOverride(
                    cargoCollider,
                    collectorCollider,
                    wasIgnored));
                if (!wasIgnored)
                    Physics2D.IgnoreCollision(cargoCollider, collectorCollider, true);
            }
        }
    }

    private void RestoreCollisionOverrides()
    {
        for (int i = 0; i < collisionOverrides.Count; i++)
        {
            CollisionOverride collisionOverride = collisionOverrides[i];
            if (collisionOverride.CargoCollider != null
                && collisionOverride.CollectorCollider != null)
            {
                Physics2D.IgnoreCollision(
                    collisionOverride.CargoCollider,
                    collisionOverride.CollectorCollider,
                    collisionOverride.WasIgnored);
            }
        }

        collisionOverrides.Clear();
    }

    private void ApplyRecoveryContactOverrides()
    {
        if (cargoBody == null)
            return;

        Collider2D[] cargoColliders = cargoBody.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cargoColliders.Length; i++)
        {
            Collider2D cargoCollider = cargoColliders[i];
            if (cargoCollider == null
                || !cargoCollider.enabled
                || cargoCollider.isTrigger
                || cargoCollider.attachedRigidbody != cargoBody)
            {
                continue;
            }

            recoveryContacts.Clear();
            cargoCollider.GetContacts(recoveryContacts);
            for (int j = 0; j < recoveryContacts.Count; j++)
            {
                Collider2D blockingCollider = recoveryContacts[j];
                if (blockingCollider == null
                    || blockingCollider.isTrigger
                    || blockingCollider == cargoCollider
                    || blockingCollider.attachedRigidbody == cargoBody
                    || HasCollisionOverride(
                        recoveryCollisionOverrides,
                        cargoCollider,
                        blockingCollider))
                {
                    continue;
                }

                bool wasIgnored = Physics2D.GetIgnoreCollision(
                    cargoCollider,
                    blockingCollider);
                if (wasIgnored)
                    continue;

                recoveryCollisionOverrides.Add(new CollisionOverride(
                    cargoCollider,
                    blockingCollider,
                    false));
                Physics2D.IgnoreCollision(
                    cargoCollider,
                    blockingCollider,
                    true);
            }
        }

        recoveryContacts.Clear();
    }

    private void RestoreRecoveryCollisionOverrides()
    {
        for (int i = 0; i < recoveryCollisionOverrides.Count; i++)
        {
            CollisionOverride collisionOverride = recoveryCollisionOverrides[i];
            if (collisionOverride.CargoCollider != null
                && collisionOverride.CollectorCollider != null)
            {
                Physics2D.IgnoreCollision(
                    collisionOverride.CargoCollider,
                    collisionOverride.CollectorCollider,
                    collisionOverride.WasIgnored);
            }
        }

        recoveryCollisionOverrides.Clear();
        recoveryContacts.Clear();
    }

    private static bool HasCollisionOverride(
        IReadOnlyList<CollisionOverride> overrides,
        Collider2D first,
        Collider2D second)
    {
        for (int i = 0; i < overrides.Count; i++)
        {
            CollisionOverride collisionOverride = overrides[i];
            bool sameOrder = collisionOverride.CargoCollider == first
                && collisionOverride.CollectorCollider == second;
            bool reverseOrder = collisionOverride.CargoCollider == second
                && collisionOverride.CollectorCollider == first;
            if (sameOrder || reverseOrder)
                return true;
        }

        return false;
    }

    private readonly struct CollisionOverride
    {
        public CollisionOverride(
            Collider2D cargoCollider,
            Collider2D collectorCollider,
            bool wasIgnored)
        {
            CargoCollider = cargoCollider;
            CollectorCollider = collectorCollider;
            WasIgnored = wasIgnored;
        }

        public Collider2D CargoCollider { get; }
        public Collider2D CollectorCollider { get; }
        public bool WasIgnored { get; }
    }

    private static void DestroyOwned(UnityEngine.Object ownedObject)
    {
        if (ownedObject == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(ownedObject);
            return;
        }
#endif
        Destroy(ownedObject);
    }
}
