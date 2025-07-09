using UnityEngine;

public class Worker_GoingToStartRoom : WorkerState
{
    private bool hasArrived;

    public Worker_GoingToStartRoom(EnemyWorkerController enemy,
                                   WorkerStateMachine machine,
                                   IWaypointService waypointService)
        : base(enemy, machine, waypointService)
    {
        hasArrived = false;
    }

    public override void EnterState()
    {
        enemy.SetWorkerState(WorkerCondition.Active);
        var startRoom = waypointService.GetStartPoint();
        enemy.SetDestination(startRoom);
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

