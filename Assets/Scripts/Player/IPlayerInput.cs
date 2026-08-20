using UnityEngine;

public interface IPlayerInput
{
    Vector2 Movement { get; }
    Vector2 Aim { get; }
    bool AimIsScreenPosition { get; }
    bool JumpPressed { get; }
    bool JumpDown { get; }
    bool CrouchHeld { get; }

    bool LeftGrabDown { get; }
    bool LeftGrabHeld { get; }
    bool LeftGrabUp { get; }
    uint LeftGrabPressSequence { get; }

    bool RightGrabDown { get; }
    bool RightGrabHeld { get; }
    bool RightGrabUp { get; }
    uint RightGrabPressSequence { get; }

    bool LeftAttackDown { get; }
    bool LeftAttackHeld { get; }
    bool LeftAttackUp { get; }
    uint LeftAttackPressSequence { get; }

    bool RightAttackDown { get; }
    bool RightAttackHeld { get; }
    bool RightAttackUp { get; }
    uint RightAttackPressSequence { get; }
}

/// <summary>
/// The mutually exclusive intent currently controlling one player arm.
/// </summary>
public enum PlayerArmMode
{
    Rest,
    Grab,
    Attack
}

/// <summary>
/// Resolves one arm's held inputs using the most recent press sequence.
/// </summary>
public static class PlayerArmModeResolver
{
    /// <summary>
    /// Returns the winning mode, falling back to the other held mode when the winner is released.
    /// </summary>
    public static PlayerArmMode Resolve(
        bool grabHeld,
        uint grabPressSequence,
        bool attackHeld,
        uint attackPressSequence)
    {
        if (!grabHeld)
            return attackHeld ? PlayerArmMode.Attack : PlayerArmMode.Rest;

        if (!attackHeld)
            return PlayerArmMode.Grab;

        return attackPressSequence > grabPressSequence
            ? PlayerArmMode.Attack
            : PlayerArmMode.Grab;
    }
}
