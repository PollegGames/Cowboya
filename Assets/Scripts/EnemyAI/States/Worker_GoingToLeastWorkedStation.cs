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
    private FactoryMachine reservedMachine;
    private float readyStartTime;      // moment où on est passé en ReadyToWork
    private const float MaxReadyDuration = 20f; // 20 secondes
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
            stateMachine.ChangeState(new Worker_GoingToRestStation(
                enemy, stateMachine, waypointService));
            return;
        }

        reservedMachine = waypointService.ReserveFreeMachine(targetPoint.parentRoom, enemy);

        enemy.SetDestination(targetPoint);
    }

    public override void UpdateState()
    {
        // si pas encore arrivé, on vérifie l’arrivée
        if (!hasArrived)
        {
            if (enemy.HasArrivedAtDestination())
            {
                hasArrived = true;
                enemy.SetMovement(0f);
                enemy.SetVerticalMovement(0f);

                // mémoriser le POI et le libérer
                enemy.memory.SetLastVisitedPoint(targetPoint);
                waypointService.ReleasePOI(targetPoint);

                enemy.workerState = WorkerStatus.ReadyToWork;
                readyStartTime = Time.time; // démarrer le timer
                Debug.Log($"[GoingToLeastWorkedStation] Arrived at {targetPoint.name}. Changing state to ReadyToWork.");
            }

            return;
        }

        // arrivé et en ReadyToWork : on vérifie le timer
        if (enemy.workerState == WorkerStatus.ReadyToWork &&
            Time.time - readyStartTime >= MaxReadyDuration)
        {
            Debug.Log($"[GoingToLeastWorkedStation] ReadyToWork for {MaxReadyDuration}s. Going to rest machine.");

            // passer à l'état aller se reposer
            stateMachine.ChangeState(new Worker_GoingToRestStation(
                enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }
}
