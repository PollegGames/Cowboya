using UnityEngine;

/// <summary>
/// Example implementation of a simple state: Work (stationary).
/// </summary>
public class Worker_Spawning : WorkerState
{
    public Worker_Spawning(EnemyWorkerController enemy,
                                    WorkerStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.Spawning;
    }

    public override void UpdateState()
    {
   
    }

    public override void ExitState()
    {
        // Optional cleanup when leaving the state
    }
}
