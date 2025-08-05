using UnityEngine;

/// <summary>
/// State responsible for following the player or patrolling via waypoints.
/// </summary>
public class Enemy_Follower : EnemyState
{
    private readonly FactoryAlarmStatus factoryAlarmStatus;

    // Max radius within which the enemy can enter attack mode
    private readonly float followRadius = 8f;

    public Enemy_Follower(
        EnemyController enemy,
        EnemyStateMachine machine,
        IWaypointService waypointService,
        FactoryAlarmStatus factoryAlarmStatus)
        : base(enemy, machine, waypointService)
    {
        this.enemy = enemy;
        this.waypointService = waypointService;
        this.factoryAlarmStatus = factoryAlarmStatus;
    }

    /// <summary>
    /// Set the enemy status when entering this state.
    /// </summary>
    public override void EnterState()
    {
        enemy.EnemyStatus = EnemyStatus.Following;
    }

    /// <summary>
    /// Move toward the player if known; otherwise follow waypoints.
    /// </summary>
    public override void UpdateState()
    {
        Vector3 lastKnownPos = enemy.memory.LastKnownPlayerPosition;
        if (lastKnownPos != Vector3.zero)
        {
            float distanceToKnownPos = Vector3.Distance(enemy.transform.position, lastKnownPos);
            if (distanceToKnownPos < followRadius)
            {
                stateMachine.ChangeState(
                    new Enemy_AttackPlayer(enemy, stateMachine, waypointService, this)
                );
            }
            else
            {
                Vector3 direction = (lastKnownPos - enemy.transform.position).normalized;
                enemy.SetMovement(direction.x);
                enemy.SetVerticalMovement(direction.y);
            }
            return;
        }

        // 1) Find the waypoint closest to the player
        var targetWp = waypointService.ClosestWaypointToPlayer;
        // 2) If the player is close enough, switch to attack
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, targetWp.WorldPos);
        if (distanceToPlayer < followRadius)
        {
            stateMachine.ChangeState(
                new Enemy_AttackPlayer(enemy, stateMachine, waypointService, this)
            );
            return;
        }

        enemy.SetDestination(targetWp);
    }

    /// <summary>
    /// Perform cleanup when exiting the state.
    /// </summary>
    public override void ExitState()
    {
        // Nothing to clean up here
    }
}
