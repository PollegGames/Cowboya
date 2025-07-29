using UnityEngine;

/// <summary>
/// Example implementation of a simple state: Idle (standing still).
/// </summary>
public class Worker_Idle : WorkerState
{
    private const float IDLE_DURATION = 10f;
    private float _timer;
    public Worker_Idle(EnemyWorkerController enemy,
                                    WorkerStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        _timer = 0f;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.Idle;
        _timer = 0f;
        enemy.SetMovement(0);
    }

    public override void UpdateState()
    {
        _timer += Time.deltaTime;
        if (_timer >= IDLE_DURATION)
        {
            Debug.Log($"[Idle] {IDLE_DURATION} seconds elapsed → moving to next POI.");
            var returnPoint = enemy.memory.LastVisitedPoint;
            waypointService.ReleasePOI(returnPoint);
            if (enemy.IsWorkerSpawner)
                stateMachine.ChangeState(new Worker_GoingToSpawningMachine(enemy, stateMachine, waypointService));
            else
                stateMachine.ChangeState(new Worker_GoingToLeastWorkedStation(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }

}
