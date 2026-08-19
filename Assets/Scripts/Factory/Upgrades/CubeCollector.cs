using System.Collections.Generic;
using UnityEngine;

public enum CubeCollectionMode {
    LaboratoryStorage = 0,
    ImmediateUpgradeLegacy = 1
}

/// <summary>
/// Collects cube resources. The default mode stores exact cube counts in the
/// current run so the following laboratory can restore them physically.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CubeCollector : MonoBehaviour {
    [SerializeField] private CubeCollectionMode collectionMode = CubeCollectionMode.LaboratoryStorage;
    [SerializeField] private bool collectNormalCubesAsWhite = true;
    [SerializeField] private CubeUpgradeSO upgradeStore;
    [SerializeField, HideInInspector] private CubeUpgradeSO upgradeConfig;

    private readonly HashSet<int> committedCubeIds = new HashSet<int>();

    private CubeUpgradeSO ActiveUpgradeStore => upgradeStore != null ? upgradeStore : upgradeConfig;

    private void OnValidate() {
        if (upgradeStore == null && upgradeConfig != null) {
            upgradeStore = upgradeConfig;
        }
    }

    private void Awake() {
        Collider2D collectorCollider = GetComponent<Collider2D>();
        if (collectorCollider != null) {
            collectorCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other) {
        if (other == null) {
            return;
        }

        CubePickup pickup = other.GetComponentInParent<CubePickup>();
        if (pickup == null) {
            return;
        }

        if (collectionMode == CubeCollectionMode.ImmediateUpgradeLegacy) {
            TryCollectLegacyUpgrade(pickup);
            return;
        }

        RunProgressManager manager = RunProgressManager.Instance;
        if (manager == null) {
            Debug.LogWarning(
                $"[{nameof(CubeCollector)}] Cannot collect '{pickup.name}' because RunProgressManager is missing.",
                this);
            return;
        }

        TryCollectForLaboratory(pickup, manager.LaboratoryProgress);
    }

    /// <summary>
    /// Atomically records one cube in the run's incoming laboratory storage and
    /// removes its physical representation only after the state commit succeeds.
    /// </summary>
    public bool TryCollectForLaboratory(CubePickup pickup, LaboratoryProgress progress) {
        if (pickup == null || progress == null) {
            return false;
        }

        int instanceId = pickup.GetInstanceID();
        if (committedCubeIds.Contains(instanceId)) {
            return false;
        }

        if (!TryResolveLaboratoryCubeType(pickup, out LaboratoryCubeType cubeType)) {
            Debug.LogWarning(
                $"[{nameof(CubeCollector)}] '{pickup.name}' has no supported cube resource type.",
                this);
            return false;
        }

        if (!progress.TryStoreIncomingCube(cubeType)) {
            Debug.LogWarning(
                $"[{nameof(CubeCollector)}] Could not store {cubeType} from '{pickup.name}'.",
                this);
            return false;
        }

        committedCubeIds.Add(instanceId);
        DisableCubeInteractions(pickup);
        Debug.Log($"[{nameof(CubeCollector)}] Stored one {cubeType} cube for the laboratory.", this);
        DestroyCollectedCube(pickup.gameObject);
        return true;
    }

    private bool TryResolveLaboratoryCubeType(
        CubePickup pickup,
        out LaboratoryCubeType cubeType) {
        CubeUpgrade upgrade = pickup.GetComponent<CubeUpgrade>();
        if (upgrade != null) {
            switch (upgrade.UpgradeType) {
                case CubeUpgradeType.MaxHealth:
                    cubeType = LaboratoryCubeType.MaxHealth;
                    return true;
                case CubeUpgradeType.MaxEnergy:
                    cubeType = LaboratoryCubeType.MaxEnergy;
                    return true;
                case CubeUpgradeType.EnergyRecharge:
                    cubeType = LaboratoryCubeType.EnergyRecharge;
                    return true;
                case CubeUpgradeType.AttackDamage:
                    cubeType = LaboratoryCubeType.AttackDamage;
                    return true;
            }

            cubeType = default;
            return false;
        }

        if (collectNormalCubesAsWhite) {
            cubeType = LaboratoryCubeType.White;
            return true;
        }

        cubeType = default;
        return false;
    }

    private bool TryCollectLegacyUpgrade(CubePickup pickup) {
        if (pickup == null) {
            return false;
        }

        int instanceId = pickup.GetInstanceID();
        if (committedCubeIds.Contains(instanceId)) {
            return false;
        }

        CubeUpgrade cube = pickup.GetComponent<CubeUpgrade>();
        if (cube == null) {
            return false;
        }

        CubeUpgradeSO store = ActiveUpgradeStore;
        RunProgressManager manager = RunProgressManager.Instance;
        PlayerRunStats runStats = manager != null ? manager.RunStats : null;
        if (store == null || runStats == null) {
            Debug.LogWarning(
                $"[{nameof(CubeCollector)}] Legacy collection requires CubeUpgradeSO and PlayerRunStats.",
                this);
            return false;
        }

        store.Store(cube.UpgradeType);
        upgradeStore = store;
        runStats.AddCubeBonus(cube.UpgradeType, store);

        committedCubeIds.Add(instanceId);
        DisableCubeInteractions(pickup);
        Debug.Log(
            $"[{nameof(CubeCollector)}] Applied legacy {cube.UpgradeType} upgrade. "
            + $"Totals: {runStats.DescribeBonuses()}",
            this);
        DestroyCollectedCube(pickup.gameObject);
        return true;
    }

    private static void DisableCubeInteractions(CubePickup pickup) {
        Collider2D[] colliders = pickup.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++) {
            if (colliders[i] != null) {
                colliders[i].enabled = false;
            }
        }
    }

    private static void DestroyCollectedCube(GameObject cubeObject) {
#if UNITY_EDITOR
        if (!Application.isPlaying) {
            Object.DestroyImmediate(cubeObject);
            return;
        }
#endif
        Object.Destroy(cubeObject);
    }
}
