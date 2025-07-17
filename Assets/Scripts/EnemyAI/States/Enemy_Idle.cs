using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Idle (stationnaire) avec un rayon limite
/// pour éviter les boucles infinies.
/// </summary>
public class Enemy_Idle : EnemyState
{
    private bool hasArrived;
    private readonly float idleRadius = 2f;// Rayon d'indulgence
    private RoomWaypoint originPoint;

    public Enemy_Idle(EnemyController enemy, EnemyStateMachine machine, IWaypointService waypointService)
        : base(enemy, machine, waypointService)
    {
    }

    public override void EnterState()
    {
        enemy.EnemyStatus = EnemyStatus.Idle;
        hasArrived = false;
        originPoint = null;
    }

    public override void UpdateState()
    {
        // Priorité : passer en attaque si le joueur est connu et qu'on a été attaqué
        if (enemy.memory.LastKnownPlayerPosition != Vector3.zero && enemy.memory.WasRecentlyAttacked)
        {
            stateMachine.ChangeState(new Enemy_AttackPlayer(enemy, stateMachine, waypointService, this));
        }
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
    }
}