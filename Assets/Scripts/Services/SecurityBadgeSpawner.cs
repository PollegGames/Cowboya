using UnityEngine;

public class SecurityBadgeSpawner : MonoBehaviour
{
    public static SecurityBadgeSpawner Instance { get; private set; }

    [SerializeField] private SecurityBadgePickup badgePrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public SecurityBadgePickup SpawnBadge(Transform parent)
    {
        if (badgePrefab == null) return null;
        return Instantiate(badgePrefab, parent);
    }
}
