// EnemyState_MoveToPOICell.cs (new)
// ---------------------------------
// ­– Knows how to find the “POI” RoomWaypoint by asking MapManager + WaypointService.
// ­– Once found, delegates movement to EnemyController.SetDestination(POIWp).

using System.Linq;
using UnityEngine;

public class Worker_GoingToLeastWorkedStation : WorkerState
{
    private RoomWaypoint targetPoint;
    private bool hasArrived;

    public Worker_GoingToLeastWorkedStation(EnemyWorkerController enemy,
                                    WorkerStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        hasArrived = false;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.GoingToWork;
        targetPoint = waypointService.GetLeastUsedFreeWorkPoint(enemy.memory.LastVisitedPoint);
        if (targetPoint == null)
        {
            stateMachine.ChangeState(new Worker_Idle(
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

            enemy.workerState = WorkerStatus.ReadyToWork;
            Debug.Log($"[GoingToLeastWorkedStation] Arrived at {targetPoint.name}. Changing state to Work.");
        }
    }

    public override void ExitState()
    {
    }
}
