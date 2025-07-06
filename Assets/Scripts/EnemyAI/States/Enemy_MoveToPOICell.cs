// MoveToPOICell.cs (new)
// ---------------------------------
// ­– Knows how to find the “POI” RoomWaypoint by asking MapManager + WaypointService.
// ­– Once found, delegates movement to EnemyController.SetDestination(POIWp).

using System.Linq;
using UnityEngine;

public class Enemy_MoveToPOICell : EnemyState
{
    private RoomWaypoint _reservedPoi;
    private bool hasArrived;

    public Enemy_MoveToPOICell(EnemyController enemy,
                                    BossStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        hasArrived = false;
    }

    public override void EnterState()
    {
        if (_reservedPoi == null)
        {
            stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService));
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

            stateMachine.ChangeState(new Enemy_WaitBeforeGoingToPOI(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }
}
