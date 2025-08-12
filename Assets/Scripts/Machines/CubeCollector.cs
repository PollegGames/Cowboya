using UnityEngine;

/// <summary>
/// Collects upgrade cubes and stores their upgrade type.
/// Requires a trigger Collider2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CubeCollector : MonoBehaviour
{
    [SerializeField] private CubeUpgradeSO upgradeStore;

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

        CubePickup pickup = other.GetComponent<CubePickup>();
        CubeUpgrade cube = other.GetComponent<CubeUpgrade>();

        if (pickup != null && cube != null && upgradeStore != null)
        {
            upgradeStore.Store(cube.UpgradeType);

            RobotStats playerStats = other.GetComponentInParent<RobotStateController>()?.Stats;
            PlayerRunStats runStats = RunProgressManager.Instance?.RunStats;
            if (playerStats != null)
            {
                if (runStats != null)
                {
                    switch (cube.UpgradeType)
                    {
                        case CubeUpgradeType.MaxHealth:
                            runStats.MaxHealthBonus += upgradeStore.UpgradeMaxHealthValue;
                            break;
                        case CubeUpgradeType.MaxEnergy:
                            runStats.MaxEnergyBonus += upgradeStore.UpgradeMaxEnergyValue;
                            break;
                        case CubeUpgradeType.EnergyRecharge:
                            runStats.AddEnergyRechargeBonus(upgradeStore.UpgradeEnergyRechargeValue);
                            break;
                        

                        case CubeUpgradeType.AttackDamage:
                            runStats.AttackDamageBonus += upgradeStore.UpgradeAttackDamageValue;
                            break;
                    }
                }

                upgradeStore.ApplyUpgrade(playerStats);
                runStats?.Capture(playerStats);
            }

            Destroy(pickup.gameObject);
        }
    }
}

