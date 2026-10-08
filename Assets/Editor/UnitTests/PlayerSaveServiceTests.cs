using System.IO;
using NUnit.Framework;

public class PlayerSaveServiceTests
{
    private string testDirectory;
    private string savePath;

    [SetUp]
    public void SetUp()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), "CowboyaSaveTests", System.Guid.NewGuid().ToString());
        savePath = Path.Combine(testDirectory, "save.json");
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
    public void WriteAndLoad_RoundTripsAllSaveFieldTypes()
    {
        var source = new SaveData
        {
            MaxHealth = 175f,
            CurrentHealth = 123f,
            MaxEnergy = 140f,
            CurrentEnergy = 91f,
            EnergyRechargeRate = 8f,
            AttackEnergyCost = 12f,
            Morality = -15f,
            Gears = 42,
            LastChosenCharacter = "NinjaBot",
            Volume = 0.75f,
            FullScreen = false
        };
        source.UnlockedAttacks.Add("Uppercut");
        source.AttackOrder.Add("Uppercut");
        source.CollectedRobotParts.Add("Arm");
        source.UnlockedCharacters.Add("NinjaBot");
        source.SpecialResources["Power Core"] = 3;
        source.MoralAlignmentInfluences["MercyKillUnlocked"] = true;

        PlayerSaveService.WriteSaveToPath(savePath, source);
        bool loaded = PlayerSaveService.TryLoadFromPath(savePath, out SaveData result);

        Assert.IsTrue(loaded);
        Assert.AreEqual(SaveData.CurrentVersion, result.SaveVersion);
        Assert.AreEqual(175f, result.MaxHealth);
        Assert.AreEqual(123f, result.CurrentHealth);
        Assert.AreEqual(140f, result.MaxEnergy);
        Assert.AreEqual(91f, result.CurrentEnergy);
        Assert.AreEqual(8f, result.EnergyRechargeRate);
        Assert.AreEqual(12f, result.AttackEnergyCost);
        Assert.AreEqual(-15f, result.Morality);
        Assert.AreEqual(42, result.Gears);
        Assert.Contains("Uppercut", result.UnlockedAttacks);
        Assert.Contains("Arm", result.CollectedRobotParts);
        Assert.Contains("NinjaBot", result.UnlockedCharacters);
        Assert.AreEqual("NinjaBot", result.LastChosenCharacter);
        Assert.AreEqual(3, result.SpecialResources["Power Core"]);
        Assert.IsTrue(result.MoralAlignmentInfluences["MercyKillUnlocked"]);
        Assert.AreEqual(0.75f, result.Volume);
        Assert.IsFalse(result.FullScreen);
    }

    [Test]
    public void WriteSave_CreatesBackupThatCanRecoverCorruptPrimary()
    {
        var first = new SaveData { Gears = 10 };
        PlayerSaveService.WriteSaveToPath(savePath, first);

        var second = new SaveData { Gears = 20 };
        PlayerSaveService.WriteSaveToPath(savePath, second);
        File.WriteAllText(savePath, "{corrupt-json");

        Assert.IsFalse(PlayerSaveService.TryLoadFromPath(savePath, out _));
        Assert.IsTrue(PlayerSaveService.TryLoadFromPath(savePath + ".bak", out SaveData recovered));
        Assert.AreEqual(10, recovered.Gears);
    }

    [Test]
    public void Normalize_RepairsUnsafeValuesFromOlderSave()
    {
        var data = new SaveData
        {
            SaveVersion = 0,
            SaveName = null,
            MaxHealth = 0f,
            CurrentHealth = 500f,
            MaxEnergy = -10f,
            CurrentEnergy = 20f,
            EnergyRechargeRate = -1f,
            Volume = 2f
        };

        data.Normalize();

        Assert.AreEqual(SaveData.CurrentVersion, data.SaveVersion);
        Assert.IsNotEmpty(data.SaveName);
        Assert.AreEqual(1f, data.MaxHealth);
        Assert.AreEqual(1f, data.CurrentHealth);
        Assert.AreEqual(0f, data.MaxEnergy);
        Assert.AreEqual(0f, data.CurrentEnergy);
        Assert.AreEqual(0f, data.EnergyRechargeRate);
        Assert.AreEqual(1f, data.Volume);
    }
}
