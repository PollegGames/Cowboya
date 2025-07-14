using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Idle (stationnaire) avec un rayon limite
/// pour éviter les boucles infinies.
/// </summary>
public class Enemy_Idle : EnemyState
{
    private bool hasArrived;
    private readonly float idleRadius;               // Rayon d'indulgence
    private RoomWaypoint originPoint;

    public Enemy_Idle(EnemyController enemy, EnemyStateMachine machine, IWaypointService waypointService, float idleRadius = 2f)
        : base(enemy, machine, waypointService)
    {
        this.idleRadius = idleRadius;
    }

    public override void EnterState()
    {
        hasArrived = false;
        originPoint = null;
    }

    public override void UpdateState()
    {
        // Priorité : passer en attaque si le joueur est connu et qu'on a été attaqué
        if (enemy.memory.LastKnownPlayerPosition != Vector3.zero && enemy.memory.WasRecentlyAttacked)
        {
            stateMachine.ChangeState(new Enemy_AttackPlayer(enemy, stateMachine, waypointService, this));
            return;
        }
        if (originPoint == null && enemy.memory.LastVisitedPoint != null)
        {
            originPoint = enemy.memory.LastVisitedPoint;
            // On fixe la destination au dernier point visité
            enemy.SetDestination(originPoint);

            Debug.Log($"[Enemy_Idle] Moving to idle origin at {originPoint.WorldPos}");
        }

        if (hasArrived) return;

        // Vérifie si l'ennemi est arrivé à destination
        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);
            Debug.Log("[Enemy_Idle] Arrived at origin, stopping movement.");

            // Si l'ennemi s'est trop éloigné du point d'origine, on réinitialise l'état Idle
            float distFromOrigin = Vector3.Distance(enemy.transform.position, originPoint.WorldPos);
            if (distFromOrigin > idleRadius)
            {
                Debug.Log($"[Enemy_Idle] Distance {distFromOrigin} > idleRadius {idleRadius}, retrying Idle state.");
                stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService, idleRadius));
            }
        }
    }

    public override void ExitState()
    {
        Debug.Log("[Enemy_Idle] Exiting Idle state.");
    }
}