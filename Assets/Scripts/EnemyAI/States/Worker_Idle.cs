using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Idle (stationnaire).
/// </summary>
public class Worker_Idle : WorkerState
{
    private const float IDLE_DURATION = 10f;
    private float _timer;
    public Worker_Idle(EnemyWorkerController enemy,
                                    EnemyStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        _timer = 0f;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.ReadyToWork;
        _timer = 0f;
        enemy.SetMovement(0);
    }

    public override void UpdateState()
    {
        _timer += Time.deltaTime;
        if (_timer >= IDLE_DURATION)
        {
            Debug.Log("[Idle] 5 seconds elapsed → moving to next POI.");
            stateMachine.ChangeState(new Worker_GoingToLeastWorkedStation(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }

}
