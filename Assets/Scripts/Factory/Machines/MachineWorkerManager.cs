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
    private readonly Dictionary<RobotBrainNew, FactoryMachine> waitingWorkers = new();

    public void RegisterMachine(FactoryMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;
        machines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.Worker);
        machine.OnMachinePowerChanged += HandleMachinePowerChanged;
        machine.OnMachineTurnedOff += HandleMachineTurnedOff;
    }

    private void OnDestroy()
    {
        foreach (var machine in machines)
        {
            if (machine == null)
                continue;
            machine.OnMachinePowerChanged -= HandleMachinePowerChanged;
            machine.OnMachineTurnedOff -= HandleMachineTurnedOff;
        }
    }

    private void HandleMachinePowerChanged(MachinePowerChangedEvent evt)
    {
        if (!evt.IsOn)
            return;
        if (evt.Machine is FactoryMachine machine)
            OnMachineTurnedOn(machine);
    }

    private void HandleMachineTurnedOff(MachineTurnedOffEvent evt)
    {
        if (evt.Machine is not FactoryMachine machine)
            return;

        var worker = TryResolveRobotBrain(evt.PreviousRobot);
        if (worker == null)
            worker = machine.CurrentWorker;
        OnMachineTurnedOff(machine, worker);
    }

    private void OnMachineTurnedOff(FactoryMachine machine, RobotBrainNew worker)
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
                RobotDomainEventBus.PublishMachineStateDispatch(worker, machine, true);
            }
        }
    }

    /// <summary>
    /// Send worker to nearest rest point. Falls back to start room if none free.
    /// </summary>
    public void AssignToFirstFreePointAvailable(RobotBrainNew worker)
    {
        if (worker == null)
            return;
        RobotDomainEventBus.PublishMachineStateDispatch(worker, null, false);
    }

    private static RobotBrainNew TryResolveRobotBrain(GameObject robot)
    {
        if (robot == null)
            return null;

        var brain = robot.GetComponent<RobotBrainNew>();
        if (brain != null)
            return brain;

        return robot.GetComponentInParent<RobotBrainNew>();
    }

}

