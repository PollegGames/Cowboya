using System.IO;
using NUnit.Framework;

public class RunSaveStorageTests
{
    private string testDirectory;
    private string savePath;

    [SetUp]
    public void SetUp()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "CowboyaRunSaveTests", System.Guid.NewGuid().ToString());
        savePath = Path.Combine(testDirectory, "run-save.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(testDirectory))
        {
            Directory.Delete(testDirectory, true);
        }
    }

    [Test]
    public void WriteAndLoad_RoundTripsReachedLevel()
    {
        var source = new RunSaveData
        {
            ReachedStepIndex = 4,
            ReachedLevelIndex = 3
        };

        RunSaveStorage.WriteToPath(savePath, source);
        bool loaded = RunSaveStorage.TryLoadFromPath(savePath, out RunSaveData result);

        Assert.IsTrue(loaded);
        Assert.AreEqual(4, result.ReachedStepIndex);
        Assert.AreEqual(3, result.ReachedLevelIndex);
    }

    [Test]
    public void TryLoad_RejectsInvalidCheckpoint()
    {
        Directory.CreateDirectory(testDirectory);
        File.WriteAllText(savePath, "{\"SaveVersion\":1,\"ReachedStepIndex\":-1,\"ReachedLevelIndex\":1}");

        Assert.IsFalse(RunSaveStorage.TryLoadFromPath(savePath, out _));
    }

    [Test]
    public void ResetRunProgress_PreservesProfileAndPermanentData()
    {
        var data = new SaveData
        {
            MaxHealth = 180f,
            CurrentHealth = 12f,
            MaxEnergy = 150f,
            CurrentEnergy = 3f,
            EnergyRechargeRate = 12f,
            AttackEnergyCost = 9f,
            Morality = -40f,
            Gears = 25,
            Volume = 0.8f,
            FullScreen = false
        };
        data.UnlockedCharacters.Add("NinjaBot");

        data.ResetRunProgress();

        Assert.AreEqual(100f, data.MaxHealth);
        Assert.AreEqual(100f, data.CurrentHealth);
        Assert.AreEqual(100f, data.MaxEnergy);
        Assert.AreEqual(100f, data.CurrentEnergy);
        Assert.AreEqual(5f, data.EnergyRechargeRate);
        Assert.AreEqual(5f, data.AttackEnergyCost);
        Assert.AreEqual(0f, data.Morality);
        Assert.AreEqual(25, data.Gears);
        Assert.AreEqual(0.8f, data.Volume);
        Assert.IsFalse(data.FullScreen);
        Assert.Contains("NinjaBot", data.UnlockedCharacters);
    }
}
