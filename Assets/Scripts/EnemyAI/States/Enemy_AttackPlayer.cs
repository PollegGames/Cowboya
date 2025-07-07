using System.Collections;
using UnityEngine;

public class Enemy_AttackPlayer : EnemyState
{
    private Vector3 playerTransform;
    private float stopDistance = 1.5f;

    public Enemy_AttackPlayer(EnemyController enemy, EnemyStateMachine machine, IWaypointService waypointService)
        : base(enemy, machine, waypointService)
    {
        playerTransform = enemy.memory.LastKnownPlayerPosition;
    }

    public override void EnterState()
    {
        Debug.Log("[BossAttackPlayer] Start chasing the player.");
    }

    public override void UpdateState()
    {
        // Always get the latest player position
        playerTransform = enemy.memory.LastKnownPlayerPosition;

        if (playerTransform == Vector3.zero)
        {
            stateMachine.ChangeState(new Enemy_GoToEndCell(enemy, stateMachine, waypointService));
            return;
        }

        Vector3 direction = playerTransform - enemy.BodyReference.position;
        float distance = direction.magnitude;

        if (distance > stopDistance)
        {
            // Move towards player
            enemy.SetMovement(Mathf.Sign(direction.x));
            enemy.SetVerticalMovement(Mathf.Sign(direction.y));
        }
        else
        {
            enemy.SetMovement(0);
            enemy.SetVerticalMovement(0);
        }
    }

    public override void ExitState()
    {
        enemy.SetMovement(0);
        enemy.SetVerticalMovement(0);
        Debug.Log("[BossAttackPlayer] Exit attack state.");
    }
}
