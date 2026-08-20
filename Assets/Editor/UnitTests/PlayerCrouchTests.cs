using NUnit.Framework;

public class PlayerArmModeResolverTests
{
    [Test]
    public void Resolve_UsesMostRecentlyPressedHeldMode()
    {
        Assert.AreEqual(
            PlayerArmMode.Attack,
            PlayerArmModeResolver.Resolve(true, 1, true, 2));
        Assert.AreEqual(
            PlayerArmMode.Grab,
            PlayerArmModeResolver.Resolve(true, 3, true, 2));
    }

    [Test]
    public void Resolve_ReturnsToOtherModeWhenWinnerIsReleased()
    {
        Assert.AreEqual(
            PlayerArmMode.Grab,
            PlayerArmModeResolver.Resolve(true, 1, false, 2));
        Assert.AreEqual(
            PlayerArmMode.Attack,
            PlayerArmModeResolver.Resolve(false, 2, true, 1));
    }

    [Test]
    public void Resolve_ReturnsRestWhenNeitherModeIsHeld()
    {
        Assert.AreEqual(
            PlayerArmMode.Rest,
            PlayerArmModeResolver.Resolve(false, 0, false, 0));
    }

    [Test]
    public void Resolve_AllowsDifferentModesOnBothArmsAtOnce()
    {
        PlayerArmMode leftMode = PlayerArmModeResolver.Resolve(true, 1, false, 0);
        PlayerArmMode rightMode = PlayerArmModeResolver.Resolve(false, 0, true, 2);

        Assert.AreEqual(PlayerArmMode.Grab, leftMode);
        Assert.AreEqual(PlayerArmMode.Attack, rightMode);
    }
}
