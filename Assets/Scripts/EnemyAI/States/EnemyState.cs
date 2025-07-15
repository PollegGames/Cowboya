/// <summary>
/// Classe abstraite de base pour tous les états spécifiques de l'ennemi.
/// </summary>
public abstract class EnemyState
{
    protected EnemyController enemy;
    protected EnemyStateMachine stateMachine;
    protected IWaypointService waypointService;

    protected EnemyState(EnemyController enemy, EnemyStateMachine stateMachine, IWaypointService waypointService)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
        this.waypointService = waypointService;
    }

    public abstract void EnterState();
    public abstract void UpdateState();
    public abstract void ExitState();
}

public enum EnemyStatus
{
    ReadyToCheckSecurity,
    CheckingSecurity,
    GoingToSecurityPost,
    GoingToRest,
    Idle,
    Resting,
    GoingToStartRoom,
    Saved,
    ReactivatingMachine,
    Following
}