using System;
using System.Collections.Generic;
using UnityEngine;

public enum WaypointType { Door, LiftGoingUp, LiftGoingDown, Rest, Work, Center, Security }

[Serializable]
public class RoomWaypoint : MonoBehaviour
{
    public WaypointType type;
    public RoomManager parentRoom;
    public bool IsAvailable;
    public Vector3 WorldPos => transform.position;

    // unique identifier
    public int Id { get; private set; }

    // <-- add this:
    /// <summary>All the other RoomWaypoints this one is linked to.</summary>
    public List<RoomWaypoint> Neighbors { get; private set; }

    private void Awake()
    {
        Id = GetInstanceID();
        Neighbors = new List<RoomWaypoint>();
    }

    public override bool Equals(object obj)
    {
        return obj is RoomWaypoint wp && wp.Id == Id;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
