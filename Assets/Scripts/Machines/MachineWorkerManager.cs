using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Listens to FactoryMachine state changes and redirects workers accordingly.
/// This script demonstrates the worker–machine interaction logic described in the documentation.
/// </summary>
public class MachineWorkerManager : MonoBehaviour
{
    [SerializeField] private FactoryManager factoryManager;
    private List<FactoryMachine> machines = new List<FactoryMachine>();

    // Track workers waiting on a specific machine
    private readonly Dictionary<EnemyWorkerController, FactoryMachine> waitingWorkers = new();

    public void RegisterMachine(FactoryMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;
        machines.Add(machine);
        machine.OnMachineStateChanged += HandleMachineStateChanged;
    }
    private void HandleMachineStateChanged(FactoryMachine machine, bool isOn)
    {
        if (isOn)
            OnMachineTurnedOn(machine);
        else
            OnMachineTurnedOff(machine);
    }

    private void OnMachineTurnedOff(FactoryMachine machine)
    {
        var worker = machine.CurrentWorker;
        if (worker == null)
            return;

        // Store that this worker was attached to this machine
        waitingWorkers[worker] = machine;


        AssignToRestPoint(worker);
    }

    private void OnMachineTurnedOn(FactoryMachine machine)
    {
        // Any worker waiting for this machine may return to work
        foreach (var pair in waitingWorkers.ToList())
        {
            if (pair.Value == machine)
            {
                var worker = pair.Key;
                waitingWorkers.Remove(worker);
                SetWorkerState(worker, WorkerCondition.Active);
                // Pseudocode: tell worker to resume work on this machine
                // worker.stateMachine.ChangeState(new Worker_GoingToLeastWorkedStation(worker, worker.stateMachine, worker.waypointService));
            }
        }
    }

    /// <summary>
    /// Send worker to nearest rest point. Falls back to start room if none free.
    /// </summary>
    public void AssignToRestPoint(EnemyWorkerController worker)
    {
        var rest = factoryManager.GetWayPointService().GetFirstRestPoint(worker.memory.LastVisitedPoint);
        worker.stateMachine.ChangeState(new Worker_GoingToRestStation(worker, worker.stateMachine, worker.waypointService));
        worker.SetDestination(rest);
        SetWorkerState(worker, WorkerCondition.Resting);
    }

    /// <summary>
    /// Wrapper to update the worker activity state.
    /// </summary>
    public void SetWorkerState(EnemyWorkerController worker, WorkerCondition state)
    {
        Debug.Log($"Setting worker {worker.name} state to {state}");
        worker.SetWorkerState(state);
    }
}

