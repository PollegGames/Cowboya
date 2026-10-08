using System.Collections.Generic;
using System.Linq;
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
    private static readonly Dictionary<int, WorkerCollectorDropOffProvider> collectorGarages =
        new Dictionary<int, WorkerCollectorDropOffProvider>();
    private static WorkerCollectorMissionService instance;

    private int nextMissionId;
    private WorkerCollectorAssignmentFailure lastReportedFailure;

    public static WorkerCollectorAssignmentFailure LastAssignmentFailure { get; private set; }

    public static void RegisterSource(WorkerCollectorWhiteCubeSourceProvider source)
    {
        if (source != null && !sources.Contains(source))
            sources.Add(source);
        Debug.Log($"[WorkerCollectorDiagnostics] Source registered={source != null}, total={sources.Count}.", source);
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

    /// <summary>Binds a collector to the Garage that owns its missions.</summary>
    public static void BindGarage(WorkerCollectorBodyController collector,
        WorkerCollectorDropOffProvider garage)
    {
        if (collector != null && garage != null)
            collectorGarages[collector.GetInstanceID()] = garage;
    }

    /// <summary>Clears the ownership record for a collector leaving the scene.</summary>
    public static void UnbindGarage(WorkerCollectorBodyController collector)
    {
        if (collector != null)
            collectorGarages.Remove(collector.GetInstanceID());
    }

    /// <summary>Spawns the legacy static-scene collector with an explicit Garage assignment.</summary>
    public static GameObject SpawnStaticSceneCollector()
    {
        EnsureInstance();
        RemoveMissingRegistrations();
        WorkerCollectorDropOffProvider garage = instance.preferredDropOff != null
                && instance.preferredDropOff.isActiveAndEnabled
                && instance.preferredDropOff.IsGarageDestination
            ? instance.preferredDropOff
            : dropOffs
            .Where(candidate => candidate != null && candidate.isActiveAndEnabled
                && candidate.IsGarageDestination)
            .OrderBy(candidate => GetStableGridOrder(candidate.transform))
            .FirstOrDefault();
        WorkerCollectorSpawnRestProvider rest = rests
            .Where(candidate => candidate != null && candidate.isActiveAndEnabled)
            .OrderBy(candidate => GetStableGridOrder(candidate.transform))
            .FirstOrDefault();
        return garage != null && rest != null ? rest.TrySpawnCollector(garage) : null;
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
        collectorGarages.TryGetValue(collector.GetInstanceID(), out WorkerCollectorDropOffProvider dropOff);
        if (dropOff == null || !dropOff.isActiveAndEnabled || !dropOff.IsGarageDestination)
            dropOff = null;
        if (dropOff == null || dropOff.ApproachWaypoint == null)
        {
            SetAssignmentFailure(WorkerCollectorAssignmentFailure.NoGarage, collector);
            return false;
        }

        WorkerCollectorWhiteCubeSourceProvider[] orderedSources = sources
            .Where(source => source != null && source.isActiveAndEnabled && source.ApproachWaypoint != null)
            .OrderBy(source => Vector3.SqrMagnitude(
                source.ApproachWaypoint.WorldPos - dropOff.ApproachWaypoint.WorldPos))
            .ThenBy(source => GetStableGridOrder(source.transform))
            .ToArray();
        foreach (WorkerCollectorWhiteCubeSourceProvider source in orderedSources)
        {
            WorkerCollectorSpawnRestProvider rest = rests
                .Where(candidate => candidate != null && candidate.isActiveAndEnabled)
                .OrderBy(candidate => Vector3.SqrMagnitude(candidate.RestPosition - dropOff.WaitPosition))
                .ThenBy(candidate => GetStableGridOrder(candidate.transform))
                .FirstOrDefault();
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

    private static long GetStableGridOrder(Transform candidate)
    {
        RoomProperties properties = candidate != null
            ? candidate.GetComponentInParent<RoomProperties>()
            : null;
        if (properties == null)
            return long.MaxValue;
        return ((long)properties.GridPosition.x << 32) + (uint)properties.GridPosition.y;
    }

    private static void RemoveMissingRegistrations()
    {
        sources.RemoveAll(source => source == null);
        dropOffs.RemoveAll(dropOff => dropOff == null);
        rests.RemoveAll(rest => rest == null);
        int[] stale = collectorGarages.Where(pair => pair.Value == null)
            .Select(pair => pair.Key).ToArray();
        for (int i = 0; i < stale.Length; i++)
            collectorGarages.Remove(stale[i]);
    }
}
