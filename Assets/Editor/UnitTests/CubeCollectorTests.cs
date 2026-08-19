using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class CubeCollectorTests {
    private GameObject collectorObject;
    private CubeCollector collector;

    [SetUp]
    public void SetUp() {
        collectorObject = new GameObject("collector");
        collectorObject.AddComponent<BoxCollider2D>();
        collector = collectorObject.AddComponent<CubeCollector>();
    }

    [TearDown]
    public void TearDown() {
        if (collectorObject != null) {
            Object.DestroyImmediate(collectorObject);
        }

        CubePickup[] remainingCubes = Object.FindObjectsByType<CubePickup>(FindObjectsSortMode.None);
        for (int i = 0; i < remainingCubes.Length; i++) {
            if (remainingCubes[i] != null) {
                Object.DestroyImmediate(remainingCubes[i].gameObject);
            }
        }
    }

    [Test]
    public void CollectorStoresColoredCubeForLaboratory() {
        LaboratoryProgress progress = new LaboratoryProgress();
        CubePickup pickup = CreateCube(CubeUpgradeType.AttackDamage);

        bool collected = collector.TryCollectForLaboratory(pickup, progress);

        Assert.IsTrue(collected);
        Assert.AreEqual(1, progress.GetIncomingCubeCount(LaboratoryCubeType.AttackDamage));
        Assert.IsTrue(pickup == null);
    }

    [Test]
    public void CollectorStoresNormalCubeAsWhite() {
        LaboratoryProgress progress = new LaboratoryProgress();
        CubePickup pickup = CreateCube();

        bool collected = collector.TryCollectForLaboratory(pickup, progress);

        Assert.IsTrue(collected);
        Assert.AreEqual(1, progress.GetIncomingCubeCount(LaboratoryCubeType.White));
        Assert.IsTrue(pickup == null);
    }

    [Test]
    public void CollectorPreservesRepeatedAndMixedCubeCounts() {
        LaboratoryProgress progress = new LaboratoryProgress();

        Assert.IsTrue(collector.TryCollectForLaboratory(
            CreateCube(CubeUpgradeType.MaxHealth),
            progress));
        Assert.IsTrue(collector.TryCollectForLaboratory(
            CreateCube(CubeUpgradeType.MaxHealth),
            progress));
        Assert.IsTrue(collector.TryCollectForLaboratory(
            CreateCube(CubeUpgradeType.MaxEnergy),
            progress));

        Assert.AreEqual(2, progress.GetIncomingCubeCount(LaboratoryCubeType.MaxHealth));
        Assert.AreEqual(1, progress.GetIncomingCubeCount(LaboratoryCubeType.MaxEnergy));
        Assert.AreEqual(0, progress.GetIncomingCubeCount(LaboratoryCubeType.EnergyRecharge));
    }

    [Test]
    public void CollectorLeavesCubeWhenProgressIsMissing() {
        CubePickup pickup = CreateCube(CubeUpgradeType.EnergyRecharge);

        bool collected = collector.TryCollectForLaboratory(pickup, null);

        Assert.IsFalse(collected);
        Assert.IsNotNull(pickup);
    }

    [Test]
    public void CollectorRejectsUnknownUpgradeWithoutDestroyingCube() {
        LaboratoryProgress progress = new LaboratoryProgress();
        CubePickup pickup = CreateCube((CubeUpgradeType)999);

        bool collected = collector.TryCollectForLaboratory(pickup, progress);

        Assert.IsFalse(collected);
        Assert.IsNotNull(pickup);
        Assert.AreEqual(0, progress.GetIncomingCubeCount(LaboratoryCubeType.White));
    }

    [Test]
    public void CubeCollecteurPrefab_DefaultsToLaboratoryStorageIncludingWhite() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Prefabs/Map/Basic/Machines/CubeCollecteur.prefab");

        Assert.IsNotNull(prefab);
        CubeCollector prefabCollector = prefab.GetComponentInChildren<CubeCollector>(true);
        Assert.IsNotNull(prefabCollector);

        SerializedObject serializedCollector = new SerializedObject(prefabCollector);
        Assert.AreEqual(
            (int)CubeCollectionMode.LaboratoryStorage,
            serializedCollector.FindProperty("collectionMode").enumValueIndex);
        Assert.IsTrue(
            serializedCollector.FindProperty("collectNormalCubesAsWhite").boolValue);
    }

    private static CubePickup CreateCube(CubeUpgradeType? upgradeType = null) {
        GameObject cubeObject = new GameObject("cube");
        CubePickup pickup = cubeObject.AddComponent<CubePickup>();
        cubeObject.AddComponent<BoxCollider2D>();

        if (upgradeType.HasValue) {
            CubeUpgrade upgrade = cubeObject.AddComponent<CubeUpgrade>();
            typeof(CubeUpgrade)
                .GetField("upgradeType", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(upgrade, upgradeType.Value);
        }

        return pickup;
    }
}
