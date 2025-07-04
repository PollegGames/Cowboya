// EnemyState_MoveToPOICell.cs (new)
// ---------------------------------
// ­– Knows how to find the “POI” RoomWaypoint by asking MapManager + WaypointService.
// ­– Once found, delegates movement to EnemyController.SetDestination(POIWp).

using System.Linq;
using UnityEngine;

public class GoingToLeastWorkedStation : EnemyState
{
    private RoomWaypoint targetPoint;
    private bool hasArrived;

    public GoingToLeastWorkedStation(EnemyController enemy,
                                    EnemyStateMachine machine,
                                    WaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        hasArrived = false;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerState.GoingToWork;
        targetPoint = waypointService.GetLeastUsedFreeWorkPoint(enemy.memory.LastVisitedPoint);
        if (targetPoint == null)
        {
            stateMachine.ChangeState(new EnemyState_Idle(
                enemy, stateMachine, waypointService));
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
            waypointService.ReleasePOI(targetPoint);

            enemy.workerState = WorkerState.ReadyToWork;
            Debug.Log($"[GoingToLeastWorkedStation] Arrived at {targetPoint.name}. Changing state to Work.");
        }
    }

    public override void ExitState()
    {
    }
}
