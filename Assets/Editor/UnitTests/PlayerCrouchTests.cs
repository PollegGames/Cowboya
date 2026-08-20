using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PlayerCrouchInputTests
{
    [Test]
    public void RightStickDown_UsesPressAndReleaseThresholds()
    {
        var playerObject = new GameObject("Player input test");
        PlayerInputReader reader = playerObject.AddComponent<PlayerInputReader>();
        MethodInfo updateCrouch = typeof(PlayerInputReader).GetMethod(
            "UpdateGamepadCrouch",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(updateCrouch);

        updateCrouch.Invoke(reader, new object[] { -0.5f });
        Assert.IsFalse(reader.CrouchHeld);

        updateCrouch.Invoke(reader, new object[] { -0.7f });
        Assert.IsTrue(reader.CrouchHeld);

        updateCrouch.Invoke(reader, new object[] { -0.5f });
        Assert.IsTrue(reader.CrouchHeld);

        updateCrouch.Invoke(reader, new object[] { -0.4f });
        Assert.IsFalse(reader.CrouchHeld);

        Object.DestroyImmediate(playerObject);
    }
}

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
