using System.Collections.Generic;
using UnityEngine;

/// <summary>Supplies shared navigation dependencies to every robot already authored in a static scene.</summary>
public static class StaticRobotNavigationInitializer
{
    private static WaypointService sharedWaypointService;
    private static readonly Dictionary<int, string> lastDiagnostics = new Dictionary<int, string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedService()
    {
        sharedWaypointService = null;
        lastDiagnostics.Clear();
    }

    /// <summary>Retains the verified service used by the active static level.</summary>
    public static void SetSharedService(IWaypointService waypointService)
    {
        sharedWaypointService = waypointService as WaypointService;
        Debug.Log($"[WorkerCollectorDiagnostics] Shared navigation service="
            + $"{(sharedWaypointService != null ? sharedWaypointService.name : "null")}, "
            + $"waypoints={(sharedWaypointService != null ? sharedWaypointService.GetAllWaypoints().Count : 0)}, "
            + $"mode={(sharedWaypointService != null ? sharedWaypointService.GraphMode.ToString() : "none")}.");
    }

    /// <summary>Initializes one robot with the shared static navigation service.</summary>
    public static bool InitializeRobot(GameObject robot, IWaypointService waypointService)
    {
        if (robot == null || waypointService == null)
            return false;

        RobotBodyController body = robot.GetComponent<RobotBodyController>();
        if (body == null)
            return false;

        body.Initialize(waypointService, waypointService, null);
        RobotMemoryNew memory = robot.GetComponent<RobotMemoryNew>();
        if (memory != null)
            memory.InitializeWaypointAvailability(waypointService.GetAllWaypoints());
        return body.IsNavigationInitialized;
    }

    /// <summary>
    /// Initializes a dynamically spawned robot when the scene's shared graph is ready.
    /// </summary>
    public static bool TryInitializeSpawnedRobot(GameObject robot)
    {
        if (robot == null)
            return false;

        WaypointService service = sharedWaypointService;
        bool usingVerifiedService = service != null;
        if (service == null)
        {
            WaypointService[] services = Object.FindObjectsByType<WaypointService>(FindObjectsSortMode.None);
            if (services.Length != 1)
                return Report(robot, $"failed: sharedService=null, discoveredServices={services.Length}");
            service = services[0];
        }

        if (service.GetAllWaypoints().Count == 0)
            return Report(robot, $"failed: service={service.name}, waypoints=0, mode={service.GraphMode}");

        if (!usingVerifiedService && service.GraphMode == WaypointGraphMode.StaticExplicit)
        {
            List<StaticLevelPath> paths = StaticLevelPath.FindEnabledInScene();
            if (paths.Count != 1 || !paths[0].IsNavigationReady)
                return Report(robot, $"failed: service={service.name}, paths={paths.Count}, "
                    + $"pathReady={(paths.Count == 1 && paths[0].IsNavigationReady)}, "
                    + $"waypoints={service.GetAllWaypoints().Count}, mode={service.GraphMode}");
        }

        RobotBodyController body = robot.GetComponent<RobotBodyController>();
        if (body == null)
            return Report(robot, $"failed: robot has no {nameof(RobotBodyController)}");

        bool initialized = InitializeRobot(robot, service);
        return Report(robot, initialized
            ? $"success: service={service.name}, waypoints={service.GetAllWaypoints().Count}, mode={service.GraphMode}"
            : $"failed: body initialization returned false, bodyReference="
                + $"{(body.BodyReference != null ? body.BodyReference.name : "null")}");
    }

    private static bool Report(GameObject robot, string message)
    {
        int id = robot.GetInstanceID();
        if (!lastDiagnostics.TryGetValue(id, out string previous) || previous != message)
        {
            Debug.Log($"[WorkerCollectorDiagnostics] Navigation robot={robot.name} {message}.", robot);
            lastDiagnostics[id] = message;
        }
        return message.StartsWith("success:");
    }

    /// <summary>Initializes all active scene robot bodies and their waypoint memories.</summary>
    public static int InitializeSceneRobots(IWaypointService waypointService)
    {
        if (waypointService == null)
            return 0;

        SetSharedService(waypointService);

        RobotBodyController[] bodies = Object.FindObjectsByType<RobotBodyController>(FindObjectsSortMode.None);
        foreach (RobotBodyController body in bodies)
        {
            if (body == null)
                continue;

            InitializeRobot(body.gameObject, waypointService);
        }

        return bodies.Length;
    }
}
