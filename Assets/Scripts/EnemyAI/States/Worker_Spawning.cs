using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Work (stationnaire).
/// </summary>
public class Worker_Spawning : WorkerState
{
    private bool hasArrived;
    private readonly float idleRadius = 2f;// Rayon d'indulgence      
    private RoomWaypoint originPoint;
    public Worker_Spawning(EnemyWorkerController enemy,
                                    WorkerStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.Spawning;
        hasArrived = false;
        originPoint = null;
    }

    public override void UpdateState()
    {
        if (originPoint == null && enemy.memory.LastVisitedPoint != null)
        {
            originPoint = enemy.memory.LastVisitedPoint;
            // On fixe la destination au dernier point visité
            enemy.SetDestination(originPoint);

        }

        if (hasArrived) return;

        // Vérifie si l'ennemi est arrivé à destination
        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);

            // Si l'ennemi s'est trop éloigné du point d'origine, on réinitialise l'état Idle
            float distFromOrigin = Vector3.Distance(enemy.transform.position, originPoint.WorldPos);
            if (distFromOrigin > idleRadius)
            {
                hasArrived = false;
                originPoint = null;
            }
        }
    }

    public override void ExitState()
    {
        // Nettoyage éventuel à la sortie de l'état
    }
}
