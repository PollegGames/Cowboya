using UnityEngine;

public class PickupBox : MonoBehaviour, IGrabbable
{
    [SerializeField] private Rigidbody2D rb;

    private void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
    }

    public bool CanBeGrabbed()
    {
        return true;
    }

    public void OnGrab(Transform grabParent)
    {
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.velocity = Vector2.zero;
        }
        transform.SetParent(grabParent);
        transform.localPosition = Vector3.zero;
    }

    public void OnRelease(Vector2 throwForce)
    {
        transform.SetParent(null);
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(throwForce, ForceMode2D.Impulse);
        }
    }

    public void OnAttract(Vector2 attractPoint)
    {
        if (rb != null)
            rb.MovePosition(attractPoint);
    }
}
