using UnityEngine;

/// <summary>
/// Collects upgrade cubes and stores their upgrade type.
/// Requires a trigger Collider2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CubeCollector : MonoBehaviour
{
    [SerializeField] private CubeUpgradeSO upgradeConfig; 

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
        if (other == null) return;

        var pickup = other.GetComponent<CubePickup>();
        var cube = other.GetComponent<CubeUpgrade>();
        if (pickup == null || cube == null) return;

        var runStats = RunProgressManager.Instance?.RunStats;

        if (runStats != null)
        {
            switch (cube.UpgradeType)
            {
                case CubeUpgradeType.MaxHealth:
                    runStats.MaxHealthBonus += upgradeConfig.UpgradeMaxHealthValue; break;
                case CubeUpgradeType.MaxEnergy:
                    runStats.MaxEnergyBonus += upgradeConfig.UpgradeMaxEnergyValue; break;
                case CubeUpgradeType.EnergyRecharge:
                    runStats.AddEnergyRechargeBonus(upgradeConfig.UpgradeEnergyRechargeValue); break;
                case CubeUpgradeType.AttackDamage:
                    runStats.AttackDamageBonus += upgradeConfig.UpgradeAttackDamageValue; break;
            }
        }

        // Do NOT call ApplyUpgrade(...) and do NOT Capture() here anymore.
        Destroy(pickup.gameObject);
    }
}

