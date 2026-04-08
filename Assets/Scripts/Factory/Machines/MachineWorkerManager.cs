using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Listens to FactoryMachine state changes and redirects workers accordingly.
/// This script demonstrates the worker-machine interaction logic described in the documentation.
/// </summary>
public class MachineWorkerManager : MonoBehaviour
{
    [SerializeField] private FactoryManager factoryManager;
    [SerializeField] private StationReservationService reservationService;
    private List<FactoryMachine> machines = new List<FactoryMachine>();

    // Track workers waiting on a specific machine
    private readonly Dictionary<RobotBrain, FactoryMachine> waitingWorkers = new();

    public void RegisterMachine(FactoryMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;
        machines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.Worker);
        machine.OnMachineStateChanged += HandleMachineStateChanged;
        machine.OnMachineTurningOff += HandleMachineTurningOff;
    }
    private void HandleMachineStateChanged(FactoryMachine machine, bool isOn)
    {
        if (isOn)
            OnMachineTurnedOn(machine);
    }

    private void HandleMachineTurningOff(FactoryMachine machine, RobotBrain worker)
    {
        OnMachineTurnedOff(machine, worker);
    }

    private void OnMachineTurnedOff(FactoryMachine machine, RobotBrain worker)
    {
        if (worker == null)
            return;

        // Store that this worker was attached to this machine
        waitingWorkers[worker] = machine;
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
                PushBrainIntent(worker, RobotTaskType.WorkAtMachine, machine, true);
            }
        }
    }

    /// <summary>
    /// Send worker to nearest rest point. Falls back to start room if none free.
    /// </summary>
    public void AssignToFirstFreePointAvailable(RobotBrain worker)
    {
        worker.OnMachineStateChanged(null, false);
    }

    private static void PushBrainIntent(RobotBrain worker, RobotTaskType taskType, object payload, bool isOn)
    {
        if (worker == null)
            return;
        worker.OnMachineStateChanged(payload, isOn);
    }

}
