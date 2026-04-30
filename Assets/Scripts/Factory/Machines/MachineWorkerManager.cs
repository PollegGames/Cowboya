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
    private readonly List<BaseMachine> machines = new List<BaseMachine>();

    // Track workers waiting on a specific machine
    private readonly Dictionary<RobotBrainNew, BaseMachine> waitingWorkers = new();

    public void RegisterMachine(FactoryMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;
        machines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.Worker);
        machine.OnMachinePowerChanged += HandleMachinePowerChanged;
        machine.OnMachineTurnedOff += HandleMachineTurnedOff;
    }

    public void RegisterMachine(RestingMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;

        machines.Add(machine);
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
        OnMachineTurnedOn(evt.Machine);
    }

    private void HandleMachineTurnedOff(MachineTurnedOffEvent evt)
    {
        if (evt.Machine is not FactoryMachine && evt.Machine is not RestingMachine)
            return;

        var worker = TryResolveRobotBrain(evt.PreviousRobot);
        if (worker == null && evt.Machine is FactoryMachine factoryMachine)
            worker = factoryMachine.CurrentWorker;
        if (worker == null && evt.Machine is RestingMachine restingMachine)
            worker = restingMachine.CurrentWorker;

        OnMachineTurnedOff(evt.Machine, worker);
    }

    private void OnMachineTurnedOff(BaseMachine machine, RobotBrainNew worker)
    {
        NotifyWorkersMachinePoweredOff(machine, worker);

        if (worker == null)
        {
            Debug.Log($"[MachineWorkerManager] Machine turned off without resolved worker machine={(machine != null ? machine.name : "null")}", machine);
            return;
        }

        // Store that this worker was attached to this machine
        waitingWorkers[worker] = machine;
        Debug.Log($"[MachineWorkerManager] Stored waiting worker={worker.name} machine={machine.name}", machine);
    }

    private void NotifyWorkersMachinePoweredOff(BaseMachine machine, RobotBrainNew releasedWorker)
    {
        if (machine == null)
            return;

        var machineWaypoint = MachineWaypointResolver.Resolve(machine);
        var workers = FindObjectsByType<RobotBrainNew>(FindObjectsSortMode.None);
        foreach (var brain in workers)
        {
            if (brain == null || brain.Heart == null || brain.Heart.Role != RobotRole.Worker || brain.Memory == null)
                continue;

            RobotTask currentTask = brain.Heart.CurrentTask;
            bool targetsPoweredOffMachine = !ReferenceEquals(brain, releasedWorker)
                && IsWorkerTargetingMachine(brain, machine, machineWaypoint);
            if (currentTask != null
                && currentTask.Type == RobotTaskType.GoToMachine
                && !targetsPoweredOffMachine)
            {
                if (machineWaypoint != null)
                    brain.Memory.SetRoomWaypointAvailability(machineWaypoint, false);
                else
                    brain.Memory.SetMachineWaypointAvailability(machine, false);

                RobotEcosystemProbe.RecordBrainCall(
                    brain,
                    "MachineWorkerManager.NotifyWorkersMachinePoweredOff",
                    "machine=" + machine.name
                    + " targetInvalidationDeferred=True"
                    + " destinationPreserved=" + DescribeTaskTarget(currentTask));
                continue;
            }

            if (machineWaypoint != null)
                brain.Memory.SetRoomWaypointAvailability(machineWaypoint, false);
            else
                brain.Memory.SetMachineWaypointAvailability(machine, false);

            if (!targetsPoweredOffMachine)
            {
                RobotEcosystemProbe.RecordBrainCall(
                    brain,
                    "MachineWorkerManager.NotifyWorkersMachinePoweredOff",
                    "machine=" + machine.name + " waypointInvalidated=True");
                continue;
            }

            brain.Memory.ChangeConnectionToMachine(false);
            brain.Memory.SetDesiredMachineType(machine.Type);
            RobotEcosystemProbe.RecordBrainCall(
                brain,
                "MachineWorkerManager.NotifyWorkersMachinePoweredOff",
                "machine=" + machine.name + " targetInvalidated=True");
            brain.Heart.BlockCurrentTask();
        }
    }

    private static string DescribeTaskTarget(RobotTask task)
    {
        if (task == null || task.Payload == null)
            return "none";

        if (task.Payload is RoomWaypoint waypoint)
            return waypoint.type + "@" + waypoint.WorldPos.ToString("F2");

        if (task.Payload is BaseMachine machine)
            return machine.name;

        if (task.Payload is Component component && component != null)
            return component.name;

        if (task.Payload is GameObject gameObject && gameObject != null)
            return gameObject.name;

        return task.Payload.ToString();
    }

    private static string DescribeWaypoint(RoomWaypoint waypoint)
    {
        if (waypoint == null)
            return "none";

        return waypoint.type + "@" + waypoint.WorldPos.ToString("F2");
    }

    private static bool IsWorkerTargetingMachine(RobotBrainNew brain, BaseMachine machine, RoomWaypoint machineWaypoint)
    {
        if (brain == null || machine == null)
            return false;

        RobotTask currentTask = brain.Heart != null ? brain.Heart.CurrentTask : null;
        if (currentTask == null)
            return false;

        if (currentTask.Type != RobotTaskType.GoToMachine
            && currentTask.Type != RobotTaskType.WorkAtMachine
            && currentTask.Type != RobotTaskType.Rest)
        {
            return false;
        }

        if (PayloadTargetsMachine(currentTask.Payload, machine, machineWaypoint))
            return true;

        return machineWaypoint != null
            && brain.Body != null
            && ReferenceEquals(brain.Body.CurrentTarget, machineWaypoint);
    }

    private static bool PayloadTargetsMachine(object payload, BaseMachine machine, RoomWaypoint machineWaypoint)
    {
        if (payload == null || machine == null)
            return false;

        if (ReferenceEquals(payload, machine))
            return true;

        if (payload is RoomWaypoint waypoint)
            return machineWaypoint != null && ReferenceEquals(waypoint, machineWaypoint);

        if (payload is Component component && component != null)
            return ReferenceEquals(component.GetComponent<BaseMachine>() ?? component.GetComponentInParent<BaseMachine>(), machine);

        if (payload is GameObject gameObject && gameObject != null)
            return ReferenceEquals(gameObject.GetComponent<BaseMachine>() ?? gameObject.GetComponentInParent<BaseMachine>(), machine);

        return false;
    }

    private void OnMachineTurnedOn(BaseMachine machine)
    {
        NotifyWorkersMachinePoweredOn(machine);

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

    private void NotifyWorkersMachinePoweredOn(BaseMachine machine)
    {
        if (machine == null)
            return;

        var machineWaypoint = MachineWaypointResolver.Resolve(machine);
        var workers = FindObjectsByType<RobotBrainNew>(FindObjectsSortMode.None);
        foreach (var brain in workers)
        {
            if (brain == null || brain.Heart == null || brain.Heart.Role != RobotRole.Worker || brain.Memory == null)
                continue;

            if (machineWaypoint != null)
                brain.Memory.SetRoomWaypointAvailability(machineWaypoint, true);
            else
                brain.Memory.SetMachineWaypointAvailability(machine, true);

            RobotEcosystemProbe.RecordBrainCall(
                brain,
                "MachineWorkerManager.NotifyWorkersMachinePoweredOn",
                "machine=" + machine.name
                + " waypointRestored=" + (machineWaypoint != null)
                + " waypoint=" + DescribeWaypoint(machineWaypoint));
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

