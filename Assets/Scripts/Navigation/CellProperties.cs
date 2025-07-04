using UnityEngine;

/// <summary>
/// Holds metadata about room edges for door and lift initialization.
/// </summary>
public class CellProperties
{
    [Header("Edge Properties")]
    public bool HasLeftDoor { get; set; } = true;
    public bool HasRightDoor { get; set; } = true;
    public bool HasLeftDoorLocked { get; set; } = false;
    public bool HasRightDoorLocked { get; set; } = false;
    public bool HasLiftUpBlocked { get; set; } = false;
    public bool HasLiftDownBlocked { get; set; } = false;
    public UsageType usageType;
    public POIType poiType;
    public Vector2Int GridPosition { get; set; }
}
