using UnityEngine;

public class Enemy_Follower : EnemyState
{
    private readonly FactoryAlarmStatus factoryAlarmStatus;

    // Rayon max dans lequel on considère qu'on peut aller en mode attaque
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
        // 1) On cherche le waypoint le plus proche du joueur
        var targetWp = waypointService.ClosestWaypointToPlayer;
        // 2) Si le joueur est trop loin, on passe en attaque
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
        // Rien à nettoyer ici
    }
}
