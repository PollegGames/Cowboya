using UnityEngine;

public class Worker_Resting : WorkerState
{
    private const float Rest_DURATION = 5f;
    private float _timer;
    public Worker_Resting(EnemyWorkerController enemy, WorkerStateMachine machine, IWaypointService waypointService)
        : base(enemy, machine, waypointService) { }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.Resting;
        enemy.SetMovement(0f);
        _timer = 0f;
    }

    public override void UpdateState()
    {
        _timer += Time.deltaTime;
        if (_timer >= Rest_DURATION)
        {
            Debug.Log($"[Rest] {Rest_DURATION} seconds elapsed → moving to next POI.");
            stateMachine.ChangeState(new Worker_GoingToLeastWorkedStation(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
        waypointService.ReleasePOI(enemy.memory.LastVisitedPoint);
    }
}

