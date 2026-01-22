using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class SecurityBadgePickup : MonoBehaviour, IGrabbable
{
    [Header("Throw settings")]
    public float throwStrength = 5f;

    [Header("Attachment")]
    [Tooltip("Controls how quickly the badge eases toward its follow target when attached.")]
    [SerializeField, Range(1f, 40f)] private float followLerpSpeed = 20f;

    [Header("Visuals")]
    [SerializeField, Tooltip("Sorting order applied while the badge is held.")] private int heldSortingOrder = 20;
    [SerializeField, Tooltip("Sorting order applied when the badge is idle.")] private int idleSortingOrder = 0;

    [Header("Target Joint Settings")]
    [Tooltip("How springy the joint movement is. Recommended range: 5–15.")]
    [SerializeField, Range(5f, 15f)] private float frequency = 10f;
    [Tooltip("How much the joint resists oscillation. Recommended range: 0–1.")]
    [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.9f;
    [Tooltip("Maximum force the joint can apply. Recommended range: 500–3000.")]
    [SerializeField, Range(500f, 3000f)] private float maxForce = 2000f;

    /// <summary>
    /// How springy the joint movement is.
    /// </summary>
    public float Frequency
    {
        get => frequency;
        set
        {
            frequency = value;
            if (joint != null)
                joint.frequency = frequency;
        }
    }

    /// <summary>
    /// How much the joint resists oscillation.
    /// </summary>
    public float DampingRatio
    {
        get => dampingRatio;
        set
        {
            dampingRatio = value;
            if (joint != null)
                joint.dampingRatio = dampingRatio;
        }
    }

    /// <summary>
    /// Maximum force the joint can apply.
    /// </summary>
    public float MaxForce
    {
        get => maxForce;
        set
        {
            maxForce = value;
            if (joint != null)
                joint.maxForce = maxForce;
        }
    }

    Rigidbody2D rb;
    TargetJoint2D joint;
    Transform followTarget;
    bool attached = false;
    RigidbodyType2D originalBodyType;
    float originalGravityScale;

    // Flag to ensure stolen logic only runs once
    bool wasStolen = false;

    Inventory ownerInventory;
    SpriteRenderer[] spriteRenderers;

    void CacheOriginalPhysicsState()
    {
        if (rb == null)
            return;

        originalBodyType = rb.bodyType;
        originalGravityScale = rb.gravityScale;
    }


    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        CacheOriginalPhysicsState();
        joint = GetComponent<TargetJoint2D>();
        CacheSpriteRenderers();
        ApplySortingOrder(idleSortingOrder);
        if (joint != null)
        {
            // Start disabled — only enable when grabbed
            joint.enabled = false;

            // Configure joint behavior
            joint.autoConfigureTarget = false;
            joint.target = rb.position;
            joint.frequency = frequency;
            joint.dampingRatio = dampingRatio;
            joint.maxForce = maxForce;
        }
    }

    void FixedUpdate()
    {
        if (joint == null || !joint.enabled || followTarget == null)
            return;

        joint.target = followTarget.position;

        if (attached && rb != null)
        {
            Vector2 desired = joint.target;
            Vector2 next = Vector2.Lerp(rb.position, desired, followLerpSpeed * Time.fixedDeltaTime);
            rb.MovePosition(next);
        }
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        // Ensure we have a joint reference. This can be null if the badge
        // prefab didn't include a TargetJoint2D and the component was added
        // after Awake ran.
        if (joint == null)
            joint = GetComponent<TargetJoint2D>();

        if (joint != null)
        {
            if (followTarget != null)
                joint.target = followTarget.position;

            joint.enabled = true;
        }
        else
        {
            Debug.LogWarning($"{nameof(SecurityBadgePickup)} on {name} is missing a {nameof(TargetJoint2D)} component.");
        }
    }

    public bool CanBeGrabbed(Inventory inventory)
    {
        if (inventory != null)
        {
            var held = inventory.GetItem(PickupType.SecurityBadge);
            if (held != null && (object)held != this)
                return false;
        }
        return true;
    }

    public void OnGrab(Transform grabParent)
    {
        if (grabParent == null)
        {
            Debug.LogWarning($"{nameof(SecurityBadgePickup)} received a null grab parent.");
            return;
        }

        var inventory = grabParent.GetComponentInParent<Inventory>();
        if (inventory == null)
            inventory = grabParent.GetComponent<Inventory>();
        if (inventory == null && grabParent.root != null)
            inventory = grabParent.root.GetComponentInChildren<Inventory>();
        if (inventory == null)
        {
            var candidates = UnityEngine.Object.FindObjectsByType<Inventory>(FindObjectsSortMode.None);
            if (candidates != null && candidates.Length > 0)
                inventory = candidates[0];
        }
        var player = grabParent.GetComponentInParent<PlayerMovementController>();
        RobotHeart enemyHeart = null;

        // Detect if we're stealing from an enemy
        if (!wasStolen && transform.parent != null)
        {
            enemyHeart = transform.parent.GetComponentInParent<RobotHeart>();
            var stateController = enemyHeart != null ? enemyHeart.GetComponent<RobotStateController>() : null;
            if (enemyHeart != null && stateController != null && stateController.CurrentState != RobotState.Dead && player != null)
            {
                // TODO: wire badge-stolen behavior into Brain/handlers.
                wasStolen = true;
            }
        }

        attached = true;

        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            CacheOriginalPhysicsState();
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
        else
        {
            Debug.LogWarning($"{nameof(SecurityBadgePickup)} on {name} is missing a {nameof(Rigidbody2D)} component during grab.");
        }
        if (inventory != null)
        {
            if (ownerInventory != null && ownerInventory != inventory)
                ownerInventory.RemoveItem(PickupType.SecurityBadge);
            inventory.SetItem(PickupType.SecurityBadge, this);
            ownerInventory = inventory;
#if UNITY_EDITOR
            if (!ReferenceEquals(inventory.GetItem(PickupType.SecurityBadge), this))
            {
                Debug.LogWarning($"{nameof(SecurityBadgePickup)} on {name} failed to register in inventory {inventory.name}.");
            }
#endif
        }
        else
        {
            Debug.LogWarning($"{nameof(SecurityBadgePickup)} on {name} could not find an inventory to register with.");
        }

        // Detach from any previous hierarchy so the badge is no longer
        // parented to an enemy when picked up.
        Transform attachmentParent = grabParent.root;

        if (player != null)
        {
            var hip = player.BodyReference;
            if (hip != null)
            {
                attachmentParent = hip.transform;
            }
        }

        transform.SetParent(attachmentParent, true);
        SetFollowTarget(attachmentParent);
        ApplySortingOrder(heldSortingOrder);

        // TODO: notify Brain/Memory about badge stolen if needed.

    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (joint == null || !attached || !joint.enabled || followTarget != null)
            return;

        joint.target = attractPoint;
    }

    public void OnRelease(Vector2 throwForce)
    {
        attached = false;
        if (joint != null)
            joint.enabled = false;
        followTarget = null;

        // Re-parent to world root so it no longer follows any holder.
        transform.SetParent(null, worldPositionStays: true);
        if (rb != null)
        {
            rb.bodyType = originalBodyType;
            rb.gravityScale = originalGravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }

        if (ownerInventory != null)
        {
            ownerInventory.RemoveItem(PickupType.SecurityBadge);
            ownerInventory = null;
        }

        ApplySortingOrder(idleSortingOrder);
    }

    /// <summary>
    /// Sets the inventory that currently owns this pickup.
    /// </summary>
    public void AssignInventory(Inventory inventory)
    {
        ownerInventory = inventory;
    }

    private void CacheSpriteRenderers()
    {
        if (spriteRenderers != null)
            return;

        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void ApplySortingOrder(int order)
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
            return;

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer renderer = spriteRenderers[i];
            if (renderer != null)
                renderer.sortingOrder = order;
        }
    }
}
