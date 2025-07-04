using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Idle (stationnaire).
/// </summary>
public class BossIdle : BossState
{
    public BossIdle(EnemyBossController enemy, BossStateMachine machine, WaypointService waypointService)
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
            stateMachine.ChangeState(new BossAttackPlayer(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }

}
