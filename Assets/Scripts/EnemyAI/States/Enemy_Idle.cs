using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Idle (stationnaire).
/// </summary>
public class Enemy_Idle : EnemyState
{
    public Enemy_Idle(EnemyController enemy, BossStateMachine machine, IWaypointService waypointService)
        : base(enemy, machine, waypointService)
    {
    }

    public override void EnterState()
    {
        // enemy.SetMovement(0);
        Debug.Log("[BossIdle] Boss is idle.");
    }

    public override void UpdateState()
    {
        if (enemy.memory.LastKnownPlayerPosition != Vector3.zero && enemy.memory.WasRecentlyAttacked)
        {
            stateMachine.ChangeState(new Enemy_AttackPlayer(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }

}
