using NUnit.Framework;

public class PlayerLeftStickCrouchTests
{
    [Test]
    public void LeftStickDown_UsesHysteresisForCrouch()
    {
        Assert.IsFalse(PlayerCrouchInputResolver.Resolve(false, -0.5f, 0.6f, 0.45f));
        Assert.IsTrue(PlayerCrouchInputResolver.Resolve(false, -0.7f, 0.6f, 0.45f));
        Assert.IsTrue(PlayerCrouchInputResolver.Resolve(true, -0.5f, 0.6f, 0.45f));
        Assert.IsFalse(PlayerCrouchInputResolver.Resolve(true, -0.4f, 0.6f, 0.45f));
    }
}
