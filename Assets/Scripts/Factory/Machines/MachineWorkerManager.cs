using System.Collections.Generic;
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

    public void RegisterMachine(FactoryMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;
        machines.Add(machine);
        reservationService?.RegisterMachine(machine, RobotRole.Worker);
        machine.OnMachinePowerChanged += HandleMachinePowerChanged;
        machine.OnMachineTurnedOff += HandleMachineTurnedOff;
        machine.OnRobotFreed += HandleMachineFreed;
    }

    public void RegisterMachine(RestingMachine machine)
    {
        if (machine == null || machines.Contains(machine))
            return;

        machines.Add(machine);
        machine.OnMachinePowerChanged += HandleMachinePowerChanged;
        machine.OnMachineTurnedOff += HandleMachineTurnedOff;
        machine.OnRobotFreed += HandleMachineFreed;
    }

    private void OnDestroy()
    {
        foreach (var machine in machines)
        {
            if (machine == null)
                continue;
            machine.OnMachinePowerChanged -= HandleMachinePowerChanged;
            machine.OnMachineTurnedOff -= HandleMachineTurnedOff;
            machine.OnRobotFreed -= HandleMachineFreed;
        }
    }

    private void HandleMachineFreed(BaseMachine machine)
    {
        if (machine != null && machine.IsOn)
            NotifyWorkersMachinePoweredOn(machine);
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
            Debug.Log($"[MachineWorkerManager] Machine turned off without resolved worker machine={(machine != null ? machine.name : "null")}", machine);
    }

    private void NotifyWorkersMachinePoweredOff(BaseMachine machine, RobotBrainNew releasedWorker)
    {
        if (machine == null)
            return;

        var machineWaypoint = MachineWaypointResolver.Resolve(machine);
        if (releasedWorker == null)
        {
            Debug.Log($"[MachineWorkerManager] Powered-off machine has no attached worker to notify machine={machine.name}", machine);
            return;
        }

        if (releasedWorker.Heart == null || releasedWorker.Heart.Role != RobotRole.Worker || releasedWorker.Memory == null)
        {
            RobotEcosystemProbe.RecordBrainCall(
                releasedWorker,
                "MachineWorkerManager.NotifyWorkersMachinePoweredOff",
                "machine=" + machine.name
                + " attachedWorkerNotified=True"
                + " memoryUpdated=False"
                + " taskBlocked=False"
                + " skippedInvalidWorkerState=True");
            return;
        }

        RobotTask currentTask = releasedWorker.Heart.CurrentTask;
        if (machineWaypoint != null)
            releasedWorker.Memory.SetRoomWaypointAvailability(machineWaypoint, false);
        else
            releasedWorker.Memory.SetMachineWaypointAvailability(machine, false);

        releasedWorker.Memory.ChangeConnectionToMachine(false);
        releasedWorker.Memory.SetDesiredMachineType(machine.Type);
        RobotEcosystemProbe.RecordBrainCall(
            releasedWorker,
            "MachineWorkerManager.NotifyWorkersMachinePoweredOff",
            "machine=" + machine.name
            + " attachedWorkerNotified=True"
            + " memoryUpdated=True"
            + " taskBlocked=False"
            + " currentTask=" + (currentTask != null ? currentTask.Type.ToString() : "None")
            + " currentTarget=" + DescribeTaskTarget(currentTask));
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

    private void OnMachineTurnedOn(BaseMachine machine)
    {
        NotifyWorkersMachinePoweredOn(machine);
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

