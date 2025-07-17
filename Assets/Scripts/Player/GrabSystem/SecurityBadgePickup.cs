using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(TargetJoint2D))]
public class SecurityBadgePickup : MonoBehaviour, IGrabbable
{
    [Header("Throw settings")]
    public float throwStrength = 5f;

    Rigidbody2D rb;
    TargetJoint2D joint;
    bool attached = false;

    void Awake()
    {
        rb    = GetComponent<Rigidbody2D>();
        joint = GetComponent<TargetJoint2D>();

        // Start disabled — only enable when grabbed
        joint.enabled = false;

        // Tweak as you like in Inspector:
        joint.frequency       = 5f;   // springiness
        joint.dampingRatio    = 0.8f; // how “snappy” it is
        joint.maxForce        = 1000f;
        joint.autoConfigureTarget = false;
    }

    public bool CanBeGrabbed()
    {
        return !attached;
    }

    public void OnGrab(Transform grabParent)
    {
        attached = true;

        // Make sure physics is on
        rb.simulated = true;

        // Connect the joint to the hand’s rigidbody (if any)
        var handRb = grabParent.GetComponentInParent<Rigidbody2D>();
        if (handRb != null)
            joint.connectedBody = handRb;
        else
            joint.connectedBody = null;

        // Enable the joint; set its initial target to current position
        joint.enabled        = true;
        joint.target         = transform.position;
    }

    public void OnAttract(Vector2 attractPoint)
    {
        // Each frame you hold, update the joint’s target
        if (attached && joint.enabled)
            joint.target = attractPoint;
    }

    public void OnRelease(Vector2 throwForce)
    {
        attached = false;

        // Turn off the joint
        joint.enabled        = false;
        joint.connectedBody  = null;

        // Give it some velocity so it flies off
        rb.AddForce(throwForce, ForceMode2D.Impulse);
    }
}
