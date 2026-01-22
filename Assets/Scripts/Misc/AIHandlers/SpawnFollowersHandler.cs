using System.Linq;
using UnityEngine;

/// <summary>
/// Directs worker spawners to a spawning machine and keeps them there so followers are produced.
/// </summary>
[CreateAssetMenu(menuName = "RobotAI/Handlers/SpawnFollowers", fileName = "SpawnFollowersHandler")]
public class SpawnFollowersHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        if (brain == null || brain.Body == null)
            return;

        var machine = ResolveMachine(payload);
        if (machine == null)
        {
            Debug.LogWarning("[SpawnFollowersHandler] No available spawning machine for worker spawner.");
            return;
        }

        RefreshTaskPayload(brain, machine);

        var waypoint = machine.GetComponent<RoomWaypoint>();
        if (waypoint != null)
        {
            brain.Body.SetDestination(waypoint);
        }
        else
        {
            // Force navigation to the closest waypoint even if it is marked unavailable
            // (blocked rooms often mark their waypoints as unavailable).
            brain.Body.SetDestination(machine.transform.position, includeUnavailable: true);
        }
    }

    private static SpawningMachine ResolveMachine(object payload)
    {
        if (payload is SpawningMachine spawningMachine)
            return spawningMachine;

        if (payload is BaseMachine baseMachine)
            return baseMachine as SpawningMachine;

        if (payload is RoomWaypoint waypoint && waypoint.parentRoom != null)
        {
            var room = waypoint.parentRoom;
            var candidate = room.spawningMachinesInRoom
                .FirstOrDefault(m => m != null && m.IsOn && !m.HasWorker)
                ?? room.spawningMachinesInRoom.FirstOrDefault(m => m != null);
            if (candidate != null)
                return candidate;
        }

        var reservationService = StationReservationService.Instance;
        if (reservationService != null)
            return reservationService.ReserveStation(RobotRole.WorkerSpawner) as SpawningMachine;

        return null;
    }

    private static void RefreshTaskPayload(RobotBrain brain, SpawningMachine machine)
    {
        if (brain == null || brain.Heart == null || machine == null)
            return;

        var current = brain.Heart.CurrentTask;
        if (current != null && current.Type == RobotTaskType.SpawnFollowers && ReferenceEquals(current.Payload, machine))
            return;

        float? expiry = brain.Config != null ? brain.Config.GetTimeout(RobotTaskType.SpawnFollowers) : (float?)null;
        int urgency = brain.Config != null ? brain.Config.GetUrgency(RobotTaskType.SpawnFollowers) : 0;
        brain.Heart.TryPushTask(new RobotTask(RobotTaskType.SpawnFollowers, machine, expiry, urgency));
    }
}
