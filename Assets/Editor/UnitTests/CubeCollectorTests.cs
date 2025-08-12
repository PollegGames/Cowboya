using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class CubeCollectorTests
{
    [Test]
    public void CollectorStoresUpgradeFromCube()
    {
        var upgradeSO = ScriptableObject.CreateInstance<CubeUpgradeSO>();

        var collectorGO = new GameObject("collector");
        collectorGO.AddComponent<BoxCollider2D>();
        var collector = collectorGO.AddComponent<CubeCollector>();
        typeof(CubeCollector)
            .GetField("upgradeStore", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(collector, upgradeSO);

        var cubeGO = new GameObject("cube");
        cubeGO.AddComponent<Rigidbody2D>();
        cubeGO.AddComponent<TargetJoint2D>();
        var pickup = cubeGO.AddComponent<CubePickup>();
        var upgrade = cubeGO.AddComponent<CubeUpgrade>();
        var cubeCollider = cubeGO.AddComponent<BoxCollider2D>();

        typeof(CubeUpgrade)
            .GetField("upgradeType", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(upgrade, CubeUpgradeType.AttackDamage);

        var method = typeof(CubeCollector)
            .GetMethod("OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
        method.Invoke(collector, new object[] { cubeCollider });

        Assert.AreEqual(CubeUpgradeType.AttackDamage, upgradeSO.SelectedUpgrade);
        Assert.IsTrue(pickup == null);
    }
}

