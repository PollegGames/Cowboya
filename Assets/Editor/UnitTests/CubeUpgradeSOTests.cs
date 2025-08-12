using NUnit.Framework;
using UnityEngine;

public class CubeUpgradeSOTests
{
    [Test]
    public void ApplyUpgrade_IncreasesEnergyRechargeRate()
    {
        var upgradeSO = ScriptableObject.CreateInstance<CubeUpgradeSO>();
        upgradeSO.Store(CubeUpgradeType.EnergyRecharge);

        var stats = new RobotStats();
        float initialRate = stats.EnergyRechargeRate;

        upgradeSO.ApplyUpgrade(stats);

        Assert.AreEqual(initialRate + upgradeSO.UpgradeEnergyRechargeValue, stats.EnergyRechargeRate);
    }
}
