using UnityEngine;

public class Enemy_SecurityCheck : EnemyState
{
    private const float Check_DURATION = 5f;
    private float _timer;

    public Enemy_SecurityCheck(EnemyController enemy,
                               EnemyStateMachine machine,
                               IWaypointService waypointService)
        : base(enemy, machine, waypointService)
    {
    }

    public override void EnterState()
    {
        _timer = 0f;
        enemy.SetMovement(0f);
        enemy.SetVerticalMovement(0f);
    }

    public override void UpdateState()
    {
        _timer += Time.deltaTime;
        if (_timer >= Check_DURATION)
        {
            stateMachine.ChangeState(new Enemy_SecurityGuardRest(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState() {}
}
