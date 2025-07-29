using UnityEngine;

public class Worker_GoingToMachine : WorkerState
{
    private readonly FactoryMachine machine;
    private RoomWaypoint targetPoint;
    private bool hasArrived;

    public Worker_GoingToMachine(EnemyWorkerController enemy,
                                 WorkerStateMachine machineSM,
                                 IWaypointService waypointService,
                                 FactoryMachine machine)
        : base(enemy, machineSM, waypointService)
    {
        this.machine = machine;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.GoingToWork;
        targetPoint = waypointService.GetClosestWaypoint(machine.transform.position, includeUnavailable: true);
        enemy.SetDestination(targetPoint, includeUnavailable: true);
        hasArrived = false;
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
            machine.AttachRobot(enemy.gameObject);
        }
    }

    public override void ExitState() { }
}
