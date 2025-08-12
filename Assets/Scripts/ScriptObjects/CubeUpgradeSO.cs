using UnityEngine;

/// <summary>
/// Stores a cube upgrade and can apply it to robot stats.
/// </summary>
[CreateAssetMenu(fileName = "CubeUpgrade", menuName = "Upgrades/CubeUpgrade")]
public class CubeUpgradeSO : ScriptableObject
{
    [SerializeField] private CubeUpgradeType selectedUpgrade;

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
                target.MaxHealth += 1f;
                target.UpdateHealth(1f);
                break;
            case CubeUpgradeType.MaxEnergy:
                target.MaxEnergy += 1f;
                target.UpdateEnergy(1f);
                break;
            case CubeUpgradeType.EnergyRecharge:
                target.UpdateEnergy(1f);
                break;
            case CubeUpgradeType.AttackDamage:
                foreach (Attack attack in target.Attacks)
                    attack.Damage += 1;
                break;
        }
    }
}

