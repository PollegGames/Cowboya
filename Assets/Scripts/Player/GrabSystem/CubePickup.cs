using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class CubePickup : MonoBehaviour, IGrabbable
{

    [Header("Target Joint Settings")]
    [Tooltip("How springy the joint movement is.")]
    [SerializeField] private float frequency = 5f;
    [Tooltip("How much the joint resists oscillation.")]
    [SerializeField] private float dampingRatio = 0.8f;
    [Tooltip("Maximum force the joint can apply.")]
    [SerializeField] private float maxForce = 1000f;

    private Rigidbody2D rb;
    private TargetJoint2D joint;
    private Transform followTarget;
    private bool attached = false;
    private bool wasStolen = false;

    private Inventory ownerInventory;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<TargetJoint2D>();

        joint.enabled = false;
        joint.autoConfigureTarget = false;
        joint.target = rb.position;
        joint.frequency = frequency;
        joint.dampingRatio = dampingRatio;
        joint.maxForce = maxForce;
    }

    private void FixedUpdate()
    {
        if (joint.enabled && followTarget != null)
            joint.target = followTarget.position;
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
            Debug.LogWarning($"{nameof(CubePickup)} on {name} is missing a {nameof(TargetJoint2D)} component.");
        }
    }

    public bool CanBeGrabbed(Inventory inventory)
    {
        if (inventory != null)
        {
            var held = inventory.GetItem(PickupType.Cube);
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
                ownerInventory.RemoveItem(PickupType.Cube);
            inventory.SetItem(PickupType.Cube, this);
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
        if (attached && joint.enabled && followTarget == null)
            joint.target = attractPoint;
    }

    public void OnRelease(Vector2 throwForce)
    {
        attached = false;
        joint.enabled = false;
        followTarget = null;

        if (ownerInventory != null)
        {
            ownerInventory.RemoveItem(PickupType.Cube);
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
