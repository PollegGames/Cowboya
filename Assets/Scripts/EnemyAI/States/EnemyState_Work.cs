using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Work (stationnaire).
/// </summary>
public class EnemyState_Work : EnemyState
{
    private const float Work_DURATION = 5f;
    private float _timer;
    public EnemyState_Work(EnemyController enemy,
                                    EnemyStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        _timer = 0f;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerState.Working;
        enemy.SetMovement(0);
    }

    public override void UpdateState()
    {
       
    }

    public override void ExitState()
    {
        // Nettoyage éventuel à la sortie de l'état
    }
}
