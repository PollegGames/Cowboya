using NUnit.Framework;
using UnityEditor;

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

    [TestCase("Assets/Scripts/ScriptObjects/Setup Level 4.asset", 3, 0, 0, "282899")]
    [TestCase("Assets/Scripts/ScriptObjects/Setup Level 5.asset", 3, 0, 1, "867059")]
    [TestCase("Assets/Scripts/ScriptObjects/Setup Level 6.asset", 4, 2, 1, "712226")]
    public void FixedGeneratedLevels_HaveExpectedWorkerPopulation(
        string assetPath, int totalWorkers, int collectors, int enemies, string seed)
    {
        RunMapConfigSO config = AssetDatabase.LoadAssetAtPath<RunMapConfigSO>(assetPath);

        Assert.IsNotNull(config);
        Assert.AreEqual(totalWorkers, config.workersCount);
        Assert.AreEqual(collectors, config.workerCollectorsCount);
        Assert.AreEqual(enemies, config.enemiesCount);
        Assert.AreEqual(seed, config.seed);
    }
}
