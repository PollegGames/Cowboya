using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class GeneratedWorkerPopulationTests
{
    [Test]
    public void Calculate_AddsWorkersAndCollectorsFromRoomRoles()
    {
        WorkerPopulation population = GeneratedWorkerPopulation.Calculate(
            workRooms: 4,
            restingRooms: 2,
            garages: 1,
            conveyors: 1);

        Assert.AreEqual(6, population.Workers);
        Assert.AreEqual(3, population.WorkerCollectors);
        Assert.IsTrue(population.WorkerCollectorsRestAfterBatch);
    }

    [Test]
    public void Calculate_RequiresCompleteGarageConveyorPairs()
    {
        WorkerPopulation population = GeneratedWorkerPopulation.Calculate(
            workRooms: 1,
            restingRooms: 0,
            garages: 3,
            conveyors: 2);

        Assert.AreEqual(1, population.Workers);
        Assert.AreEqual(2, population.WorkerCollectors);
    }

    [Test]
    public void Calculate_TreatsNegativeCountsAsZero()
    {
        WorkerPopulation population = GeneratedWorkerPopulation.Calculate(-1, -2, -3, -4);

        Assert.AreEqual(0, population.Workers);
        Assert.AreEqual(0, population.WorkerCollectors);
    }

    [TestCase("Assets/Scripts/ScriptObjects/Setup Level 4.asset", 3, 3, 0, 1, "867059")]
    [TestCase("Assets/Scripts/ScriptObjects/Setup Level 5.asset", 6, 4, 2, 1, "712226")]
    public void FixedGeneratedLevels_HaveExpectedWorkerPopulation(
        string assetPath, int poiCount, int totalWorkers, int collectors, int enemies, string seed)
    {
        RunMapConfigSO config = AssetDatabase.LoadAssetAtPath<RunMapConfigSO>(assetPath);

        Assert.IsNotNull(config);
        Assert.AreEqual(poiCount, config.poiCount);
        Assert.AreEqual(totalWorkers, config.workersCount);
        Assert.AreEqual(collectors, config.workerCollectorsCount);
        Assert.AreEqual(enemies, config.enemiesCount);
        Assert.AreEqual(seed, config.seed);
    }

    [Test]
    public void RunProgression_EndsFixedGenerationAtLevelFive()
    {
        const string prefabPath = "Assets/Resources/Prefabs/Setup/RunProgressManager.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        RunProgressManager manager = prefab != null ? prefab.GetComponent<RunProgressManager>() : null;
        Assert.IsNotNull(manager, prefabPath);

        FieldInfo field = typeof(RunProgressManager).GetField(
            "runSteps", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        var steps = field.GetValue(manager) as List<RunStepDefinition>;
        Assert.IsNotNull(steps);
        Assert.AreEqual(10, steps.Count);
        Assert.AreEqual(RunStepKind.GeneratedLevel, steps[8].Kind);
        Assert.AreEqual("Setup Level 5", steps[8].GeneratedConfig.name);
        Assert.AreEqual(RunStepKind.Laboratory, steps[9].Kind);
        Assert.AreEqual(2, steps.FindAll(step =>
            step.Kind == RunStepKind.GeneratedLevel && step.GeneratedConfig != null).Count);
    }

    [Test]
    public void DynamicLevelSix_UsesFormerLevelSevenGrowthStep()
    {
        RunMapConfigSO baseConfig = ScriptableObject.CreateInstance<RunMapConfigSO>();
        baseConfig.gridWidth = 3;
        baseConfig.gridHeight = 4;
        baseConfig.poiCount = 6;
        baseConfig.workersCount = 4;
        baseConfig.enemiesCount = 1;

        MethodInfo method = typeof(RunProgressManager).GetMethod(
            "CreateDynamicConfig", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        RunMapConfigSO levelSix = null;
        RunMapConfigSO levelSeven = null;
        try
        {
            Random.InitState(12345);
            levelSix = method.Invoke(null, new object[] { baseConfig, 6 }) as RunMapConfigSO;
            Assert.IsNotNull(levelSix);
            Assert.AreEqual(8, levelSix.gridWidth + levelSix.gridHeight,
                "Former Level 7 increased one grid dimension by one.");
            Assert.AreEqual(7, levelSix.poiCount);
            Assert.AreEqual(5, levelSix.workersCount);
            Assert.AreEqual(2, levelSix.enemiesCount);

            levelSeven = method.Invoke(null, new object[] { levelSix, 7 }) as RunMapConfigSO;
            Assert.IsNotNull(levelSeven);
            Assert.AreEqual(9, levelSeven.gridWidth + levelSeven.gridHeight);
            Assert.AreEqual(10, levelSeven.poiCount,
                "New Level 7 must receive the former Level 8 growth increment.");
            Assert.AreEqual(8, levelSeven.workersCount);
            Assert.AreEqual(5, levelSeven.enemiesCount);
        }
        finally
        {
            Object.DestroyImmediate(levelSeven);
            Object.DestroyImmediate(levelSix);
            Object.DestroyImmediate(baseConfig);
        }
    }
}
