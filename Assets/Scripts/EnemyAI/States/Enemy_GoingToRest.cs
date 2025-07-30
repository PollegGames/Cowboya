using UnityEngine;

public class Enemy_GoingToRest : EnemyState
{
    private RoomWaypoint targetPoint;
    private bool hasArrived;

    public Enemy_GoingToRest(EnemyController enemy, EnemyStateMachine machine, IWaypointService waypointService)
        : base(enemy, machine, waypointService) { }

    public override void EnterState()
    {
        enemy.EnemyStatus = EnemyStatus.GoingToRest;
        targetPoint = waypointService.GetFirstRestPoint(enemy.memory.LastVisitedPoint);
        if (targetPoint == null)
            targetPoint = waypointService.GetFirstRestPoint();
        if (targetPoint == null)
        {
            stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService));
            return;
        }
        enemy.SetDestination(targetPoint);
        hasArrived = false;
    }

    public override void UpdateState()
    {
        if (hasArrived) return;
        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);
            enemy.memory.SetLastVisitedPoint(targetPoint);
            waypointService.ReleasePOI(targetPoint);
            stateMachine.ChangeState(new Enemy_SecurityGuardRest(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState() { }
}
