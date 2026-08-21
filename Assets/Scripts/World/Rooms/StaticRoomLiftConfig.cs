using UnityEngine;

/// <summary>
/// Configures lift connections for a room placed in a static scene.
/// </summary>
public class StaticRoomLiftConfig : MonoBehaviour
{
    [SerializeField] private bool liftUp;
    [SerializeField] private bool liftDown;

    /// <summary>
    /// Applies the configured lift connections to the room's runtime properties.
    /// </summary>
    public void Apply(RoomManager room)
    {
        if (room == null || room.roomProperties == null)
        {
            Debug.LogWarning($"StaticRoomLiftConfig '{name}' cannot apply because room or roomProperties is missing.", this);
            return;
        }

        room.roomProperties.HasLiftUp = liftUp;
        room.roomProperties.HasLiftDown = liftDown;
    }
}
