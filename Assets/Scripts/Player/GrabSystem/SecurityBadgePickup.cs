using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class SecurityBadgePickup : MonoBehaviour, IGrabbable
{
    [Header("Throw settings")]
    public float throwStrength = 5f;

    [Header("Target Joint Settings")]
    [Tooltip("How springy the joint movement is.")]
    [SerializeField] private float frequency = 5f;
    [Tooltip("How much the joint resists oscillation.")]
    [SerializeField] private float dampingRatio = 0.8f;
    [Tooltip("Maximum force the joint can apply.")]
    [SerializeField] private float maxForce = 1000f;

    Rigidbody2D rb;
    TargetJoint2D joint;
    Transform followTarget;
    bool attached = false;

    // Flag to ensure stolen logic only runs once
    bool wasStolen = false;

    Inventory ownerInventory;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<TargetJoint2D>();
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
        if (joint != null && joint.enabled && followTarget != null)
        {
            joint.target = followTarget.position;
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
        var inventory = grabParent.GetComponentInParent<Inventory>();
        var player = grabParent.GetComponentInParent<PlayerMovementController>();

        // Detect if we're stealing from an enemy
        if (!wasStolen && transform.parent != null)
        {
            var enemy = transform.parent.GetComponentInParent<EnemyController>();
            if (enemy != null)
            {
                var stateController = enemy.GetComponent<RobotStateController>();
                if (stateController != null && stateController.CurrentState != RobotState.Dead && player != null)
                {
                    wasStolen = true;
                }
            }
        }

        attached = true;
        rb.simulated = true;
        if (inventory != null)
        {
            if (ownerInventory != null && ownerInventory != inventory)
                ownerInventory.RemoveItem(PickupType.SecurityBadge);
            inventory.SetItem(PickupType.SecurityBadge, this);
            ownerInventory = inventory;
        }

        if (player != null)
        {
            var hip = player.BodyReference;
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
        if (joint == null)
            return;

        if (attached && joint.enabled && followTarget == null)
            joint.target = attractPoint;
    }

    public void OnRelease(Vector2 throwForce)
    {
        attached = false;
        if (joint != null)
            joint.enabled = false;
        followTarget = null;

        if (ownerInventory != null)
        {
            ownerInventory.RemoveItem(PickupType.SecurityBadge);
            ownerInventory = null;
        }

        // // Give it some velocity so it flies off
        // rb.AddForce(throwForce * throwStrength, ForceMode2D.Impulse);
    }

    /// <summary>
    /// Sets the inventory that currently owns this pickup.
    /// </summary>
    public void AssignInventory(Inventory inventory)
    {
        ownerInventory = inventory;
    }
}
