using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks active robot destination assignments to balance machine targeting.
/// </summary>
public static class RobotMachineDestinationBalancer
{
    private static readonly Dictionary<int, int> robotToWaypoint = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> waypointActiveLoads = new Dictionary<int, int>();
    private static readonly List<RoomWaypoint> tieBuffer = new List<RoomWaypoint>();

    public static void AssignDestination(int robotId, RoomWaypoint waypoint)
    {
        if (robotId == 0 || waypoint == null)
            return;

        int newWaypointId = waypoint.GetInstanceID();
        if (robotToWaypoint.TryGetValue(robotId, out int oldWaypointId))
        {
            if (oldWaypointId == newWaypointId)
                return;
            DecrementWaypointLoad(oldWaypointId);
        }

        robotToWaypoint[robotId] = newWaypointId;
        waypointActiveLoads[newWaypointId] = GetWaypointLoad(newWaypointId) + 1;
    }

    public static void ReleaseRobot(int robotId)
    {
        if (robotId == 0)
            return;

        if (!robotToWaypoint.TryGetValue(robotId, out int waypointId))
            return;

        DecrementWaypointLoad(waypointId);
        robotToWaypoint.Remove(robotId);
    }

    public static RoomWaypoint SelectLeastTargeted(IReadOnlyList<RoomWaypoint> candidates, int robotId)
    {
        if (candidates == null || candidates.Count == 0)
            return null;

        tieBuffer.Clear();
        int bestCount = int.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            RoomWaypoint waypoint = candidates[i];
            if (waypoint == null)
                continue;

            int load = GetLoadForRobot(waypoint.GetInstanceID(), robotId);
            if (load < bestCount)
            {
                bestCount = load;
                tieBuffer.Clear();
                tieBuffer.Add(waypoint);
            }
            else if (load == bestCount)
            {
                tieBuffer.Add(waypoint);
            }
        }

        if (tieBuffer.Count == 0)
            return null;
        if (tieBuffer.Count == 1)
            return tieBuffer[0];

        int randomIndex = Random.Range(0, tieBuffer.Count);
        return tieBuffer[randomIndex];
    }

    private static int GetLoadForRobot(int waypointId, int robotId)
    {
        int load = GetWaypointLoad(waypointId);
        if (robotId != 0 && robotToWaypoint.TryGetValue(robotId, out int assigned) && assigned == waypointId)
            return Mathf.Max(0, load - 1);
        return load;
    }

    private static int GetWaypointLoad(int waypointId)
    {
        return waypointActiveLoads.TryGetValue(waypointId, out int load) ? load : 0;
    }

    private static void DecrementWaypointLoad(int waypointId)
    {
        int current = GetWaypointLoad(waypointId);
        if (current <= 1)
            waypointActiveLoads.Remove(waypointId);
        else
            waypointActiveLoads[waypointId] = current - 1;
    }
}
