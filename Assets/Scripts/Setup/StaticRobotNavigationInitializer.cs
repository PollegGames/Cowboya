using System.Collections.Generic;
using UnityEngine;

/// <summary>Supplies shared navigation dependencies to every robot already authored in a static scene.</summary>
public static class StaticRobotNavigationInitializer
{
    /// <summary>Initializes all active scene robot bodies and their waypoint memories.</summary>
    public static int InitializeSceneRobots(IWaypointService waypointService)
    {
        if (waypointService == null)
            return 0;

        List<RoomWaypoint> waypoints = waypointService.GetAllWaypoints();
        RobotBodyController[] bodies = Object.FindObjectsByType<RobotBodyController>(FindObjectsSortMode.None);
        foreach (RobotBodyController body in bodies)
        {
            if (body == null)
                continue;

            body.Initialize(waypointService, waypointService, null);
            RobotMemoryNew memory = body.GetComponent<RobotMemoryNew>();
            if (memory != null)
                memory.InitializeWaypointAvailability(waypoints);
        }

        return bodies.Length;
    }
}
