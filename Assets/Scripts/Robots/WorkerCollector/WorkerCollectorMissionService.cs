using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns and matches Worker Collectors with registered source, garage, and rest capabilities.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorMissionService : MonoBehaviour
{
    private static readonly List<WorkerCollectorWhiteCubeSourceProvider> sources =
        new List<WorkerCollectorWhiteCubeSourceProvider>();
    private static readonly List<WorkerCollectorDropOffProvider> dropOffs =
        new List<WorkerCollectorDropOffProvider>();
    private static readonly List<WorkerCollectorSpawnRestProvider> rests =
        new List<WorkerCollectorSpawnRestProvider>();
    private static WorkerCollectorMissionService instance;

    private int nextMissionId;
    private float nextSpawnCheck;

    private void Update()
    {
        if (Time.time < nextSpawnCheck)
            return;
        nextSpawnCheck = Time.time + 1f;
        TryEnsureCollectors();
    }

    public static void RegisterSource(WorkerCollectorWhiteCubeSourceProvider source)
    {
        if (source != null && !sources.Contains(source))
            sources.Add(source);
        if (Application.isPlaying)
        {
            EnsureInstance();
            instance.TryEnsureCollectors();
        }
    }

    public static void UnregisterSource(WorkerCollectorWhiteCubeSourceProvider source)
    {
        sources.Remove(source);
    }

    public static void RegisterDropOff(WorkerCollectorDropOffProvider dropOff)
    {
        if (dropOff != null && !dropOffs.Contains(dropOff))
            dropOffs.Add(dropOff);
        if (Application.isPlaying)
        {
            EnsureInstance();
            instance.TryEnsureCollectors();
        }
    }

    public static void UnregisterDropOff(WorkerCollectorDropOffProvider dropOff)
    {
        dropOffs.Remove(dropOff);
    }

    public static void RegisterRest(WorkerCollectorSpawnRestProvider rest)
    {
        if (rest != null && !rests.Contains(rest))
            rests.Add(rest);
        if (Application.isPlaying)
        {
            EnsureInstance();
            instance.TryEnsureCollectors();
        }
    }

    public static void UnregisterRest(WorkerCollectorSpawnRestProvider rest)
    {
        rests.Remove(rest);
    }

    /// <summary>
    /// Finds and assigns one exclusively claimed white cube through Brain ingress.
    /// </summary>
    public static bool RequestAssignment(WorkerCollectorBodyController collector)
    {
        if (collector == null)
            return false;
        EnsureInstance();
        return instance.TryAssign(collector);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
            return;
        instance = FindFirstObjectByType<WorkerCollectorMissionService>();
        if (instance == null)
            instance = new GameObject(nameof(WorkerCollectorMissionService)).AddComponent<WorkerCollectorMissionService>();
    }

    private bool TryAssign(WorkerCollectorBodyController collector)
    {
        if (collector.Brain == null || collector.Brain.Memory == null
            || collector.Brain.Memory.Snapshot.WorkerCollector.Assignment != null)
        {
            return false;
        }

        RemoveMissingRegistrations();
        WorkerCollectorDropOffProvider dropOff = FindAvailableGarage(collector, out GarageSlotReservation reservation);
        if (dropOff == null)
            return false;

        for (int i = 0; i < sources.Count; i++)
        {
            WorkerCollectorWhiteCubeSourceProvider source = sources[i];
            if (source == null || !source.TryClaimCube(collector, out WhiteCubeCargo target, out WhiteCubeClaim claim))
                continue;

            WorkerCollectorSpawnRestProvider rest = rests.Count > 0 ? rests[0] : null;
            var assignment = new WorkerCollectorMissionAssignment(
                ++nextMissionId, source, dropOff, rest, target, claim, reservation);
            if (collector.Brain.OnWorkerCollectorMissionAssigned(assignment))
                return true;

            target.ReleaseClaim(claim);
        }


        dropOff.ReleaseReservation(reservation);

        return false;
    }

    private static WorkerCollectorDropOffProvider FindAvailableGarage(
        WorkerCollectorBodyController collector, out GarageSlotReservation reservation)
    {
        reservation = default;
        for (int i = 0; i < dropOffs.Count; i++)
        {
            WorkerCollectorDropOffProvider dropOff = dropOffs[i];
            if (dropOff != null && dropOff.TryReserve(collector, out reservation))
                return dropOff;
        }
        return null;
    }

    private void TryEnsureCollectors()
    {
        if (!Application.isPlaying)
            return;
        RemoveMissingRegistrations();
        if (sources.Count == 0 || dropOffs.Find(dropOff => dropOff != null && dropOff.IsGarageDestination) == null)
            return;

        WorkerCollectorBodyController[] live = FindObjectsByType<WorkerCollectorBodyController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (live.Length > 0)
            return;
        for (int i = 0; i < rests.Count; i++)
        {
            if (rests[i] != null && rests[i].TrySpawnCollector() != null)
                return;
        }
    }

    private static void RemoveMissingRegistrations()
    {
        sources.RemoveAll(source => source == null);
        dropOffs.RemoveAll(dropOff => dropOff == null);
        rests.RemoveAll(rest => rest == null);
    }
}
