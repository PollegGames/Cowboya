using UnityEngine;

public interface IRobotMemory
{
    Vector3 LastKnownPlayerPosition { get; }
    bool PlayerInAttackZone { get; }
    bool CanSeePlayer { get; }
    bool WasRecentlyAttacked { get; }
    RoomWaypoint LastVisitedPoint { get; }

    void SetRespawnService(IRobotRespawnService service);
    void SetLastVisitedPoint(RoomWaypoint point);
    void RememberPlayerPosition(Vector3 playerPosition);
    void ClearPlayerPosition();
    void SetPlayerInAttackZone(bool inZone);
    void SetCanSeePlayer(bool canSee);
    void RegisterAttack();
    void ResetAttackMemory();
}
