using UnityEngine;

/// <summary>
/// Moves the guard to a disabled machine and reactivates it when close enough.
/// </summary>
public class Enemy_ReactivateMachine : EnemyState
{
    private readonly FactoryMachine targetMachine;
    private RoomWaypoint targetPoint;
    private bool hasArrived;

    public Enemy_ReactivateMachine(
        EnemyController enemy,
        EnemyStateMachine machine,
        IWaypointService waypointService,
        FactoryMachine machineToActivate)
        : base(enemy, machine, waypointService)
    {
        targetMachine = machineToActivate;
    }

    public override void EnterState()
    {
        hasArrived = false;
        if (targetMachine == null)
        {
            stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService));
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
            targetMachine.SetState(true);
            stateMachine.ChangeState(new Enemy_SecurityGuardRest(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState() { }
}

