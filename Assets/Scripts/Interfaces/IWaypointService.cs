using System.Collections.Generic;
using UnityEngine;

public interface IWaypointService : IWaypointNotifier, IWaypointQueries
{
    void RegisterRoomWaypoints(RoomManager room, IEnumerable<RoomWaypoint> waypoints);
    void UnregisterRoomWaypoints(RoomManager room);

    void BuildAllNeighbors(bool includeUnavailable = false);

    RoomWaypoint GetLeastUsedFreeWorkPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetAnyOnWorkPoint(RoomWaypoint exclude = null);
    FactoryMachine GetAnyOnFactoryMachine();
    RoomWaypoint GetWorkOrRestPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetFirstRestPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetFirstFreeSecurityPoint();
    RoomWaypoint GetSecurityOrRestPoint(RoomWaypoint exclude = null);
    RoomWaypoint GetBlockedRoomSecuritySpawning(RoomWaypoint exclude = null);
    RoomWaypoint GetBlockedRoomCenter(RoomWaypoint exclude = null);
    bool IsPOIReserved(RoomWaypoint poi);
    void ReleaseInvalidReservations();
    void ReleasePOI(RoomWaypoint poi);
    FactoryMachine ReserveFreeMachine(RoomManager room, RobotBrain worker);
    bool ReserveMachine(FactoryMachine machine, RobotBrain worker);
    void ReleaseMachine(FactoryMachine machine);
    bool IsMachineReserved(FactoryMachine machine);
    bool IsMachineReservedFor(FactoryMachine machine, RobotBrain worker);
}
