using UnityEngine;

/// <summary>
/// Example implementation of a simple Idle state with a radius limit
/// to prevent infinite loops.
/// </summary>
public class Enemy_Idle : EnemyState
{
    private bool hasArrived;
    private readonly float idleRadius = 2f; // grace radius
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
        // Priority: switch to attack if the player is known and we've been hit
        if (enemy.memory.LastKnownPlayerPosition != Vector3.zero && enemy.memory.WasRecentlyAttacked)
        {
            stateMachine.ChangeState(new Enemy_AttackPlayer(enemy, stateMachine, waypointService, this));
        }
        if (originPoint == null && enemy.memory.LastVisitedPoint != null)
        {
            originPoint = enemy.memory.LastVisitedPoint;
            // Set the destination to the last visited point
            enemy.SetDestination(originPoint);

        }

        if (hasArrived) return;

        // Check if the enemy reached the destination
        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);

            // If the enemy moved too far from the origin point, reset Idle state
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