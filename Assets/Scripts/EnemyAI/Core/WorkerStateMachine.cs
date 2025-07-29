using UnityEngine;

/// <summary>
/// Finite state machine (FSM) that controls the various worker behaviours.
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
    /// Changes the worker's current state.
    /// </summary>
    /// <param name="newState">The new state to activate.</param>
    public void ChangeState(WorkerState newState)
    {
        currentState?.ExitState();
        currentState = newState;
        currentState?.EnterState();
    }
}
