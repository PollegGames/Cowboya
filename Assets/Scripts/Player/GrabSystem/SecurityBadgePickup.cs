using UnityEngine;

public class SecurityBadgePickup : MonoBehaviour, IGrabbable
{
    private Rigidbody2D rb;
    private bool attached;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public bool CanBeGrabbed()
    {
        return !attached;
    }

    public void OnGrab(Transform grabParent)
    {
        attached = true;
        if (rb != null)
        {
            rb.simulated = false;
            rb.velocity = Vector2.zero;
        }
        transform.SetParent(grabParent);
        transform.localPosition = Vector3.zero;
    }

    public void OnRelease(Vector2 throwForce)
    {
        attached = false;
        transform.SetParent(null);
        if (rb != null)
        {
            rb.simulated = true;
            rb.velocity = throwForce;
        }
    }

    public void OnAttract(Vector2 attractPoint)
    {
        transform.position = Vector2.MoveTowards(transform.position, attractPoint, 10f * Time.deltaTime);
    }
}
