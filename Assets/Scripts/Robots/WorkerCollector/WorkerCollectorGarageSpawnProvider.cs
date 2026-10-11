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

    public WorkerCollectorDropOffProvider DropOff => ResolveDropOff();

    private void Awake()
    {
        ResolveDropOff();
    }

    /// <summary>Spawns this Garage's collector once, with ownership set before activation.</summary>
    public GameObject TrySpawnCollector(bool restAfterBatch = true)
    {
        WorkerCollectorDropOffProvider resolvedDropOff = ResolveDropOff();
        spawnedCollectors.RemoveAll(collector => collector == null);
        if (!Application.isPlaying || workerCollectorPrefab == null || resolvedDropOff == null
            || spawnedCollectors.Count >= Mathf.Max(1, maximumLiveCollectors))
            return null;

        WorkerCollectorSpawnRestProvider rest = FindObjectsByType<WorkerCollectorSpawnRestProvider>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(candidate => candidate != null && candidate.isActiveAndEnabled)
            .OrderBy(candidate => Vector3.SqrMagnitude(candidate.RestPosition - resolvedDropOff.WaitPosition))
            .ThenBy(candidate => StableGridOrder(candidate.transform))
            .FirstOrDefault();
        Vector3 spawnPosition = rest != null ? rest.SpawnPosition : resolvedDropOff.WaitPosition;

        GameObject staging = new GameObject("WorkerCollectorSpawnStaging");
        staging.SetActive(false);
        GameObject collector = Instantiate(workerCollectorPrefab, spawnPosition,
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
        WorkerCollectorMissionService.BindGarage(body, resolvedDropOff, restAfterBatch);
        AlignNavigationBodyToSpawn(collector, spawnPosition);
        if (!StaticRobotNavigationInitializer.TryInitializeSpawnedRobot(collector))
        {
            WorkerCollectorMissionService.UnbindGarage(body);
            Destroy(collector);
            Destroy(staging);
            return null;
        }
        collector.SetActive(true);
        Destroy(staging);
        spawnedCollectors.Add(collector);
        Debug.Log($"WorkerCollectorSpawned garage={name} spawn={(rest != null ? rest.name : "Garage")}", collector);
        return collector;
    }

    private WorkerCollectorDropOffProvider ResolveDropOff()
    {
        if (dropOff == null)
            dropOff = GetComponentInChildren<WorkerCollectorDropOffProvider>(true);
        return dropOff;
    }

    /// <summary>Spawns up to the requested number of collectors for this Garage.</summary>
    public int TrySpawnCollectors(int count, bool restAfterBatch = true)
    {
        maximumLiveCollectors = Mathf.Max(maximumLiveCollectors, count);
        int spawnedCount = 0;
        for (int i = 0; i < Mathf.Max(0, count); i++)
        {
            if (TrySpawnCollector(restAfterBatch) != null)
                spawnedCount++;
        }
        return spawnedCount;
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
