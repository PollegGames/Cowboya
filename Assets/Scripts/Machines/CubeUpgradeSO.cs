using UnityEngine;

/// <summary>
/// Stores a cube upgrade and can apply it to robot stats.
/// </summary>
[CreateAssetMenu(fileName = "CubeUpgrade", menuName = "Upgrades/CubeUpgrade")]
public class CubeUpgradeSO : ScriptableObject
{
    [SerializeField] private CubeUpgradeType selectedUpgrade;

    [SerializeField] private float upgradeMaxHealthValue = 10f;
    [SerializeField] private float upgradeMaxEnergyValue = 10f;
    [SerializeField] private float upgradeEnergyRechargeValue = 5f;
    [SerializeField] private int upgradeAttackDamageValue = 5;

    /// <summary>
    /// Value added to MaxHealth when upgrading.
    /// </summary>
    public float UpgradeMaxHealthValue => upgradeMaxHealthValue;

    /// <summary>
    /// Value added to MaxEnergy when upgrading.
    /// </summary>
    public float UpgradeMaxEnergyValue => upgradeMaxEnergyValue;

    /// <summary>
    /// Value added to energy recharge when upgrading.
    /// </summary>
    public float UpgradeEnergyRechargeValue => upgradeEnergyRechargeValue;

    /// <summary>
    /// Value added to attack damage when upgrading.
    /// </summary>
    public int UpgradeAttackDamageValue => upgradeAttackDamageValue;

    /// <summary>
    /// Gets the stored upgrade type.
    /// </summary>
    public CubeUpgradeType SelectedUpgrade => selectedUpgrade;

    /// <summary>
    /// Stores the provided upgrade type.
    /// </summary>
    /// <param name="upgrade">Upgrade obtained from a cube.</param>
    public void Store(CubeUpgradeType upgrade)
    {
        selectedUpgrade = upgrade;
    }

    /// <summary>
    /// Applies the stored upgrade to the target stats.
    /// </summary>
    /// <param name="target">Stats to receive the upgrade.</param>
    public void ApplyUpgrade(RobotStats target)
    {
        if (target == null)
            return;

        switch (selectedUpgrade)
        {
            case CubeUpgradeType.MaxHealth:
                target.MaxHealth += upgradeMaxHealthValue;
                target.UpdateHealth(upgradeMaxHealthValue);
                break;
            case CubeUpgradeType.MaxEnergy:
                target.MaxEnergy += upgradeMaxEnergyValue;
                target.UpdateEnergy(upgradeMaxEnergyValue);
                break;
            case CubeUpgradeType.EnergyRecharge:
                target.UpdateEnergyRecharge(upgradeEnergyRechargeValue);
                break;
            case CubeUpgradeType.AttackDamage:
                foreach (Attack attack in target.Attacks)
                    attack.Damage += upgradeAttackDamageValue;
                break;
        }
    }
}

