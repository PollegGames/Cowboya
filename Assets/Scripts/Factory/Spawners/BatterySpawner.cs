using UnityEngine;

public class BatterySpawner : MonoBehaviour
{
    [SerializeField] private BatteryPickup batteryPrefab;

    public BatteryPickup SpawnBattery(Transform parent)
    {
        if (batteryPrefab == null)
        {
            Debug.LogWarning("BatterySpawner: batteryPrefab is null!");
            return null;
        }

        var battery = Instantiate(
            batteryPrefab,
            parent.position,
            parent.rotation,
            parent
        );

        battery.gameObject.layer = LayerMask.NameToLayer("Battery");

        var rb = battery.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.simulated = true;
        }

        battery.SetFollowTarget(parent);

        return battery;
    }
}
