using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class BatteryPickup : MonoBehaviour, IGrabbable
{
    [Header("Health settings")]
    [SerializeField] private float healthGain = 10f;

    [Header("Attachment")]
    [Tooltip("Controls how quickly the battery eases toward its follow target when attached.")]
    [SerializeField, Range(1f, 40f)] private float followLerpSpeed = 20f;

    [Header("Visuals")]
    [SerializeField, Tooltip("Sorting order applied while the battery is held.")] private int heldSortingOrder = 20;
    [SerializeField, Tooltip("Sorting order applied when the battery is idle.")] private int idleSortingOrder = 0;

    [Header("Target Joint Settings")]
    [Tooltip("How springy the joint movement is. Recommended range: 5â€“15.")]
    [SerializeField, Range(5f, 15f)] private float frequency = 10f;
    [Tooltip("How much the joint resists oscillation. Recommended range: 0â€“1.")]
    [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.9f;
    [Tooltip("Maximum force the joint can apply. Recommended range: 500â€“3000.")]
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

    private Rigidbody2D rb;
    private TargetJoint2D joint;
    private Transform followTarget;
    private bool attached = false;
    private bool wasStolen = false;
    private float originalGravityScale;
    private RigidbodyType2D originalBodyType;

    private Inventory ownerInventory;
    private SpriteRenderer[] spriteRenderers;

    private void CacheOriginalPhysicsState()
    {
        if (rb == null)
            return;

        originalBodyType = rb.bodyType;
        originalGravityScale = rb.gravityScale;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        CacheOriginalPhysicsState();
        joint = GetComponent<TargetJoint2D>();
        CacheSpriteRenderers();
        ApplySortingOrder(idleSortingOrder);
        if (joint != null)
        {
            joint.enabled = false;
            joint.autoConfigureTarget = false;
            joint.target = rb.position;
            joint.frequency = frequency;
            joint.dampingRatio = dampingRatio;
            joint.maxForce = maxForce;
        }
    }

    private void FixedUpdate()
    {
        if (joint == null || !joint.enabled || followTarget == null)
            return;

        joint.target = followTarget.position;

        if (attached)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            if (rb != null)
            {
                Vector2 desired = joint.target;
                Vector2 next = Vector2.Lerp(rb.position, desired, followLerpSpeed * Time.fixedDeltaTime);
                rb.MovePosition(next);
            }
        }
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
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
            Debug.LogWarning($"{nameof(BatteryPickup)} on {name} is missing a {nameof(TargetJoint2D)} component.");
        }
    }

    public bool CanBeGrabbed(Inventory inventory)
    {
        if (inventory != null)
        {
            var held = inventory.GetItem(PickupType.Battery);
            if (held != null && (object)held != this)
                return false;
        }
        return true;
    }

    public void OnGrab(Transform grabParent)
    {
        if (grabParent == null)
        {
            Debug.LogWarning($"{nameof(BatteryPickup)} received a null grab parent.");
            return;
        }

        var player = grabParent.GetComponentInParent<PlayerMovementController>();

        Inventory inventory = null;
        if (player != null)
        {
            inventory = player.GetComponent<Inventory>();
            if (inventory == null)
                inventory = player.GetComponentInChildren<Inventory>();
        }

        if (inventory == null)
        {
            inventory = grabParent.GetComponent<Inventory>();
            if (inventory == null)
                inventory = grabParent.GetComponentInParent<Inventory>();
        }

        if (inventory == null && grabParent.root != null)
        {
            var root = grabParent.root;
            inventory = root.GetComponent<Inventory>();
            if (inventory == null)
                inventory = root.GetComponentInChildren<Inventory>();
        }

        if (inventory == null && ownerInventory != null)
            inventory = ownerInventory;

        if (inventory == null)
        {
            var candidates = UnityEngine.Object.FindObjectsByType<Inventory>(FindObjectsSortMode.None);
            if (candidates != null && candidates.Length > 0)
                inventory = candidates[0];
        }

        if (!wasStolen && transform.parent != null)
        {
            var brain = transform.parent.GetComponentInParent<RobotBrainNew>();
            var stateController = brain != null ? brain.GetComponent<RobotStateController>() : null;
            if (brain != null && stateController != null && stateController.CurrentState != RobotState.Dead && player != null)
            {
                // TODO: notify brain/memory about battery stolen if behavior needed.
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
            Debug.LogWarning($"{nameof(BatteryPickup)} on {name} is missing a {nameof(Rigidbody2D)} component during grab.");
        }

        var holderState = grabParent.GetComponentInParent<RobotStateController>();
        if (holderState != null)
            holderState.Stats.UpdateHealth(healthGain);

        if (inventory != null)
        {
            if (ownerInventory != null && ownerInventory != inventory)
                ownerInventory.RemoveItem(PickupType.Battery);
            inventory.SetItem(PickupType.Battery, this);
            ownerInventory = inventory;
#if UNITY_EDITOR
            if (!ReferenceEquals(inventory.GetItem(PickupType.Battery), this))
            {
                Debug.LogWarning($"{nameof(BatteryPickup)} on {name} failed to register in inventory {inventory.name}.");
            }
#endif
        }
        else
        {
            Debug.LogWarning($"{nameof(BatteryPickup)} on {name} could not find an inventory to register with.");
        }

        if (player != null)
        {
            var hip = player.BodyReference;
            if (hip != null)
            {
                var hipTransform = hip.transform;
                transform.SetParent(hipTransform, true);
                SetFollowTarget(hipTransform);
            }
            else
            {
                transform.SetParent(grabParent, true);
                SetFollowTarget(grabParent);
            }
        }
        else
        {
            transform.SetParent(grabParent, true);
            SetFollowTarget(grabParent);
        }

        ApplySortingOrder(heldSortingOrder);
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
        transform.SetParent(null, true);

        if (rb != null)
        {
            rb.bodyType = originalBodyType;
            rb.gravityScale = originalGravityScale;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = true;
        }
        else
        {
            Debug.LogWarning($"{nameof(BatteryPickup)} on {name} is missing a {nameof(Rigidbody2D)} component.");
        }

        if (ownerInventory != null)
        {
            ownerInventory.RemoveItem(PickupType.Battery);
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

