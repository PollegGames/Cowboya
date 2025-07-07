using UnityEngine;

public class Worker_Resting : WorkerState
{
    public Worker_Resting(EnemyWorkerController enemy, WorkerStateMachine machine, IWaypointService waypointService)
        : base(enemy, machine, waypointService) { }

    public override void EnterState()
    {
        enemy.SetWorkerState(WorkerCondition.Resting);
        enemy.SetMovement(0f);
    }

    public override void UpdateState() {}

    public override void ExitState() {}
}

