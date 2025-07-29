using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(SpringJoint2D))]
public class SecurityBadgePickup : MonoBehaviour, IGrabbable
{
    [Header("Throw settings")]
    public float throwStrength = 5f;

    [Header("Spring Joint Settings")]
    [Tooltip("How springy the joint is.")]
    [SerializeField] private float frequency = 5f;
    [Tooltip("How much the joint resists oscillation.")]
    [SerializeField] private float dampingRatio = 0.8f;
    [Tooltip("Rest length of the spring.")]
    [SerializeField] private float restDistance = 0f;

    Rigidbody2D rb;
    SpringJoint2D spring;
    bool attached = false;

    // Tracks the badge currently held by the player
    public static SecurityBadgePickup PlayerHeldBadge { get; private set; }
    bool heldByPlayer = false;

    void Awake()
    {
        rb     = GetComponent<Rigidbody2D>();
        spring = GetComponent<SpringJoint2D>();

        // Start disabled — only enable when grabbed
        spring.enabled = false;

        // Configure spring behavior
        spring.autoConfigureDistance = false;
        spring.distance              = restDistance;
        spring.frequency             = frequency;
        spring.dampingRatio          = dampingRatio;
        // Note: SpringJoint2D has no maxForce setting

        // If we attach to a world point (no Rigidbody), we will use connectedAnchor
        spring.connectedBody = null;
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

        attached = true;
        rb.simulated = true;

        var player = grabParent.GetComponentInParent<PlayerMovementController>();
        if (player != null)
        {
            // Attach directly to the player's hips
            var hip = player.BodyReference;
            if (hip != null)
            {
                spring.connectedBody = hip;
                transform.SetParent(hip.transform, true);
                PlayerHeldBadge = this;
                heldByPlayer = true;

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
            // Fallback: attach to whatever grabbed us
            var handRb = grabParent.GetComponentInParent<Rigidbody2D>();
            if (handRb != null)
            {
                spring.connectedBody = handRb;
            }
            else
            {
                spring.connectedBody = null;
                spring.connectedAnchor = transform.position;
            }
        }

        spring.distance = restDistance;
        spring.enabled = true;
    }

    public void OnAttract(Vector2 attractPoint)
    {
        // Update world‐anchor if we're not tied to an actual Rigidbody2D
        if (attached && spring.enabled && spring.connectedBody == null)
        {
            spring.connectedAnchor = attractPoint;
        }
    }

    public void OnRelease(Vector2 throwForce)
    {
        attached = false;

        // Turn off the spring
        spring.enabled        = false;
        spring.connectedBody  = null;

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
