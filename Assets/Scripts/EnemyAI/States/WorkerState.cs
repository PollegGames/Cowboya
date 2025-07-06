/// <summary>
/// Classe abstraite de base pour tous les états spécifiques de l'ennemi.
/// </summary>
public abstract class WorkerState
{
    protected EnemyWorkerController enemy;
    protected EnemyStateMachine stateMachine;
    protected IWaypointService waypointService;
    protected WorkerState(EnemyWorkerController enemy, EnemyStateMachine stateMachine, IWaypointService waypointService)
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
    Working,
    GoingToWork,
    GoingToRest,
    Idle
}