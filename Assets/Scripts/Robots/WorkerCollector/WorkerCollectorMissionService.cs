using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Matches idle Worker Collectors with registered white-cube sources and a temporary destination.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorMissionService : MonoBehaviour
{
    private static readonly List<WorkerCollectorWhiteCubeSourceProvider> sources =
        new List<WorkerCollectorWhiteCubeSourceProvider>();
    private static readonly List<WorkerCollectorDropOffProvider> dropOffs =
        new List<WorkerCollectorDropOffProvider>();
    private static WorkerCollectorMissionService instance;

    private int nextMissionId;

    public static void RegisterSource(WorkerCollectorWhiteCubeSourceProvider source)
    {
        if (source != null && !sources.Contains(source))
            sources.Add(source);
    }

    public static void UnregisterSource(WorkerCollectorWhiteCubeSourceProvider source)
    {
        sources.Remove(source);
    }

    public static void RegisterDropOff(WorkerCollectorDropOffProvider dropOff)
    {
        if (dropOff != null && !dropOffs.Contains(dropOff))
            dropOffs.Add(dropOff);
    }

    public static void UnregisterDropOff(WorkerCollectorDropOffProvider dropOff)
    {
        dropOffs.Remove(dropOff);
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
        if (dropOffs.Count == 0)
            return false;

        for (int i = 0; i < sources.Count; i++)
        {
            WorkerCollectorWhiteCubeSourceProvider source = sources[i];
            if (source == null || !source.TryClaimCube(collector, out WhiteCubeCargo target, out WhiteCubeClaim claim))
                continue;

            var assignment = new WorkerCollectorMissionAssignment(
                ++nextMissionId, source, dropOffs[0], target, claim);
            if (collector.Brain.OnWorkerCollectorMissionAssigned(assignment))
                return true;

            target.ReleaseClaim(claim);
        }

        return false;
    }

    private static void RemoveMissingRegistrations()
    {
        sources.RemoveAll(source => source == null);
        dropOffs.RemoveAll(dropOff => dropOff == null);
    }
}
