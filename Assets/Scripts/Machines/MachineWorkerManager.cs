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
    [SerializeField] private List<FactoryMachine> machines;

    // Track workers waiting on a specific machine
    private readonly Dictionary<EnemyWorkerController, FactoryMachine> waitingWorkers = new();

    private void Awake()
    {
        SubscribeToMachineEvents();
    }

    /// <summary>
    /// Subscribe to each machine's state change event.
    /// </summary>
    public void SubscribeToMachineEvents()
    {
        foreach (var m in machines)
        {
            if (m != null)
                m.OnMachineStateChanged += HandleMachineStateChanged;
        }
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

        if (machine.MachineType == MachineType.RestStation)
            AssignToStartRoom(worker);
        else
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
        if (rest == null)
        {
            AssignToStartRoom(worker);
            return;
        }

        worker.stateMachine.ChangeState(new Worker_GoingToRestStation(worker, worker.stateMachine, worker.waypointService));
        worker.SetDestination(rest);
        SetWorkerState(worker, WorkerCondition.Resting);
    }

    /// <summary>
    /// Direct worker to the start room. When arrived they enter the Saved state.
    /// </summary>
    public void AssignToStartRoom(EnemyWorkerController worker)
    {
        Vector3 pos = factoryManager.GetStartCellWorldPosition();
        RoomWaypoint wp = factoryManager.GetWayPointService().GetClosestWaypoint(pos);
        worker.stateMachine.ChangeState(new Worker_GoingToStartRoom(worker, worker.stateMachine, worker.waypointService, wp));
        worker.SetDestination(wp);
    }

    /// <summary>
    /// Wrapper to update the worker activity state.
    /// </summary>
    public void SetWorkerState(EnemyWorkerController worker, WorkerCondition state)
    {
        worker.SetWorkerState(state);
    }
}

