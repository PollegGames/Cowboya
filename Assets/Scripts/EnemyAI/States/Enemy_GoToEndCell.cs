using System.Linq;
using UnityEngine;

public class Enemy_GoToEndCell : EnemyState
{
    private RoomWaypoint endWaypoint;
    private bool hasArrived;

    public Enemy_GoToEndCell(EnemyController enemy,
                                    BossStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        hasArrived = false;
    }

    public override void EnterState()
    {
        // Step 1: ask WaypointService for all active waypoints (populated via MapManager).
        var allActive = waypointService.GetActiveWaypoints();
        if (allActive == null || allActive.Count == 0)
        {
            Debug.LogError("[MoveToEndCell] No active waypoints available.");
            return;
        }

        // Step 2: find the one whose parent room has UsageType.End and type == Center
        endWaypoint = allActive
            .Where(wp => wp.parentRoom.roomProperties.usageType == UsageType.End
                      && wp.type == WaypointType.Center)
            .FirstOrDefault();

        if (endWaypoint == null)
        {
            Debug.LogError("[MoveToEndCell] Could not locate an 'End' Center waypoint.");
            return;
        }

        Debug.Log($"[MoveToEndCell] Found '{endWaypoint.name}', path‐finding now...");
        enemy.SetDestination(endWaypoint);
    }

    public override void UpdateState()
    {
        if (enemy.memory.LastKnownPlayerPosition != Vector3.zero && enemy.memory.WasRecentlyAttacked)
        {
            stateMachine.ChangeState(new Enemy_AttackPlayer(enemy, stateMachine, waypointService));
        }
        if (hasArrived) return;

        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);

            stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState() { /* no extra cleanup */ }
}
