/// <summary>
/// Base abstract class for all specific enemy states.
/// </summary>
public abstract class WorkerState
{
    protected EnemyWorkerController enemy;
    protected WorkerStateMachine stateMachine;
    protected IWaypointService waypointService;
    protected WorkerState(EnemyWorkerController enemy, WorkerStateMachine stateMachine, IWaypointService waypointService)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
        this.waypointService = waypointService;
    }

    public abstract void EnterState();
    public abstract void UpdateState();
    public abstract void ExitState();
}

public enum WorkerStatus
{
    ReadyToWork,
    ReadyToRest,
    Working,
    GoingToWork,
    GoingToRest,
    Idle,
    Resting,
    ReadyToSpawnFollowers,
    GoingToStartRoom,
    Saved,
    Spawning,
    GoingToSpawningMachine
}