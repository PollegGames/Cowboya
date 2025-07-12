using UnityEngine;

public class RestState : WorkerState
{
    private BaseMachine machine;
    private const float RestDuration = 5f;
    private float timer;

    public RestState(EnemyWorkerController enemy, WorkerStateMachine machineSM, IWaypointService waypointService, BaseMachine machine)
        : base(enemy, machineSM, waypointService)
    {
        this.machine = machine;
    }

    public override void EnterState()
    {
        timer = 0f;
        enemy.workerState = WorkerStatus.Resting;
        enemy.SetMovement(0f);
    }

    public override void UpdateState()
    {
        timer += Time.deltaTime;
        if (timer >= RestDuration)
        {
            stateMachine.ChangeState(new Worker_GoingToLeastWorkedStation(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
        machine?.ReleaseRobot();
    }
}
