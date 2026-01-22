using UnityEngine;

/// <summary>
/// Represents the direction and target information for an attack attempt.
/// </summary>
public enum AttackSector
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// Describes a request to perform an attack, including aiming and energy data.
/// </summary>
public readonly struct AttackRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AttackRequest"/> struct.
    /// </summary>
    /// <param name="targetPosition">World position the attack should target.</param>
    /// <param name="sector">Directional sector of the attack.</param>
    /// <param name="energyRequired">Energy required to execute the attack.</param>
    public AttackRequest(Vector2 targetPosition, AttackSector sector, float energyRequired)
    {
        TargetPosition = targetPosition;
        Sector = sector;
        EnergyRequired = energyRequired;
    }

    /// <summary>
    /// Gets the world position the attack should target.
    /// </summary>
    public Vector2 TargetPosition { get; }

    /// <summary>
    /// Gets the directional sector associated with the request.
    /// </summary>
    public AttackSector Sector { get; }

    /// <summary>
    /// Gets the amount of energy required to execute the attack.
    /// </summary>
    public float EnergyRequired { get; }
}
