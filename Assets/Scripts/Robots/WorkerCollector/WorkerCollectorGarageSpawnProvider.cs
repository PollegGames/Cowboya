using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Owns the dedicated Worker Collector for one generated Garage.</summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorGarageSpawnProvider : MonoBehaviour
{
    [SerializeField] private GameObject workerCollectorPrefab;
    [SerializeField] private WorkerCollectorDropOffProvider dropOff;
    [SerializeField, Min(1)] private int maximumLiveCollectors = 1;

    private readonly List<GameObject> spawnedCollectors = new List<GameObject>();

    public WorkerCollectorDropOffProvider DropOff => dropOff;

    private void Awake()
    {
        if (dropOff == null)
            dropOff = GetComponentInChildren<WorkerCollectorDropOffProvider>(true);
    }

    /// <summary>Spawns this Garage's collector once, with ownership set before activation.</summary>
    public GameObject TrySpawnCollector()
    {
        spawnedCollectors.RemoveAll(collector => collector == null);
        if (!Application.isPlaying || workerCollectorPrefab == null || dropOff == null
            || spawnedCollectors.Count >= Mathf.Max(1, maximumLiveCollectors))
            return null;

        WorkerCollectorSpawnRestProvider rest = FindObjectsByType<WorkerCollectorSpawnRestProvider>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(candidate => candidate != null && candidate.isActiveAndEnabled)
            .OrderBy(candidate => Vector3.SqrMagnitude(candidate.RestPosition - dropOff.WaitPosition))
            .ThenBy(candidate => StableGridOrder(candidate.transform))
            .FirstOrDefault();
        if (rest == null)
        {
            Debug.LogWarning($"WorkerCollectorStandby garage={name} reason=NoRest", this);
            return null;
        }

        GameObject staging = new GameObject("WorkerCollectorSpawnStaging");
        staging.SetActive(false);
        GameObject collector = Instantiate(workerCollectorPrefab, rest.SpawnPosition,
            workerCollectorPrefab.transform.rotation, staging.transform);
        collector.SetActive(false);
        collector.transform.SetParent(null, true);
        collector.name = workerCollectorPrefab.name;

        RobotStateController state = collector.GetComponent<RobotStateController>();
        if (state != null)
        {
            state.Stats = new WorkerRobotFactory().CreateRobot();
            state.Stats.RobotName = "Worker Collector";
        }

        WorkerCollectorBodyController body = collector.GetComponent<WorkerCollectorBodyController>();
        WorkerCollectorMissionService.BindGarage(body, dropOff);
        AlignNavigationBodyToSpawn(collector, rest.SpawnPosition);
        StaticRobotNavigationInitializer.TryInitializeSpawnedRobot(collector);
        collector.SetActive(true);
        Destroy(staging);
        spawnedCollectors.Add(collector);
        Debug.Log($"WorkerCollectorSpawned garage={name} spawn={rest.name}", collector);
        return collector;
    }

    private static long StableGridOrder(Transform candidate)
    {
        RoomProperties properties = candidate.GetComponentInParent<RoomProperties>();
        return properties == null
            ? long.MaxValue
            : ((long)properties.GridPosition.x << 32) + (uint)properties.GridPosition.y;
    }

    private static void AlignNavigationBodyToSpawn(GameObject collector, Vector3 spawnPosition)
    {
        RobotBodyController body = collector.GetComponent<RobotBodyController>();
        if (body == null || body.BodyReference == null)
            return;
        collector.transform.position += spawnPosition - body.BodyReference.position;
        Physics2D.SyncTransforms();
    }
}
