using UnityEngine;

/// <summary>
/// Exposes movement data for a platform that can carry riders.
/// Attach an implementation (e.g., MovingPlatform2D) to any moving platform root.
/// </summary>
public interface IMovingPlatform2D
{
    /// <summary>
    /// World-space delta position since the last physics step.
    /// </summary>
    Vector2 DeltaPosition { get; }

    /// <summary>
    /// World-space velocity estimated from the last physics step.
    /// </summary>
    Vector2 Velocity { get; }
}

