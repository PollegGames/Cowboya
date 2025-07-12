using UnityEngine;

public class GoPowerOnMachineState : EnemyState
{
    private BaseMachine targetMachine;
    private RoomWaypoint targetPoint;
    private readonly RoomWaypoint returnPoint;
    private bool hasArrived;

    public GoPowerOnMachineState(EnemyController enemy, EnemyStateMachine machine, IWaypointService waypointService, BaseMachine machineToPower, RoomWaypoint returnPoint)
        : base(enemy, machine, waypointService)
    {
        targetMachine = machineToPower;
        this.returnPoint = returnPoint;
    }

    public override void EnterState()
    {
        hasArrived = false;
        if (targetMachine == null)
        {
            stateMachine.ChangeState(new Enemy_ReturnToSecurityPost(enemy, stateMachine, waypointService, returnPoint));
            return;
        }
        targetPoint = waypointService.GetClosestWaypoint(targetMachine.transform.position);
        enemy.SetDestination(targetPoint);
    }

    public override void UpdateState()
    {
        if (hasArrived) return;
        if (enemy.HasArrivedAtDestination())
        {
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);
            targetMachine.PowerOn();
            stateMachine.ChangeState(new Enemy_ReturnToSecurityPost(enemy, stateMachine, waypointService, returnPoint));
        }
    }

    public override void ExitState() { }
}
