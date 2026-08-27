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
        spawnedCollectors.RemoveAll(collector => collector == null);
        if (!Application.isPlaying || workerCollectorPrefab == null
            || spawnedCollectors.Count >= MaximumLiveCollectors)
            return null;

        Transform point = spawnPoint != null ? spawnPoint : transform;
        GameObject staging = new GameObject("WorkerCollectorSpawnStaging");
        staging.SetActive(false);
        GameObject collector = Instantiate(workerCollectorPrefab, point.position, point.rotation, staging.transform);
        collector.SetActive(false);
        collector.transform.SetParent(null, true);
        collector.name = workerCollectorPrefab.name;
        RobotStateController state = collector.GetComponent<RobotStateController>();
        if (state != null)
        {
            state.Stats = new WorkerRobotFactory().CreateRobot();
            state.Stats.RobotName = "Worker Collector";
        }
        collector.SetActive(true);
        if (Application.isPlaying)
            Destroy(staging);
        else
            DestroyImmediate(staging);
        spawnedCollectors.Add(collector);
        return collector;
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
