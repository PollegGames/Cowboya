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
        return !attached;
    }

    public void OnGrab(Transform grabParent)
    {
        attached = true;
        rb.simulated = true;

        // Try to hook up to the hand's Rigidbody2D
        var handRb = grabParent.GetComponentInParent<Rigidbody2D>();
        if (handRb != null)
        {
            spring.connectedBody = handRb;
        }
        else
        {
            spring.connectedBody   = null;
            // anchor at current position so it doesn't snap
            spring.connectedAnchor = transform.position;
        }

        spring.distance = restDistance;
        spring.enabled  = true;
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

        // // Give it some velocity so it flies off
        // rb.AddForce(throwForce * throwStrength, ForceMode2D.Impulse);
    }
}
