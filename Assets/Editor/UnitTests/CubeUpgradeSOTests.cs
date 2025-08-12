using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CubeUpgradeSOTests
{
    [Test]
    public void ApplyUpgradeUpdatesEnergyRecharge()
    {
        var upgradeSO = ScriptableObject.CreateInstance<CubeUpgradeSO>();
        typeof(CubeUpgradeSO)
            .GetField("selectedUpgrade", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(upgradeSO, CubeUpgradeType.EnergyRecharge);

        var stats = new RobotStats();
        stats.EnergyRechargeRate = 1f;

        upgradeSO.ApplyUpgrade(stats);

        Assert.AreEqual(1f + upgradeSO.UpgradeEnergyRechargeValue, stats.EnergyRechargeRate);
    }
}
