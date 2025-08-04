using UnityEngine;

/// <summary>
/// Worker flees from the player when they exhibit low morality.
/// </summary>
public class Worker_FleePlayer : WorkerState
{
    private readonly WorkerState previousState;
    private readonly Transform player;
    private readonly FactoryMachine storedMachine;
    private bool machineReleased;

    public Worker_FleePlayer(EnemyWorkerController enemy,
                             WorkerStateMachine machineSM,
                             IWaypointService waypointService,
                             WorkerState previousState,
                             Transform player,
                             FactoryMachine currentMachine)
        : base(enemy, machineSM, waypointService)
    {
        this.previousState = previousState;
        this.player = player;
        storedMachine = currentMachine;
        machineReleased = currentMachine == null;
    }

    public override void EnterState()
    {
        enemy.workerState = WorkerStatus.RunningAway;

        if (!machineReleased && storedMachine != null)
        {
            machineReleased = true;
            storedMachine.ReleaseRobot();
            enemy.ClearCurrentMachine();
            stateMachine.ChangeState(this);
            return;
        }

        enemy.SetDestination(GetFleeWaypoint(), includeUnavailable: true);

        var audio = enemy.GetComponent<AudioSource>();
        audio?.Play();
    }

    private RoomWaypoint GetFleeWaypoint()
    {
        Vector2 direction = (enemy.transform.position - player.position).normalized;
        Vector2 targetPosition = (Vector2)enemy.transform.position + direction * 12f;
        return waypointService.GetClosestWaypoint(targetPosition, includeUnavailable: true);
    }

    public override void UpdateState()
    {
        if (Vector2.Distance(enemy.transform.position, player.position) > 10f)
        {
            if (storedMachine != null)
            {
                stateMachine.ChangeState(new Worker_GoingToMachine(enemy, stateMachine, waypointService, storedMachine));
            }
            else
            {
                stateMachine.ChangeState(previousState);
            }
            return;
        }

        if (enemy.HasArrivedAtDestination())
        {
            enemy.SetDestination(GetFleeWaypoint(), includeUnavailable: true);
        }
    }

    public override void ExitState()
    {
        enemy.SetMovement(0f);
        enemy.SetVerticalMovement(0f);
    }
}
