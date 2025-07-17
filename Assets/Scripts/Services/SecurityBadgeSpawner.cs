using UnityEngine;

public class SecurityBadgeSpawner : MonoBehaviour
{
    [SerializeField] private SecurityBadgePickup badgePrefab;

    public SecurityBadgePickup SpawnBadge(Transform parent)
    {
        if (badgePrefab == null) return null;
        return Instantiate(badgePrefab, parent);
    }
}
