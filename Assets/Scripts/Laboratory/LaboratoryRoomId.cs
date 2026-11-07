/// <summary>
/// Stable identity of a laboratory room. Serialized numeric values must not be reordered.
/// Rooms after <see cref="WhiteCubes"/> unlock one at a time in declaration order.
/// </summary>
public enum LaboratoryRoomId {
    /// <summary>Spawn room with the scientist and the white-cube machine. Always unlocked.</summary>
    WhiteCubes = 0,
    /// <summary>Room of the colored-cube machine that upgrades the player stats.</summary>
    ColorCubes = 1,
    /// <summary>Room of the player stats screens.</summary>
    Stats = 2,
    /// <summary>Room of the body upgrade machine.</summary>
    Upgrades = 3
}
