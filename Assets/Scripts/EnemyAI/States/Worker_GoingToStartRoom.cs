using UnityEngine;

public class Worker_GoingToStartRoom : WorkerState
{
    private RoomWaypoint target;
    private bool hasArrived;

    public Worker_GoingToStartRoom(EnemyWorkerController enemy,
                                   WorkerStateMachine machine,
                                   IWaypointService waypointService,
                                   RoomWaypoint targetWp)
        : base(enemy, machine, waypointService)
    {
        target = targetWp;
        hasArrived = false;
    }

    public override void EnterState()
    {
        enemy.SetWorkerState(WorkerCondition.Active);
        enemy.SetDestination(target);
    }

    public override void UpdateState()
    {
        if (hasArrived) return;

        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            stateMachine.ChangeState(new Worker_Saved(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState() {}
}

