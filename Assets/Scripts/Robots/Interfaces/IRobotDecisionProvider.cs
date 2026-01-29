using UnityEngine;

/// <summary>
/// Provides high-level decision data for a robot including how it wishes to move,
/// face, and whether it wants to execute an attack.
/// </summary>
public interface IRobotDecisionProvider
{
    /// <summary>
    /// Gets the desired movement input, typically ranging from -1 to 1 per axis.
    /// </summary>
    Vector2 Movement { get; }

    /// <summary>
    /// Gets the desired facing direction for aiming and mirroring purposes.
    /// </summary>
    Vector2 DesiredFacing { get; }

    /// <summary>
    /// Attempts to build an attack request for the current frame.
    /// </summary>
    /// <param name="request">Request describing the desired attack.</param>
    /// <returns><c>true</c> when an attack should be executed.</returns>
    bool TryBuildAttackRequest(out AttackRequest request);
}
