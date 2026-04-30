using UnityEngine;

public static class MachineWaypointResolver
{
    public static RoomWaypoint Resolve(BaseMachine machine)
    {
        if (machine == null)
            return null;

        var waypoint = machine.GetComponent<RoomWaypoint>() ?? machine.GetComponentInParent<RoomWaypoint>();
        if (waypoint != null)
            return waypoint;

        WaypointType? waypointType = MapMachineTypeToWaypointType(machine.Type);
        if (!waypointType.HasValue || machine.WaypointService == null)
            return null;

        RoomWaypoint best = null;
        float bestDistance = float.MaxValue;
        var waypoints = machine.WaypointService.GetAllWaypoints();
        if (waypoints == null)
            return null;

        foreach (var candidate in waypoints)
        {
            if (candidate == null || candidate.type != waypointType.Value)
                continue;

            float distance = Vector2.Distance(machine.transform.position, candidate.WorldPos);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    public static WaypointType? MapMachineTypeToWaypointType(MachineType type)
    {
        switch (type)
        {
            case MachineType.WorkStation:
                return WaypointType.Work;
            case MachineType.RestStation:
                return WaypointType.Rest;
            case MachineType.SecurityMachine:
                return WaypointType.Security;
            case MachineType.SpawningMachine:
                return WaypointType.Spawner;
            default:
                return null;
        }
    }
}
