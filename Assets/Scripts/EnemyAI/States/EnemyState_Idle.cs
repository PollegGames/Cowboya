using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Idle (stationnaire).
/// </summary>
public class EnemyState_Idle : EnemyState
{
    private const float IDLE_DURATION = 10f;
    private float _timer;
    public EnemyState_Idle(EnemyController enemy,
                                    EnemyStateMachine machine,
                                    WaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        _timer = 0f;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerState.ReadyToWork;
        _timer = 0f;
        enemy.SetMovement(0);
    }

    public override void UpdateState()
    {
        _timer += Time.deltaTime;
        if (_timer >= IDLE_DURATION)
        {
            Debug.Log("[Idle] 5 seconds elapsed → moving to next POI.");
            stateMachine.ChangeState(new GoingToLeastWorkedStation(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }

}
