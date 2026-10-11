using NUnit.Framework;
using UnityEngine;

public class VictorySetupTests
{
    private VictorySetup setup;

    [SetUp]
    public void SetUp()
    {
        setup = ScriptableObject.CreateInstance<VictorySetup>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(setup);
    }

    [Test]
    public void HasMetAnyGoal_ZeroTargetsMakeExitAvailable()
    {
        Assert.IsTrue(setup.HasMetAnyGoal());
    }

    [TestCase(1)]
    [TestCase(6)]
    public void ConfigureForLevel_FirstSixLevelsHaveNoTargets(int level)
    {
        setup.ConfigureForLevel(level, 10f);

        Assert.AreEqual(0, setup.robotsSavedTarget);
        Assert.AreEqual(0, setup.robotsKilledTarget);
    }

    [TestCase(7, 0f, 3, 1)]
    [TestCase(7, 10f, 3, 1)]
    [TestCase(7, -10f, 2, 2)]
    [TestCase(8, 10f, 4, 2)]
    [TestCase(8, -10f, 3, 3)]
    [TestCase(10, 10f, 6, 4)]
    public void ConfigureForLevel_GrowsTargetsAndAppliesOneMoralityBonus(
        int level,
        float morality,
        int expectedSaved,
        int expectedKilled)
    {
        setup.ConfigureForLevel(level, morality);

        Assert.AreEqual(expectedSaved, setup.robotsSavedTarget);
        Assert.AreEqual(expectedKilled, setup.robotsKilledTarget);
    }

    [TestCase(1, 0, 1, 0)]
    [TestCase(0, 1, 0, 1)]
    public void HasMetAnyGoal_EitherEnabledGoalCanCompleteLevel(
        int savedTarget,
        int killedTarget,
        int currentSaved,
        int currentKilled)
    {
        setup.robotsSavedTarget = savedTarget;
        setup.robotsKilledTarget = killedTarget;
        setup.currentSaved = currentSaved;
        setup.currentKilled = currentKilled;

        Assert.IsTrue(setup.HasMetAnyGoal());
    }

    [Test]
    public void HasMetAnyGoal_RemainsFalseBeforeBothGoals()
    {
        setup.robotsSavedTarget = 2;
        setup.robotsKilledTarget = 2;
        setup.currentSaved = 1;
        setup.currentKilled = 1;

        Assert.IsFalse(setup.HasMetAnyGoal());
    }
}
