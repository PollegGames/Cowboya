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

    // Tracks the badge currently held by the player
    public static SecurityBadgePickup PlayerHeldBadge { get; private set; }
    bool heldByPlayer = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<TargetJoint2D>();

        // Start disabled — only enable when grabbed
        joint.enabled = false;

        // Configure joint behavior
        joint.autoConfigureTarget = false;
        joint.target = rb.position;
        joint.frequency = frequency;
        joint.dampingRatio = dampingRatio;
        joint.maxForce = maxForce;
    }

    void FixedUpdate()
    {
        if (joint.enabled && followTarget != null)
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

    public bool CanBeGrabbed()
    {
        if (PlayerHeldBadge != null && PlayerHeldBadge != this)
            return false;
        return !attached;
    }

    public void OnGrab(Transform grabParent)
    {
        // Prevent grabbing a second badge once one is already attached
        if (PlayerHeldBadge != null && PlayerHeldBadge != this)
            return;

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
                    enemy.OnBadgeStolen(player.gameObject);
                    wasStolen = true;
                }
            }
        }

        attached = true;
        rb.simulated = true;
        if (player != null)
        {
            // Attach directly to the player's hips
            var hip = player.BodyReference;
            if (hip != null)
            {
                PlayerHeldBadge = this;
                heldByPlayer = true;
                SetFollowTarget(hip.transform);

                // Disable or destroy all other badges in the scene
                foreach (var badge in FindObjectsOfType<SecurityBadgePickup>())
                {
                    if (badge != this)
                        Destroy(badge.gameObject);
                }
            }
        }
        else
        {
            // Fallback: follow whatever grabbed us
            SetFollowTarget(grabParent);
        }

    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (attached && joint.enabled && followTarget == null)
        {
            joint.target = attractPoint;
        }
    }

    public void OnRelease(Vector2 throwForce)
    {
        attached = false;

        // Turn off the joint
        joint.enabled = false;
        followTarget = null;

        if (heldByPlayer)
        {
            heldByPlayer = false;
            if (PlayerHeldBadge == this)
                PlayerHeldBadge = null;
        }

        // // Give it some velocity so it flies off
        // rb.AddForce(throwForce * throwStrength, ForceMode2D.Impulse);
    }

    private void OnDestroy()
    {
        if (PlayerHeldBadge == this)
            PlayerHeldBadge = null;
    }

    public static void DropPlayerBadge()
    {
        if (PlayerHeldBadge != null)
        {
            PlayerHeldBadge.OnRelease(Vector2.zero);
        }
    }
}
