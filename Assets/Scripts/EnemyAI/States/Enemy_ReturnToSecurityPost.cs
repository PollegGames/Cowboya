using UnityEngine;

/// <summary>
/// Moves the guard back to a specific security waypoint.
/// </summary>
public class Enemy_ReturnToSecurityPost : EnemyState
{
    private readonly RoomWaypoint targetPoint;
    private bool hasArrived;

    public Enemy_ReturnToSecurityPost(
        EnemyController enemy,
        EnemyStateMachine machine,
        IWaypointService waypointService,
        RoomWaypoint securityPoint)
        : base(enemy, machine, waypointService)
    {
        targetPoint = securityPoint;
    }

    public override void EnterState()
    {
        enemy.EnemyStatus = EnemyStatus.GoingToSecurityPost;
        hasArrived = false;
        if (targetPoint == null)
        {
            stateMachine.ChangeState(new Enemy_SecurityGuardRest(enemy, stateMachine, waypointService));
            return;
        }
        enemy.SetDestination(targetPoint);
    }

    public override void UpdateState()
    {
         // Priority: switch to attack if the player is known and we've been hit
        if (enemy.memory.LastKnownPlayerPosition != Vector3.zero && enemy.memory.WasRecentlyAttacked)
        {
            stateMachine.ChangeState(new Enemy_AttackPlayer(enemy, stateMachine, waypointService, this));
        }
        if (hasArrived) return;

        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);
            enemy.memory.SetLastVisitedPoint(targetPoint);
            stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState() { }
}
