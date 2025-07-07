using UnityEngine;

public interface IRobotMemory
{
    Vector3 LastKnownPlayerPosition { get; }
    bool WasRecentlyAttacked { get; }
    RoomWaypoint LastVisitedPoint { get; }

    void SetRespawnService(IRobotRespawnService service);
    void SetLastVisitedPoint(RoomWaypoint point);
    void OnStuck(EnemyWorkerController controller);
    void OnBossStuck(EnemyController controller);
    void RememberPlayerPosition(Vector3 playerPosition);
    void ClearPlayerPosition();
    void RegisterAttack();
    void ResetAttackMemory();
}
