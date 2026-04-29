using System;
using UnityEngine;

public enum RoomMachineEventKind
{
    PowerChanged,
    OccupancyChanged,
    TurnedOff
}

public enum RoomThreatSource
{
    Unknown,
    SecurityCamera,
    MachineSystem
}

public readonly struct RoomMachineChangedEvent
{
    public RoomMachineChangedEvent(
        RoomManager room,
        BaseMachine machine,
        RoomMachineEventKind eventKind,
        bool? isOn,
        bool? isOccupied,
        GameObject robot,
        GameObject previousRobot)
    {
        Room = room;
        Machine = machine;
        EventKind = eventKind;
        IsOn = isOn;
        IsOccupied = isOccupied;
        Robot = robot;
        PreviousRobot = previousRobot;
    }

    public RoomManager Room { get; }
    public BaseMachine Machine { get; }
    public RoomMachineEventKind EventKind { get; }
    public bool? IsOn { get; }
    public bool? IsOccupied { get; }
    public GameObject Robot { get; }
    public GameObject PreviousRobot { get; }
}

public readonly struct RoomThreatChangedEvent
{
    public RoomThreatChangedEvent(
        RoomManager room,
        AlarmState desiredAlarmState,
        RoomThreatSource source,
        bool hasKnownPlayerPosition,
        Vector3 knownPlayerPosition)
    {
        Room = room;
        DesiredAlarmState = desiredAlarmState;
        Source = source;
        HasKnownPlayerPosition = hasKnownPlayerPosition;
        KnownPlayerPosition = knownPlayerPosition;
    }

    public RoomManager Room { get; }
    public AlarmState DesiredAlarmState { get; }
    public RoomThreatSource Source { get; }
    public bool HasKnownPlayerPosition { get; }
    public Vector3 KnownPlayerPosition { get; }
}

public sealed class RoomEventHub
{
    public event Action<RoomMachineChangedEvent> OnRoomMachineChanged;
    public event Action<RoomThreatChangedEvent> OnRoomThreatChanged;

    public void PublishMachineChanged(RoomMachineChangedEvent evt)
    {
        OnRoomMachineChanged?.Invoke(evt);
    }

    public void PublishThreatChanged(RoomThreatChangedEvent evt)
    {
        OnRoomThreatChanged?.Invoke(evt);
    }
}
