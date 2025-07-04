/// <summary>
/// Classe abstraite de base pour tous les états spécifiques de l'ennemi.
/// </summary>
public abstract class EnemyState
{
    protected EnemyController enemy;
    protected EnemyStateMachine stateMachine;
    protected WaypointService waypointService;
    protected EnemyState(EnemyController enemy, EnemyStateMachine stateMachine, WaypointService waypointService)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
        this.waypointService = waypointService;
    }

    public abstract void EnterState();
    public abstract void UpdateState();
    public abstract void ExitState();
}