using UnityEngine;

/// <summary>
/// Moves the guard to a disabled machine and reactivates it when close enough.
/// </summary>
public class Enemy_ReactiveRestingMachine : EnemyState
{
    private readonly RestingMachine targetMachine;
    private RoomWaypoint targetPoint;
    private readonly RoomWaypoint returnPoint;
    private bool hasArrived;

    public Enemy_ReactiveRestingMachine(
        EnemyController enemy,
        EnemyStateMachine machine,
        IWaypointService waypointService,
        RestingMachine machineToActivate,
        RoomWaypoint returnPoint)
        : base(enemy, machine, waypointService)
    {
        targetMachine = machineToActivate;
        this.returnPoint = returnPoint;
    }

    public override void EnterState()
    {
         // Priorité : passer en attaque si le joueur est connu et qu'on a été attaqué
        if (enemy.memory.LastKnownPlayerPosition != Vector3.zero && enemy.memory.WasRecentlyAttacked)
        {
            stateMachine.ChangeState(new Enemy_AttackPlayer(enemy, stateMachine, waypointService, this));
        }
        enemy.EnemyStatus = EnemyStatus.ReactivatingMachine;
        Debug.Log($"Entering ReactivateMachine state for {targetMachine.name}");
        hasArrived = false;
        if (targetMachine == null)
        {
            Debug.LogError("Target machine is null, cannot reactivate.");
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

