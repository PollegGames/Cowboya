using UnityEngine;

public class Worker_Saved : WorkerState
{
    public Worker_Saved(EnemyWorkerController enemy, WorkerStateMachine machine, IWaypointService waypointService)
        : base(enemy, machine, waypointService) { }

    public override void EnterState()
    {
        // Stop any motion before deciding what to do
        enemy.SetMovement(0f);
        enemy.SetVerticalMovement(0f);
    }

    public override void UpdateState()
    {

        // 1) Try to work
        var last = enemy.memory?.LastVisitedPoint;
        RoomWaypoint workPoint = waypointService.GetLeastUsedFreeWorkPoint(last);
        if (workPoint != null)
        {
            stateMachine.ChangeState(new Worker_GoingToLeastWorkedStation(enemy, stateMachine, waypointService));
            return;
        }

        // 2) Try to rest (method name may differ in your IWaypointService)
        RoomWaypoint restPoint = waypointService.GetFirstRestPoint(last);
        if (restPoint != null)
        {
            stateMachine.ChangeState(new Worker_GoingToRestStation(enemy, stateMachine, waypointService));
            return;
        }

        // 3) Nothing available => convert and mark as saved
        enemy.workerState = WorkerStatus.Saved;
        enemy.ConvertToAlly();
        SceneController.instance?.RobotSaved();
    }

    public override void ExitState() { }
}

