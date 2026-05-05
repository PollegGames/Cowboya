using UnityEngine;

/// <summary>
/// Collects upgrade cubes and stores their upgrade type.
/// Requires a trigger Collider2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CubeCollector : MonoBehaviour
{
    [SerializeField] private CubeUpgradeSO upgradeStore;
    [SerializeField, HideInInspector] private CubeUpgradeSO upgradeConfig; // legacy serialization support

    private CubeUpgradeSO ActiveUpgradeStore => upgradeStore != null ? upgradeStore : upgradeConfig;

    private void OnValidate()
    {
        if (upgradeStore == null && upgradeConfig != null)
            upgradeStore = upgradeConfig;
    }

    private void Awake()
    {
        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
            return;

        var pickup = other.GetComponent<CubePickup>();
        var cube = other.GetComponent<CubeUpgrade>();
        if (pickup == null || cube == null)
            return;

        Debug.Log($"[CubeCollector] Collected {cube.UpgradeType} cube from {pickup.name}.", this);
        StoreUpgrade(cube.UpgradeType);
        ApplyRunBonuses(cube.UpgradeType);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEngine.Object.DestroyImmediate(pickup.gameObject);
            return;
        }
#endif
        UnityEngine.Object.Destroy(pickup.gameObject);
    }

    private void StoreUpgrade(CubeUpgradeType upgrade)
    {
        var store = ActiveUpgradeStore;
        if (store != null)
        {
            store.Store(upgrade);
            upgradeStore = store;
        }
        else
        {
            Debug.LogWarning($"{nameof(CubeCollector)} on {name} has no {nameof(CubeUpgradeSO)} assigned.");
        }
    }

    private void ApplyRunBonuses(CubeUpgradeType upgrade)
    {
        var manager = RunProgressManager.Instance;
        var runStats = manager != null ? manager.RunStats : null;
        var store = ActiveUpgradeStore;
        if (manager == null)
        {
            Debug.LogWarning("[CubeCollector] Run bonus skipped because RunProgressManager is missing.", this);
            return;
        }

        if (runStats == null)
        {
            Debug.LogWarning("[CubeCollector] Run bonus skipped because PlayerRunStats is missing on RunProgressManager.", this);
            return;
        }

        if (store == null)
        {
            Debug.LogWarning("[CubeCollector] Run bonus skipped because CubeUpgradeSO is missing.", this);
            return;
        }

        runStats.AddCubeBonus(upgrade, store);
        Debug.Log($"[CubeCollector] Run bonus stored for {upgrade}. Totals: {runStats.DescribeBonuses()}", this);
    }
}
