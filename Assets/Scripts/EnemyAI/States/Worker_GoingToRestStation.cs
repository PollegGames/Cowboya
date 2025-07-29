// EnemyState_MoveToPOICell.cs (new)
// ---------------------------------
// ­– Knows how to find the “POI” RoomWaypoint by asking MapManager + WaypointService.
// ­– Once found, delegates movement to EnemyController.SetDestination(POIWp).

using System.Linq;
using UnityEngine;

public class Worker_GoingToRestStation : WorkerState
{
    private RoomWaypoint targetPoint;
    private bool hasArrived;
    private float readyStartTime;      // time when we entered ReadyToRest
    private const float MaxReadyDuration = 2f;
    public Worker_GoingToRestStation(EnemyWorkerController enemy,
                                    WorkerStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        hasArrived = false;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.GoingToRest;
        // Try to find a rest point, excluding the last one visited to
        // encourage some variety. However if this yields no result (for
        // example when there is only one rest station in the map), retry
        // without the exclusion so the worker can reuse the same point.
        targetPoint = waypointService.GetFirstRestPoint(enemy.memory.LastVisitedPoint);
        if (targetPoint == null)
            targetPoint = waypointService.GetFirstRestPoint();

        if (targetPoint == null)
        {
            Debug.LogWarning("[GoingToRestStation] No rest point found. Returning to start room.");
            stateMachine.ChangeState(new Worker_GoingToStartRoom(enemy, stateMachine, waypointService));
            return;
        }
        enemy.SetDestination(targetPoint);
    }

    public override void UpdateState()
    {
        if (!hasArrived)
        {
            if (enemy.HasArrivedAtDestination())
            {
                hasArrived = true;
                enemy.SetMovement(0f);
                enemy.SetVerticalMovement(0f);

                enemy.memory.SetLastVisitedPoint(targetPoint);
                waypointService.ReleasePOI(targetPoint);
                
                enemy.workerState = WorkerStatus.ReadyToRest;
                readyStartTime = Time.time;
            }
            return;
        }
        // Once arrived and in ReadyToWork we check the timer
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
