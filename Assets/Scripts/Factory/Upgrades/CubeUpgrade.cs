using UnityEngine;

/// <summary>
/// Defines an upgrade type for a cube.
/// </summary>
public class CubeUpgrade : MonoBehaviour
{
    [SerializeField] private CubeUpgradeType upgradeType;

    /// <summary>
    /// Gets the configured upgrade type.
    /// </summary>
    public CubeUpgradeType UpgradeType => upgradeType;
}


public enum CubeUpgradeType
{
    MaxHealth,
    MaxEnergy,
    EnergyRecharge,
    AttackDamage
}
