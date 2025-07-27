using UnityEngine;

/// <summary>
/// Holds metadata about room edges for door and lift initialization.
/// </summary>
public class RoomProperties : MonoBehaviour
{
    [Header("Edge Properties")]
    public bool HasLeftDoor { get; set; }
    public bool HasRightDoor { get; set; }
    public bool HasLiftUp { get; set; }
    public bool HasLiftDown { get; set; }
    public bool HasRightDoorLocked { get; set; } 
    public bool HasLeftDoorLocked { get; set; }
    public bool IsVictoryDoorLeft { get; set; } = false;
    public bool IsVictoryDoorRight { get; set; } = false;
    public UsageType usageType;
    public POIType poiType;


    /// <summary>
    /// The grid position of the target.
    /// </summary>
    public Vector2Int GridPosition { get; set; }


}
