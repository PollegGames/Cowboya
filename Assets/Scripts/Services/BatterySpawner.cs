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

        battery.SetFollowTarget(parent);

        return battery;
    }
}
