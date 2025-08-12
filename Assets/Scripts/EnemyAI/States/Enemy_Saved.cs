using UnityEngine;

/// <summary>
/// Example implementation of a simple Idle state with a radius limit
/// to prevent infinite loops.
/// </summary>
public class Enemy_Saved : EnemyState
{

    public Enemy_Saved(EnemyController enemy, EnemyStateMachine machine, IWaypointService waypointService)
        : base(enemy, machine, waypointService)
    {
    }

    public override void EnterState()
    {
        enemy.EnemyStatus = EnemyStatus.Saved;
    }

    public override void UpdateState()
    {
        // 1) Try to work
        var last = enemy.memory?.LastVisitedPoint;
        RoomWaypoint securityPoint = waypointService.GetFirstFreeSecurityPoint();
        if (securityPoint != null)
        {
            stateMachine.ChangeState(new Enemy_ReturnToSecurityPost(enemy, stateMachine, waypointService, securityPoint));
            return;
        }

        // 2) Try to rest (method name may differ in your IWaypointService)
        RoomWaypoint restPoint = waypointService.GetFirstRestPoint(last);
        if (restPoint != null)
        {
            stateMachine.ChangeState(new Enemy_GoingToRest(enemy, stateMachine, waypointService));
            return;
        }

        // 3) Nothing available => convert and mark as saved
        enemy.EnemyStatus = EnemyStatus.Saved;
        // enemy.ConvertToAlly();
        SceneController.instance?.RobotSaved();
    }

    public override void ExitState()
    {
    }
}