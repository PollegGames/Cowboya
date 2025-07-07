using UnityEngine;

/// <summary>
/// Machine à états finis (FSM) pour gérer les différents comportements d'un ennemi.
/// </summary>
public class WorkerStateMachine : MonoBehaviour, IWorkerStateMachine
{
    private WorkerState currentState;

    public WorkerState enemyState
    {
        get => currentState;
        set
        {
            if (currentState != null)
            {
                currentState.ExitState();
            }
            currentState = value;
            currentState?.EnterState();
        }
    }

    private void Update()
    {
        currentState?.UpdateState();
    }

    /// <summary>
    /// Permet de changer l'état actuel de l'ennemi.
    /// </summary>
    /// <param name="newState">Le nouvel état à activer.</param>
    public void ChangeState(WorkerState newState)
    {
        currentState?.ExitState();
        currentState = newState;
        currentState?.EnterState();
    }
}
