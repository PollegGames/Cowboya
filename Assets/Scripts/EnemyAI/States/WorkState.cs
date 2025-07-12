using UnityEngine;

public class WorkState : WorkerState
{
    private BaseMachine machine;

    public WorkState(EnemyWorkerController enemy, WorkerStateMachine machineSM, IWaypointService waypointService, BaseMachine machine)
        : base(enemy, machineSM, waypointService)
    {
        this.machine = machine;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.Working;
        enemy.SetMovement(0f);
    }

    public override void UpdateState() { }

    public override void ExitState() { }
}
