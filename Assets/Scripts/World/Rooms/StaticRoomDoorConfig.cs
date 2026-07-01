using UnityEngine;

public enum StaticDoorState
{
    Wall,
    Unlocked,
    LockedRequiresBadge,
    LocksOnAlarm,
    VictoryExit
}

public class StaticRoomDoorConfig : MonoBehaviour
{
    [SerializeField] private StaticDoorState leftDoor = StaticDoorState.Unlocked;
    [SerializeField] private StaticDoorState rightDoor = StaticDoorState.Unlocked;

    public void Apply(RoomManager room)
    {
        if (room == null || room.roomProperties == null)
        {
            Debug.LogWarning($"StaticRoomDoorConfig '{name}' cannot apply because room or roomProperties is missing.", this);
            return;
        }

        ApplyToProperties(room.roomProperties, DoorDirection.Left, leftDoor);
        ApplyToProperties(room.roomProperties, DoorDirection.Right, rightDoor);
        ApplyToDoorControllers(room);
    }

    private void ApplyToProperties(RoomProperties properties, DoorDirection direction, StaticDoorState state)
    {
        bool hasDoor = state != StaticDoorState.Wall;
        bool requiresBadge = state == StaticDoorState.LockedRequiresBadge;
        bool isVictory = state == StaticDoorState.VictoryExit;

        if (direction == DoorDirection.Left)
        {
            properties.HasLeftDoor = hasDoor;
            properties.HasLeftDoorLocked = requiresBadge;
            properties.IsVictoryDoorLeft = isVictory;
        }
        else
        {
            properties.HasRightDoor = hasDoor;
            properties.HasRightDoorLocked = requiresBadge;
            properties.IsVictoryDoorRight = isVictory;
        }
    }

    private void ApplyToDoorControllers(RoomManager room)
    {
        DoorController[] doors = room.GetComponentsInChildren<DoorController>(includeInactive: true);
        foreach (DoorController door in doors)
        {
            if (door == null)
                continue;

            StaticDoorState state = door.moveDirection.x < 0f ? leftDoor : rightDoor;
            door.alarmLocksDoor = state == StaticDoorState.LocksOnAlarm;
            door.RefreshFromRoomProperties();
        }
    }
}
