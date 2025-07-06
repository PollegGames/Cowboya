using UnityEngine;

/// <summary>
/// Exemple d'implémentation d'un état simple : Work (stationnaire).
/// </summary>
public class Enemy_WaitBeforeGoingToPOI : EnemyState
{
    private const float Work_DURATION = 5f;
    private float _timer;
    public Enemy_WaitBeforeGoingToPOI(EnemyController enemy,
                                    BossStateMachine machine,
                                    IWaypointService waypointService)
      : base(enemy, machine, waypointService)
    {
        _timer = 0f;
    }

    public override void EnterState()
    {
        _timer = 0f;
        enemy.SetMovement(0);
    }

    public override void UpdateState()
    {
         _timer += Time.deltaTime;
        if (_timer >= Work_DURATION)
        {
            Debug.Log("[Work] 5 seconds elapsed → moving to next POI.");
            stateMachine.ChangeState(new Enemy_MoveToPOICell(enemy, stateMachine, waypointService));
        }
    }

    public override void ExitState()
    {
        // Nettoyage éventuel à la sortie de l'état
    }
}
