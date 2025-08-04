using UnityEngine;

public class Worker_Saved : WorkerState
{
    public Worker_Saved(EnemyWorkerController enemy, WorkerStateMachine machine, IWaypointService waypointService)
        : base(enemy, machine, waypointService) { }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.Saved;
        enemy.SetMovement(0f);
        enemy.ConvertToAlly();
        SceneController.instance?.RobotSaved();
    }

    public override void UpdateState() {}

    public override void ExitState() {}
}

