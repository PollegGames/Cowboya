using UnityEngine;

public class PickupBox : MonoBehaviour, IGrabbable
{
    [SerializeField] private Rigidbody2D rb;

    private RigidbodyType2D originalBodyType;

    private float originalGravityScale;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            originalBodyType = rb.bodyType;
            originalGravityScale = rb.gravityScale;
        }
    }

    public bool CanBeGrabbed(Inventory inventory)
    {
        return true;
    }

    public void OnGrab(Transform grabParent)
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            originalBodyType = rb.bodyType;
            originalGravityScale = rb.gravityScale;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
        }
        transform.SetParent(grabParent);
        transform.localPosition = Vector3.zero;
    }

    public void OnRelease(Vector2 throwForce)
    {
        transform.SetParent(null);
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.bodyType = originalBodyType;
            rb.gravityScale = originalGravityScale;
            rb.linearVelocity = throwForce;
            rb.angularVelocity = 0f;
        }
    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (rb != null)
            rb.MovePosition(attractPoint);
    }
}
