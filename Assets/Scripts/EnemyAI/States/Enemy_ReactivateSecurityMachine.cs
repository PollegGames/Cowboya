using UnityEngine;

public class Enemy_ReactivateSecurityMachine : EnemyState
{
    private readonly SecurityMachine targetMachine;
    private RoomWaypoint targetPoint;
    private readonly RoomWaypoint returnPoint;
    private bool hasArrived;

    public Enemy_ReactivateSecurityMachine(
        EnemyController enemy,
        EnemyStateMachine machine,
        IWaypointService waypointService,
        SecurityMachine machineToActivate,
        RoomWaypoint returnPoint)
        : base(enemy, machine, waypointService)
    {
        targetMachine = machineToActivate;
        this.returnPoint = returnPoint;
    }

    public override void EnterState()
    {
        enemy.EnemyStatus = EnemyStatus.ReactivatingMachine;
        hasArrived = false;
        if (targetMachine == null)
        {
            stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService));
            return;
        }
        targetPoint = waypointService.GetClosestWaypoint(targetMachine.transform.position, includeUnavailable: true);
        enemy.SetDestination(targetPoint, includeUnavailable: true);
    }

    public override void UpdateState()
    {
        if (hasArrived) return;
        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);
            targetMachine.SetState(true);
            stateMachine.ChangeState(new Enemy_ReturnToSecurityPost(enemy, stateMachine, waypointService, returnPoint));
        }
    }

    public override void ExitState() { }
}
