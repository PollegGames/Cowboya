using UnityEngine;

public class Enemy_CheckingSecurity : EnemyState
{
    public Enemy_CheckingSecurity(EnemyController enemy, EnemyStateMachine stateMachine, IWaypointService waypointService)
        : base(enemy, stateMachine, waypointService) {}

    public override void EnterState()
    {
        enemy.EnemyStatus = EnemyStatus.CheckingSecurity;
        enemy.SetMovement(0f);
        enemy.SetVerticalMovement(0f);
    }

    public override void UpdateState()
    {
        if (enemy.memory.LastKnownPlayerPosition != Vector3.zero && enemy.memory.WasRecentlyAttacked)
        {
            stateMachine.ChangeState(new Enemy_AttackPlayer(enemy, stateMachine, waypointService, this));
        }
    }

    public override void ExitState() { }
}
