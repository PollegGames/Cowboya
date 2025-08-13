using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class BatteryPickup : MonoBehaviour, IGrabbable
{
    [Header("Health settings")]
    [SerializeField] private float healthGain = 10f;

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

    private Rigidbody2D rb;
    private TargetJoint2D joint;
    private Transform followTarget;
    private bool attached = false;
    private bool wasStolen = false;
    private float originalGravityScale;

    private Inventory ownerInventory;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalGravityScale = rb.gravityScale;
        joint = GetComponent<TargetJoint2D>();
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
        var inventory = grabParent.GetComponentInParent<Inventory>();
        var player = grabParent.GetComponentInParent<PlayerMovementController>();

        if (!wasStolen && transform.parent != null)
        {
            var enemy = transform.parent.GetComponentInParent<EnemyWorkerController>();
            if (enemy != null)
            {
                var stateController = enemy.GetComponent<RobotStateController>();
                if (stateController != null && stateController.CurrentState != RobotState.Dead && player != null)
                {
                    enemy.OnBatteryStolen(player.gameObject);
                    wasStolen = true;
                }
            }
        }

        attached = true;
        rb.simulated = true;
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var holderState = grabParent.GetComponentInParent<RobotStateController>();
        if (holderState != null)
            holderState.Stats.UpdateHealth(healthGain);

        if (inventory != null)
        {
            if (ownerInventory != null && ownerInventory != inventory)
                ownerInventory.RemoveItem(PickupType.Battery);
            inventory.SetItem(PickupType.Battery, this);
            ownerInventory = inventory;
        }

        if (player != null)
        {
            var hip = player.BodyReference;
            // Detach from any previous hierarchy so the badge is no longer
            // parented to an enemy when picked up.
            transform.SetParent(hip.transform, true);

            if (hip != null)
                SetFollowTarget(hip.transform);
            else
                SetFollowTarget(grabParent);
        }
        else
        {
            SetFollowTarget(grabParent);
        }
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
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = originalGravityScale;
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
    }

    /// <summary>
    /// Sets the inventory that currently owns this pickup.
    /// </summary>
    public void AssignInventory(Inventory inventory)
    {
        ownerInventory = inventory;
    }
}
