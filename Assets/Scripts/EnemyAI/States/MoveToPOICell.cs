// MoveToPOICell.cs (new)
// ---------------------------------
// ­– Knows how to find the “POI” RoomWaypoint by asking MapManager + WaypointService.
// ­– Once found, delegates movement to EnemyController.SetDestination(POIWp).

using System.Linq;
using UnityEngine;

public class MoveToPOICell : BossState
{
    private RoomWaypoint _reservedPoi;
    private bool hasArrived;

    public MoveToPOICell(EnemyBossController enemy,
                                    BossStateMachine machine,
                                    WaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        hasArrived = false;
    }

    public override void EnterState()
    {
        if (_reservedPoi == null)
        {
            stateMachine.ChangeState(new BossIdle(enemy, stateMachine, waypointService));
            return;
        }

        enemy.SetDestination(_reservedPoi);
    }

    public override void UpdateState()
    {
        if (hasArrived) return;

        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);

            stateMachine.ChangeState(new BossDefend(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }
}
