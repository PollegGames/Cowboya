using UnityEngine;

public class Enemy_Follower : EnemyState
{
    private readonly FactoryAlarmStatus factoryAlarmStatus;

    // Rayon max dans lequel on considère qu'on peut « suivre » avant d'attaquer
    private readonly float followRadius = 2f;

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
        // 1) On récupère la position la plus récente du joueur
        Vector3 playerPos = factoryAlarmStatus.LastPlayerPosition;

        // 2) Si le joueur est trop loin, on passe en attaque
        float distanceToPlayer = Vector3.Distance(enemy.transform.position, playerPos);
        if (distanceToPlayer > followRadius)
        {
            stateMachine.ChangeState(
                new Enemy_AttackPlayer(enemy, stateMachine, waypointService, this)
            );
            return;
        }

        // 3) On cherche le waypoint le plus proche du joueur
        var targetWp = waypointService.GetClosestWaypoint(playerPos);
        if (targetWp != null)
        {
            // Pas de waypoint trouvé : fonce directement sur la position
            enemy.SetDestination(targetWp);
        }
        else
        {
            // todo ???
        }
    }

    public override void ExitState()
    {
        // Rien à nettoyer ici
    }
}
