using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Reusable room capability that spawns Worker Collectors and exposes their rest destination.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorSpawnRestProvider : MonoBehaviour
{
    [SerializeField] private GameObject workerCollectorPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private RoomWaypoint restWaypoint;
    [SerializeField] private Transform restPoint;
    [SerializeField, Min(0f)] private float restDuration = 5f;
    [SerializeField, Min(1)] private int maximumLiveCollectors = 1;

    private readonly List<GameObject> spawnedCollectors = new List<GameObject>();

    public RoomWaypoint RestWaypoint => restWaypoint;
    public Vector3 RestPosition => restPoint != null ? restPoint.position : transform.position;
    public float RestDuration => Mathf.Max(0f, restDuration);
    public int MaximumLiveCollectors => Mathf.Max(1, maximumLiveCollectors);
    public Vector3 SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;

    // Static rooms use an XZ presentation hierarchy that is commonly pitched -90 degrees.
    // The worker collector is a 2D rig, so only its prefab-authored rotation is valid here.
    public Quaternion SpawnRotation => workerCollectorPrefab != null
        ? workerCollectorPrefab.transform.rotation
        : Quaternion.identity;

    private void Awake() => ResolveReferences();

    private void OnEnable()
    {
        ResolveReferences();
        WorkerCollectorMissionService.RegisterRest(this);
    }

    private void OnDisable() => WorkerCollectorMissionService.UnregisterRest(this);

    public void Configure(GameObject prefab, Transform configuredSpawnPoint, RoomWaypoint waypoint,
        Transform configuredRestPoint, float duration, int maximumLive = 1)
    {
        workerCollectorPrefab = prefab;
        spawnPoint = configuredSpawnPoint;
        restWaypoint = waypoint;
        restPoint = configuredRestPoint;
        restDuration = Mathf.Max(0f, duration);
        maximumLiveCollectors = Mathf.Max(1, maximumLive);
    }

    /// <summary>
    /// Spawns one collector when this provider is below its configured live-instance limit.
    /// </summary>
    public GameObject TrySpawnCollector()
    {
        return TrySpawnCollector(null);
    }

    /// <summary>Compatibility spawn path for static scenes with explicit Garage ownership.</summary>
    public GameObject TrySpawnCollector(WorkerCollectorDropOffProvider garage)
    {
        spawnedCollectors.RemoveAll(collector => collector == null);
        if (!Application.isPlaying || workerCollectorPrefab == null
            || spawnedCollectors.Count >= MaximumLiveCollectors)
            return null;

        Transform point = spawnPoint != null ? spawnPoint : transform;
        GameObject staging = new GameObject("WorkerCollectorSpawnStaging");
        staging.SetActive(false);
        GameObject collector = Instantiate(workerCollectorPrefab, point.position, SpawnRotation, staging.transform);
        collector.SetActive(false);
        collector.transform.SetParent(null, true);
        collector.name = workerCollectorPrefab.name;
        AlignNavigationBodyToSpawn(collector, point.position);
        RobotStateController state = collector.GetComponent<RobotStateController>();
        if (state != null)
        {
            state.Stats = new WorkerRobotFactory().CreateRobot();
            state.Stats.RobotName = "Worker Collector";
        }

        WorkerCollectorMissionService.BindGarage(
            collector.GetComponent<WorkerCollectorBodyController>(), garage);

        // Heart starts the initial WorkerCollectorFindCube task synchronously from
        // OnEnable. Navigation must therefore be installed while the clone is still
        // inactive, before that task can request its first mission.
        bool navigationInitialized = StaticRobotNavigationInitializer.TryInitializeSpawnedRobot(collector);
        RobotBodyController robotBody = collector.GetComponent<RobotBodyController>();
        Debug.Log($"[WorkerCollectorDiagnostics] Spawn marker={point.position:F2}, "
            + $"root={collector.transform.position:F2}, "
            + $"body={(robotBody != null && robotBody.BodyReference != null ? robotBody.BodyReference.position.ToString("F2") : "null")}, "
            + $"rotation={collector.transform.eulerAngles:F1}, navigationInitialized={navigationInitialized}.", collector);
        collector.SetActive(true);
        if (Application.isPlaying)
            Destroy(staging);
        else
            DestroyImmediate(staging);
        spawnedCollectors.Add(collector);
        return collector;
    }

    private static void AlignNavigationBodyToSpawn(GameObject collector, Vector3 spawnPosition)
    {
        RobotBodyController body = collector != null ? collector.GetComponent<RobotBodyController>() : null;
        Transform navigationBody = body != null ? body.BodyReference : null;
        if (navigationBody == null)
            return;

        // The inherited Worker rig has its animated body several units above its root.
        // Paths measure from BodyReference, so align that physical/navigation reference
        // with the authored marker rather than placing only the invisible prefab root.
        collector.transform.position += spawnPosition - navigationBody.position;
        Physics2D.SyncTransforms();
    }

    private void ResolveReferences()
    {
        if (spawnPoint == null)
            spawnPoint = transform.Find("WorkerCollectorSpawnPoint");
        if (restPoint == null)
            restPoint = transform.Find("WorkerCollectorRestPoint");
        if (restWaypoint == null)
        {
            RoomWaypoint[] waypoints = GetComponentsInChildren<RoomWaypoint>(true);
            for (int i = 0; i < waypoints.Length; i++)
            {
                if (waypoints[i] != null && waypoints[i].type == WaypointType.Rest)
                {
                    restWaypoint = waypoints[i];
                    break;
                }
            }
            if (restWaypoint == null && waypoints.Length > 0)
                restWaypoint = waypoints[0];
        }
        if (spawnPoint == null)
            spawnPoint = restWaypoint != null ? restWaypoint.transform : transform;
        if (restPoint == null)
            restPoint = spawnPoint;
    }
}
