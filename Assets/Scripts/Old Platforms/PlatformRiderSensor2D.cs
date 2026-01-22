using UnityEngine;

/// <summary>
/// Forwarder component that reports collision contacts to a bound PlatformRider2D.
/// Place this on any foot/leg collider that actually touches platforms. Bind to the rider on the hips.
/// </summary>
[DisallowMultipleComponent]
public class PlatformRiderSensor2D : MonoBehaviour
{
    [SerializeField] private PlatformRider2D rider;

    public void Bind(PlatformRider2D r)
    {
        rider = r;
    }

    private void EnsureRider()
    {
        if (rider == null) rider = GetComponentInParent<PlatformRider2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        EnsureRider();
        rider?.SensorCollisionEnter(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        EnsureRider();
        rider?.SensorCollisionStay(collision);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        EnsureRider();
        rider?.SensorCollisionExit(collision);
    }
}

