using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Work (stationnaire).
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
        // Nettoyage éventuel à la sortie de l'état
    }
}
