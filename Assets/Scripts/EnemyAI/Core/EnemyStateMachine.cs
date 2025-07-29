using UnityEngine;

/// <summary>
/// Finite state machine (FSM) that controls the various enemy behaviours.
/// </summary>
public class EnemyStateMachine : MonoBehaviour, IEnemyStateMachine
{
    private EnemyState currentState;

    public EnemyState CurrentState => currentState;
    private void Update()
    {
        currentState?.UpdateState();
    }

    /// <summary>
    /// Changes the enemy's current state.
    /// </summary>
    /// <param name="newState">The new state to activate.</param>
    public void ChangeState(EnemyState newState)
    {
        currentState?.ExitState();
        currentState = newState;
        currentState?.EnterState();
    }
}
