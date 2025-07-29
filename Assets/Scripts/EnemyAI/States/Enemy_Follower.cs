using UnityEngine;

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

    public override void EnterState()
    {
        enemy.EnemyStatus = EnemyStatus.Following;
    }

    public override void UpdateState()
    {
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

    public override void ExitState()
    {
        // Nothing to clean up here
    }
}
