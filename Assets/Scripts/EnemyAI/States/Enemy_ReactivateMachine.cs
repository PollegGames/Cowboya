using UnityEngine;

/// <summary>
/// Moves the guard to a disabled machine and reactivates it when close enough.
/// </summary>
public class Enemy_ReactivateMachine : EnemyState
{
    private readonly FactoryMachine targetMachine;
    private RoomWaypoint targetPoint;
    private readonly RoomWaypoint returnPoint;
    private bool hasArrived;

    public Enemy_ReactivateMachine(
        EnemyController enemy,
        EnemyStateMachine machine,
        IWaypointService waypointService,
        FactoryMachine machineToActivate,
        RoomWaypoint returnPoint)
        : base(enemy, machine, waypointService)
    {
        targetMachine = machineToActivate;
        this.returnPoint = returnPoint;
    }

    public override void EnterState()
    {
        enemy.EnemyStatus = EnemyStatus.ReactivatingMachine;
        Debug.Log($"Entering ReactivateMachine state for {targetMachine.name}");
        hasArrived = false;
        if (targetMachine == null)
        {
            Debug.LogError("Target machine is null, cannot reactivate.");
            stateMachine.ChangeState(new Enemy_Idle(enemy, stateMachine, waypointService));
            return;
        }
        targetPoint = waypointService.GetClosestWaypoint(targetMachine.transform.position);
        enemy.SetDestination(targetPoint, includeUnavailable: true);
    }

    public override void UpdateState()
    {
        if (hasArrived) return;
        if (enemy.HasArrivedAtDestination())
        {
            Debug.Log($"Machine {targetMachine.name} reactivated by {enemy.name}");
            hasArrived = true;
            enemy.SetMovement(0f);
            enemy.SetVerticalMovement(0f);
            targetMachine.SetState(true);
            stateMachine.ChangeState(new Enemy_ReturnToSecurityPost(enemy, stateMachine, waypointService, returnPoint));
        }
    }

    public override void ExitState() { }
}

