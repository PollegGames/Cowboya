using UnityEngine;

/// <summary>
/// Plain data container for the Memory pillar to store factual observations.
/// </summary>
public class RobotMemoryState
{
    public Vector3 LastKnownPlayerPosition { get; private set; }
    public float TimeSincePlayerLastSeen { get; private set; }
    public bool WasRecentlyAttacked { get; private set; }
    public float TimeSinceLastAttack { get; private set; }
    public RoomWaypoint LastVisitedPoint { get; private set; }
    public IRobotRespawnService RespawnService { get; private set; }

    public void Tick(float deltaTime)
    {
        if (LastKnownPlayerPosition != Vector3.zero)
            TimeSincePlayerLastSeen += deltaTime;
        if (WasRecentlyAttacked)
            TimeSinceLastAttack += deltaTime;
    }

    public void RememberPlayerPosition(Vector3 position)
    {
        LastKnownPlayerPosition = position;
        TimeSincePlayerLastSeen = 0f;
    }

    public void ClearPlayerPosition()
    {
        if (WasRecentlyAttacked)
            return;
        LastKnownPlayerPosition = Vector3.zero;
        TimeSincePlayerLastSeen = 0f;
    }

    public void RegisterAttack()
    {
        WasRecentlyAttacked = true;
        TimeSinceLastAttack = 0f;
    }

    public void ResetAttackMemory()
    {
        WasRecentlyAttacked = false;
        TimeSinceLastAttack = 0f;
    }

    public void SetLastVisitedPoint(RoomWaypoint point)
    {
        LastVisitedPoint = point;
    }

    public void SetRespawnService(IRobotRespawnService service)
    {
        RespawnService = service;
    }

    public void ResetAll()
    {
        LastKnownPlayerPosition = Vector3.zero;
        TimeSincePlayerLastSeen = 0f;
        WasRecentlyAttacked = false;
        TimeSinceLastAttack = 0f;
        LastVisitedPoint = null;
        RespawnService = null;
    }
}
