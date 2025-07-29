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
    private float readyStartTime;      // time when we entered ReadyToWork
    private const float MaxReadyDuration = 2f;
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
            Debug.LogWarning("[GoingToLeastWorkedStation] No free work point found. Going to rest.");
            stateMachine.ChangeState(new Worker_GoingToRestStation(
                enemy, stateMachine, waypointService));
            return;
        }

        enemy.SetDestination(targetPoint);
    }

    public override void UpdateState()
    {
        // If not yet arrived, check arrival
        if (!hasArrived)
        {
            if (enemy.HasArrivedAtDestination())
            {
                hasArrived = true;
                enemy.SetMovement(0f);
                enemy.SetVerticalMovement(0f);

                // remember the POI and release it
                enemy.memory.SetLastVisitedPoint(targetPoint);
                waypointService.ReleasePOI(targetPoint);

                enemy.workerState = WorkerStatus.ReadyToWork;
                readyStartTime = Time.time; // start the timer
            }

            return;
        }

        // When arrived and in ReadyToWork: check the timer
        if (enemy.workerState == WorkerStatus.ReadyToWork &&
            Time.time - readyStartTime >= MaxReadyDuration)
        {
            // switch to the state that goes to rest
            stateMachine.ChangeState(new Worker_GoingToRestStation(
                enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }
}
