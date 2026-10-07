using System.Collections.Generic;
using UnityEngine;

public enum WorkerCollectorAssignmentFailure
{
    None,
    CollectorUnavailable,
    PipelineUnavailable,
    NavigationUnavailable,
    NoGarageSlot,
    NoWhiteCube,
    BrainRejectedAssignment
}

/// <summary>
/// Spawns and matches Worker Collectors with registered source, garage, and rest capabilities.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorkerCollectorMissionService : MonoBehaviour
{
    [SerializeField] private WorkerCollectorDropOffProvider preferredDropOff;

    private static readonly List<WorkerCollectorWhiteCubeSourceProvider> sources =
        new List<WorkerCollectorWhiteCubeSourceProvider>();
    private static readonly List<WorkerCollectorDropOffProvider> dropOffs =
        new List<WorkerCollectorDropOffProvider>();
    private static readonly List<WorkerCollectorSpawnRestProvider> rests =
        new List<WorkerCollectorSpawnRestProvider>();
    private static WorkerCollectorMissionService instance;

    private int nextMissionId;
    private float nextSpawnCheck;
    private WorkerCollectorAssignmentFailure lastReportedFailure;

    public static WorkerCollectorAssignmentFailure LastAssignmentFailure { get; private set; }

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
        Debug.Log($"[WorkerCollectorDiagnostics] Source registered={source != null}, total={sources.Count}.", source);
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
        Debug.Log($"[WorkerCollectorDiagnostics] Garage registered={dropOff != null}, "
            + $"valid={(dropOff != null && dropOff.IsGarageDestination)}, total={dropOffs.Count}.", dropOff);
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
        Debug.Log($"[WorkerCollectorDiagnostics] Rest registered={rest != null}, total={rests.Count}.", rest);
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
        {
            LastAssignmentFailure = WorkerCollectorAssignmentFailure.CollectorUnavailable;
            return false;
        }
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
            SetAssignmentFailure(WorkerCollectorAssignmentFailure.PipelineUnavailable, collector);
            return false;
        }

        RobotBodyController body = collector.GetComponent<RobotBodyController>();
        if (body == null || !body.IsNavigationInitialized)
        {
            StaticRobotNavigationInitializer.TryInitializeSpawnedRobot(collector.gameObject);
            if (body == null || !body.IsNavigationInitialized)
            {
                SetAssignmentFailure(WorkerCollectorAssignmentFailure.NavigationUnavailable, collector);
                return false;
            }
        }

        RemoveMissingRegistrations();
        WorkerCollectorDropOffProvider dropOff = FindAvailableGarage(collector, out GarageSlotReservation reservation);
        if (dropOff == null)
        {
            SetAssignmentFailure(WorkerCollectorAssignmentFailure.NoGarageSlot, collector);
            return false;
        }

        bool foundWhiteCube = false;
        for (int i = 0; i < sources.Count; i++)
        {
            WorkerCollectorWhiteCubeSourceProvider source = sources[i];
            if (source == null || !source.TryClaimCube(collector, out WhiteCubeCargo target, out WhiteCubeClaim claim))
                continue;

            foundWhiteCube = true;

            WorkerCollectorSpawnRestProvider rest = rests.Count > 0 ? rests[0] : null;
            var assignment = new WorkerCollectorMissionAssignment(
                ++nextMissionId, source, dropOff, rest, target, claim, reservation);
            if (collector.Brain.OnWorkerCollectorMissionAssigned(assignment))
            {
                SetAssignmentFailure(WorkerCollectorAssignmentFailure.None, collector);
                return true;
            }

            target.ReleaseClaim(claim);
        }


        dropOff.ReleaseReservation(reservation);
        SetAssignmentFailure(foundWhiteCube
            ? WorkerCollectorAssignmentFailure.BrainRejectedAssignment
            : WorkerCollectorAssignmentFailure.NoWhiteCube, collector);

        return false;
    }

    private void SetAssignmentFailure(
        WorkerCollectorAssignmentFailure failure,
        WorkerCollectorBodyController collector)
    {
        LastAssignmentFailure = failure;
        if (failure != WorkerCollectorAssignmentFailure.None && failure != lastReportedFailure)
        {
            Debug.LogWarning(
                $"Worker Collector assignment unavailable: {failure}. "
                + $"Registered sources={sources.Count}, garages={dropOffs.Count}, rests={rests.Count}.",
                collector);
        }
        lastReportedFailure = failure;
    }

    private WorkerCollectorDropOffProvider FindAvailableGarage(
        WorkerCollectorBodyController collector, out GarageSlotReservation reservation)
    {
        reservation = default;

        if (preferredDropOff != null && preferredDropOff.TryReserve(collector, out reservation))
            return preferredDropOff;

        for (int i = 0; i < dropOffs.Count; i++)
        {
            WorkerCollectorDropOffProvider dropOff = dropOffs[i];
            if (dropOff != null && dropOff != preferredDropOff
                && dropOff.TryReserve(collector, out reservation))
                return dropOff;
        }
        return null;
    }

    private void TryEnsureCollectors()
    {
        if (!Application.isPlaying)
            return;
        RemoveMissingRegistrations();

        WorkerCollectorBodyController[] live = FindObjectsByType<WorkerCollectorBodyController>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (live.Length > 0)
        {
            // Provider registration can happen after the collector's first FindCube
            // attempt. Wake every idle collector when the service checks readiness so
            // a missed startup retry cannot leave it in standby permanently.
            for (int i = 0; i < live.Length; i++)
            {
                if (live[i] != null && live[i].CurrentAssignment == null)
                    live[i].FindCube();
            }
            return;
        }

        // Creation belongs to the rest/spawn capability. Source and garage providers
        // may register later in the frame (the garage waits for its coordinator), so
        // gating creation on them can leave a valid collector permanently unspawned.
        // The collector's FindCube command already retries until a mission is ready.
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
