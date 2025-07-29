using UnityEngine;

/// <summary>
/// Example implementation of a simple state: Work (stationary).
/// </summary>
public class Worker_IsWork : WorkerState
{
    public Worker_IsWork(EnemyWorkerController enemy,
                                    WorkerStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.Working;
        enemy.SetMovement(0);
    }

    public override void UpdateState()
    {
       
    }

    public override void ExitState()
    {
        // Optional cleanup when leaving the state
    }
}
