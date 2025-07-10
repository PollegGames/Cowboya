using UnityEngine;

public class Enemy_SecurityGuardRest : EnemyState
{
    private const float Rest_DURATION = 10f;
    private float _timer;
    private bool moving;
    private RoomWaypoint targetPoint;

    public Enemy_SecurityGuardRest(EnemyController enemy,
                                   EnemyStateMachine machine,
                                   IWaypointService waypointService)
        : base(enemy, machine, waypointService)
    {
    }

    public override void EnterState()
    {
        _timer = 0f;
        moving = false;
        enemy.SetMovement(0f);
        enemy.SetVerticalMovement(0f);
    }

    public override void UpdateState()
    {
        if (!moving)
        {
            _timer += Time.deltaTime;
            if (_timer >= Rest_DURATION)
            {
                targetPoint = waypointService.GetFirstFreeSecurityPoint();
                if (targetPoint == null)
                {
                    stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService));
                    return;
                }
                enemy.SetDestination(targetPoint);
                moving = true;
            }
            return;
        }

        if (enemy.HasArrivedAtDestination())
        {
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);
            enemy.memory.SetLastVisitedPoint(targetPoint);
            waypointService.ReleasePOI(targetPoint);
            stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
    }
}
