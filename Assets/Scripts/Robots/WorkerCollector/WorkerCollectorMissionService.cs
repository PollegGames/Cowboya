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
    BrainRejectedAssignment,
    NoSource,
    NoGarage
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
    /// Records a stable service route in Memory before the Brain chooses source travel.
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

    /// <summary>
    /// Claims local cargo and a garage slot only after the worker reaches its source.
    /// </summary>
    public static bool RequestTarget(WorkerCollectorBodyController collector,
        WorkerCollectorMissionAssignment assignment) {
        if (collector == null || collector.Brain == null || collector.Brain.Memory == null
            || assignment == null || !assignment.HasRequiredReferences || assignment.HasClaimedTarget)
            return false;

        RobotMemoryNew memory = collector.Brain.Memory;
        WorkerCollectorMissionFacts facts = memory.Snapshot.WorkerCollector;
        if (!ReferenceEquals(facts.Assignment, assignment) || !facts.SourceApproachReached
            || !assignment.Source.isActiveAndEnabled || !assignment.DropOff.CanAcceptDelivery)
            return false;

        if (!assignment.Source.TryClaimCube(collector, out WhiteCubeCargo target, out WhiteCubeClaim claim))
            return false;

        if (!assignment.DropOff.TryReserve(collector, out GarageSlotReservation reservation)) {
            target.ReleaseClaim(claim);
            return false;
        }

        var targetedAssignment = new WorkerCollectorMissionAssignment(assignment.MissionId,
            assignment.Source, assignment.DropOff, assignment.Rest, target, claim, reservation);
        if (memory.TryAcquireWorkerCollectorTarget(assignment, targetedAssignment))
            return true;

        target.ReleaseClaim(claim);
        assignment.DropOff.ReleaseReservation(reservation);
        return false;
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
        WorkerCollectorDropOffProvider dropOff = FindGarage();
        if (dropOff == null)
        {
            SetAssignmentFailure(WorkerCollectorAssignmentFailure.NoGarage, collector);
            return false;
        }

        for (int i = 0; i < sources.Count; i++)
        {
            WorkerCollectorWhiteCubeSourceProvider source = sources[i];
            if (source == null || !source.isActiveAndEnabled || source.ApproachWaypoint == null)
                continue;

            WorkerCollectorSpawnRestProvider rest = rests.Count > 0 ? rests[0] : null;
            var assignment = new WorkerCollectorMissionAssignment(
                ++nextMissionId, source, dropOff, rest);
            if (collector.Brain.Memory.TryAssignWorkerCollectorMission(assignment))
            {
                SetAssignmentFailure(WorkerCollectorAssignmentFailure.None, collector);
                return true;
            }
            SetAssignmentFailure(WorkerCollectorAssignmentFailure.BrainRejectedAssignment, collector);
            return false;
        }

        SetAssignmentFailure(WorkerCollectorAssignmentFailure.NoSource, collector);
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

    private WorkerCollectorDropOffProvider FindGarage()
    {
        if (preferredDropOff != null && preferredDropOff.isActiveAndEnabled && preferredDropOff.IsGarageDestination)
            return preferredDropOff;

        for (int i = 0; i < dropOffs.Count; i++)
        {
            WorkerCollectorDropOffProvider dropOff = dropOffs[i];
            if (dropOff != null && dropOff.isActiveAndEnabled && dropOff.IsGarageDestination)
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
            return;

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
