using UnityEngine;

/// <summary>
/// Machine à états finis (FSM) pour gérer les différents comportements d'un ennemi.
/// </summary>
public class BossStateMachine : MonoBehaviour
{
    private BossState currentState;

    public BossState CurrentState => currentState;
    private void Update()
    {
        currentState?.UpdateState();
    }

    /// <summary>
    /// Permet de changer l'état actuel de l'ennemi.
    /// </summary>
    /// <param name="newState">Le nouvel état à activer.</param>
    public void ChangeState(BossState newState)
    {
        currentState?.ExitState();
        currentState = newState;
        currentState?.EnterState();
    }
}
