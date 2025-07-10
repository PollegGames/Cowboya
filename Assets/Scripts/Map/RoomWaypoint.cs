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
    public string Id { get; private set; }

    // <-- add this:
    /// <summary>All the other RoomWaypoints this one is linked to.</summary>
    public List<RoomWaypoint> Neighbors { get; private set; }

    private void Awake()
    {
        Id = transform.position.ToString("F2") + "_" + type.ToString();
        Neighbors = new List<RoomWaypoint>();
    }

    public override bool Equals(object obj)
    {
        if (obj is RoomWaypoint wp)
            return this.Id == wp.Id; // or compare position if Id is not set
        return false;
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }
}
