using System.Collections.Generic;
using UnityEngine;

public interface IRobotMemoryNew
{
    Vector3 LastKnownPlayerPosition { get; }
    RoomWaypoint LastKnownPlayerWaypoint { get; }
    bool PlayerInAttackZone { get; }
    bool PlayerInDetectZone { get; }
    bool WasRecentlyAttacked { get; }
    Vector3 LastAttackPosition { get; }
    bool HasLastAttackPosition { get; }
    RoomWaypoint LastVisitedPoint { get; }
    Dictionary<RoomWaypoint, bool> AllAvailableWaypoints { get; }

    void SetLastVisitedPoint(RoomWaypoint point);
    void RememberPlayerPosition(Vector3 playerPosition);
    void RememberPlayerWaypoint(RoomWaypoint playerWaypoint, Vector3 playerPosition);
    void ClearPlayerPosition();
    void SetPlayerInAttackZone(bool inZone);
    void SetPlayerInDetectZone(bool inZone);
    void RegisterAttack();
    void RegisterAttack(Vector3 attackerPosition);
    void ResetAttackMemory();
}
