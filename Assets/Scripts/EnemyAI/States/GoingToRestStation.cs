// EnemyState_MoveToPOICell.cs (new)
// ---------------------------------
// ­– Knows how to find the “POI” RoomWaypoint by asking MapManager + WaypointService.
// ­– Once found, delegates movement to EnemyController.SetDestination(POIWp).

using System.Linq;
using UnityEngine;

public class GoingToRestStation : EnemyState
{
    private RoomWaypoint targetPoint;
    private bool hasArrived;

    public GoingToRestStation(EnemyController enemy,
                                    EnemyStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        hasArrived = false;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerState.GoingToRest;
        targetPoint = waypointService.GetFirstRestPoint();
        if (targetPoint == null)
        {
            stateMachine.ChangeState(new EnemyState_Idle(enemy, stateMachine, waypointService));
            return;
        }
        enemy.SetDestination(targetPoint);
    }

    public override void UpdateState()
    {
        if (hasArrived) return;

        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);

            enemy.memory.SetLastVisitedPoint(targetPoint);
            stateMachine.ChangeState(new EnemyState_Idle(enemy, stateMachine, waypointService));

        }
    }

    public override void ExitState()
    {
    }
}
