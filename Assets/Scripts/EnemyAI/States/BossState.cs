/// <summary>
/// Classe abstraite de base pour tous les états spécifiques de l'ennemi.
/// </summary>
public abstract class BossState
{
    protected EnemyBossController enemy;
    protected BossStateMachine stateMachine;
    protected WaypointService waypointService;

    protected BossState(EnemyBossController enemy, BossStateMachine stateMachine, WaypointService waypointService)
    {
        this.enemy = enemy;
        this.stateMachine = stateMachine;
        this.waypointService = waypointService;
    }

    public abstract void EnterState();
    public abstract void UpdateState();
    public abstract void ExitState();
}