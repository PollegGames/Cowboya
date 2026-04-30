using UnityEngine;
using System.Collections.Generic;

public enum RobotNewPipelineMode
{
    Disabled = 0,
    NewShadow = 1,
    NewOnly = 2
}

/// <summary>
/// Runtime switches and structured trace helpers for the New robot pipeline.
/// </summary>
public static class RobotNewPipelineRuntime
{
    /// <summary>
    /// Default mode for this phase: New computes decisions in parallel without prefab takeover.
    /// </summary>
    public static RobotNewPipelineMode Mode = RobotNewPipelineMode.NewShadow;

    public static bool EnableTrace = true;
    public static bool EnableEcosystemProbe = true;
    public static bool EnableProbeSummaryOnSceneInit = true;
    public static bool DriveGameplayInShadow = true;
    public static bool WorkerCycleValidationMode = false;

    public static bool IsNewPipelineActive =>
        Mode == RobotNewPipelineMode.NewShadow || Mode == RobotNewPipelineMode.NewOnly;

    public static bool ShouldDriveGameplay =>
        Mode == RobotNewPipelineMode.NewOnly
        || (Mode == RobotNewPipelineMode.NewShadow && DriveGameplayInShadow);

    public static bool IsWorkerCycleValidationEnabled
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return WorkerCycleValidationMode;
#else
            return false;
#endif
        }
    }
}

public static class RobotNewTrace
{
    public static void Log(
        MonoBehaviour owner,
        string eventSource,
        string memoryDelta,
        BrainOption brainOptions,
        RobotTask plannedTask,
        RobotTask heartCurrentTask,
        string taskSignal)
    {
        if (!RobotNewPipelineRuntime.EnableTrace)
            return;

        string robotId = "unknown";
        if (owner != null)
            robotId = owner.name + "#" + owner.GetInstanceID();

        string planned = plannedTask != null ? plannedTask.Type.ToString() : "null";
        string heart = heartCurrentTask != null ? heartCurrentTask.Type.ToString() : "null";

        Debug.Log(
            "[RobotNewTrace] "
            + "timestamp=" + Time.time.ToString("F3")
            + " robotId=" + robotId
            + " eventSource=" + eventSource
            + " memoryDelta=" + memoryDelta
            + " brainOptions=" + brainOptions
            + " plannedTask=" + planned
            + " heartCurrentTask=" + heart
            + " taskSignal=" + taskSignal,
            owner
        );
    }
}

public sealed class RobotEcosystemProbeSnapshot
{
    public Dictionary<string, int> EventCounts { get; } = new Dictionary<string, int>();
    public Dictionary<string, string> RobotRoles { get; } = new Dictionary<string, string>();
    public Dictionary<string, string> RobotSpawnWaypoints { get; } = new Dictionary<string, string>();
    public Dictionary<string, string> RobotFirstTask { get; } = new Dictionary<string, string>();
    public Dictionary<string, string> RobotFirstSlotOutcome { get; } = new Dictionary<string, string>();
    public Dictionary<string, string> RobotCurrentTargetWaypoint { get; } = new Dictionary<string, string>();
    public Dictionary<string, string> RobotCurrentMachineOwnership { get; } = new Dictionary<string, string>();
    public Dictionary<string, List<string>> WorkerRecentTransitions { get; } = new Dictionary<string, List<string>>();
}

/// <summary>
/// Structured observability probe for New pipeline call coverage.
/// This probe does not drive gameplay decisions.
/// </summary>
public static class RobotEcosystemProbe
{
    private static readonly object gate = new object();
    private static readonly Dictionary<string, int> eventCounts = new Dictionary<string, int>();
    private static readonly Dictionary<string, string> robotRoles = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> robotSpawnWaypoints = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> robotFirstTask = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> robotFirstSlotOutcome = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> robotCurrentTargetWaypoint = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> robotCurrentMachineOwnership = new Dictionary<string, string>();
    private static readonly Dictionary<string, Queue<string>> workerRecentTransitions = new Dictionary<string, Queue<string>>();
    private static int sequence;

    private static readonly string[] summaryEvents =
    {
        "Brain.OnPerceptionChanged",
        "Brain.OnDamageTaken",
        "Brain.OnMachineStateEvent",
        "Brain.OnSecurityDispatch",
        "Heart.OnPlannedTask",
        "Heart.OnCurrentTaskChanged",
        "Spawner.InitializeRobot"
    };

    public static void Reset()
    {
        lock (gate)
        {
            eventCounts.Clear();
            robotRoles.Clear();
            robotSpawnWaypoints.Clear();
            robotFirstTask.Clear();
            robotFirstSlotOutcome.Clear();
            robotCurrentTargetWaypoint.Clear();
            robotCurrentMachineOwnership.Clear();
            workerRecentTransitions.Clear();
            sequence = 0;
        }
    }

    public static void RecordSpawn(MonoBehaviour owner, RobotRole role, RoomWaypoint initialWaypoint)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(owner);
        string waypointText = initialWaypoint != null
            ? initialWaypoint.type + "@" + initialWaypoint.WorldPos.ToString("F2")
            : "null";

        lock (gate)
        {
            robotRoles[robotId] = role.ToString();
            robotSpawnWaypoints[robotId] = waypointText;
            Increment("Spawner.InitializeRobot");
        }

        Log(robotId, "Spawner.InitializeRobot", "role=" + role + " waypoint=" + waypointText);
    }

    public static void RecordSpawnReservationDecision(MonoBehaviour owner, RobotRole role, RoomWaypoint waypoint, string outcome)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(owner);
        string waypointText = waypoint != null
            ? waypoint.type + "@" + waypoint.WorldPos.ToString("F2")
            : "null";
        string eventName = "Spawner.Reservation." + role;

        lock (gate)
        {
            Increment(eventName);
        }

        Log(robotId, eventName, "outcome=" + outcome + " waypoint=" + waypointText);
    }

    public static void RecordWaypointDecision(
        MonoBehaviour owner,
        string eventName,
        RoomWaypoint previous,
        RoomWaypoint current,
        string detail)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(owner);
        string previousText = previous != null
            ? previous.type + "@" + previous.WorldPos.ToString("F2")
            : "null";
        string currentText = current != null
            ? current.type + "@" + current.WorldPos.ToString("F2")
            : "null";

        lock (gate)
        {
            Increment(eventName);
        }

        Log(robotId, eventName, "previous=" + previousText + " current=" + currentText + " " + detail);
    }

    public static void RecordBodyNavigationReference(MonoBehaviour owner, Transform bodyReference, string reason)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(owner);
        string referenceText = bodyReference != null ? bodyReference.name : "null";

        lock (gate)
        {
            Increment("Body.NavigationReference");
        }

        Log(robotId, "Body.NavigationReference", "reference=" + referenceText + " reason=" + reason);
    }

    public static void RecordBrainCall(MonoBehaviour owner, string methodName, string payload)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string eventName = "Brain." + methodName;
        string robotId = ResolveRobotId(owner);

        lock (gate)
        {
            Increment(eventName);
        }

        Log(robotId, eventName, payload);
    }

    public static void RecordHeartPlannedTask(MonoBehaviour owner, RobotTask plannedTask, RobotTask currentTask)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(owner);
        string planned = plannedTask != null ? plannedTask.Type.ToString() : "null";
        string current = currentTask != null ? currentTask.Type.ToString() : "null";
        string plannedTarget = DescribeTaskTarget(plannedTask);
        string currentTarget = DescribeTaskTarget(currentTask);

        lock (gate)
        {
            Increment("Heart.OnPlannedTask");
            if (!robotFirstTask.ContainsKey(robotId))
                robotFirstTask[robotId] = current != "null" ? current : planned;
        }

        Log(robotId, "Heart.OnPlannedTask", "planned=" + planned + " plannedTarget=" + plannedTarget + " current=" + current + " currentTarget=" + currentTarget);
    }

    public static void RecordHeartDefaultTask(MonoBehaviour owner, RobotTask defaultTask)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(owner);
        string taskName = defaultTask != null ? defaultTask.Type.ToString() : "null";
        string taskTarget = DescribeTaskTarget(defaultTask);

        lock (gate)
        {
            Increment("Heart.BuildDefaultTask");
        }

        Log(robotId, "Heart.BuildDefaultTask", "task=" + taskName + " target=" + taskTarget);
    }

    public static void RecordHeartCurrentTaskChanged(MonoBehaviour owner, RobotTask currentTask)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(owner);
        string current = currentTask != null ? currentTask.Type.ToString() : "null";
        string currentTarget = DescribeTaskTarget(currentTask);

        lock (gate)
        {
            Increment("Heart.OnCurrentTaskChanged");
            if (!robotFirstTask.ContainsKey(robotId))
                robotFirstTask[robotId] = current;
            robotCurrentTargetWaypoint[robotId] = currentTarget;
        }

        Log(robotId, "Heart.OnCurrentTaskChanged", "current=" + current + " target=" + currentTarget);
    }

    public static void RecordSlotDecision(
        MonoBehaviour slotOwner,
        string slotName,
        string outcome,
        RobotBrainNew brain,
        BaseMachine machine,
        RobotTask task)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(brain);
        string eventName = "Slot." + slotName + "." + outcome;
        string taskName = task != null ? task.Type.ToString() : "None";
        string machineName = machine != null ? machine.name : "null";

        lock (gate)
        {
            Increment(eventName);
            if (outcome == "attach_ignored_duplicate")
                Increment("slot_attach_ignored_duplicate");
            else if (outcome == "release_ignored_non_owner")
                Increment("slot_release_ignored_non_owner");

            if (!robotFirstSlotOutcome.ContainsKey(robotId))
                robotFirstSlotOutcome[robotId] = slotName + ":" + outcome;

            if (outcome.StartsWith("attached"))
                robotCurrentMachineOwnership[robotId] = machineName;
            else if (outcome.StartsWith("released"))
                robotCurrentMachineOwnership[robotId] = "none";
        }

        _ = slotOwner;
        Log(robotId, eventName, "machine=" + machineName + " task=" + taskName);
    }

    public static void RecordSlotDecisionDetail(
        MonoBehaviour slotOwner,
        string slotName,
        string outcome,
        RobotBrainNew brain,
        BaseMachine machine,
        RobotTask task,
        string detail)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(brain);
        string eventName = "Slot." + slotName + "." + outcome;
        string taskName = task != null ? task.Type.ToString() : "None";
        string taskTarget = DescribeTaskTarget(task);
        string machineName = machine != null ? machine.name : "null";

        lock (gate)
        {
            Increment(eventName);
        }

        _ = slotOwner;
        Log(
            robotId,
            eventName,
            "machine=" + machineName
            + " task=" + taskName
            + " taskTarget=" + taskTarget
            + " " + detail);
    }

    public static void RecordWorkerCycleTransition(MonoBehaviour owner, RobotTask fromTask, RobotTask toTask, string reason)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        string robotId = ResolveRobotId(owner);
        string from = fromTask != null ? fromTask.Type.ToString() : "none";
        string to = toTask != null ? toTask.Type.ToString() : "none";
        string transition = from + "->" + to + "(" + reason + ")";

        lock (gate)
        {
            Increment("WorkerCycle." + reason);

            if (!workerRecentTransitions.TryGetValue(robotId, out var queue))
            {
                queue = new Queue<string>();
                workerRecentTransitions[robotId] = queue;
            }

            queue.Enqueue(transition);
            while (queue.Count > 5)
                queue.Dequeue();
        }

        Debug.Log(
            "[RobotEcosystemProbe] WorkerCycle"
            + " robotId=" + robotId
            + " from=" + from
            + " to=" + to
            + " reason=" + reason);
    }

    public static int GetCallCount(string eventName)
    {
        lock (gate)
        {
            return eventCounts.TryGetValue(eventName, out int count) ? count : 0;
        }
    }

    public static bool WasCalled(string eventName) => GetCallCount(eventName) > 0;

    public static RobotEcosystemProbeSnapshot GetSnapshot()
    {
        var snapshot = new RobotEcosystemProbeSnapshot();
        lock (gate)
        {
            Copy(eventCounts, snapshot.EventCounts);
            Copy(robotRoles, snapshot.RobotRoles);
            Copy(robotSpawnWaypoints, snapshot.RobotSpawnWaypoints);
            Copy(robotFirstTask, snapshot.RobotFirstTask);
            Copy(robotFirstSlotOutcome, snapshot.RobotFirstSlotOutcome);
            Copy(robotCurrentTargetWaypoint, snapshot.RobotCurrentTargetWaypoint);
            Copy(robotCurrentMachineOwnership, snapshot.RobotCurrentMachineOwnership);
            foreach (var pair in workerRecentTransitions)
                snapshot.WorkerRecentTransitions[pair.Key] = new List<string>(pair.Value);
        }
        return snapshot;
    }

    public static void DumpSummary(string context)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        Dictionary<string, int> counts;
        Dictionary<string, string> roles;
        Dictionary<string, string> firstTasks;
        Dictionary<string, string> firstSlots;
        lock (gate)
        {
            counts = new Dictionary<string, int>(eventCounts);
            roles = new Dictionary<string, string>(robotRoles);
            firstTasks = new Dictionary<string, string>(robotFirstTask);
            firstSlots = new Dictionary<string, string>(robotFirstSlotOutcome);
        }

        int workers = 0;
        int guards = 0;
        int bosses = 0;
        foreach (var pair in roles)
        {
            switch (pair.Value)
            {
                case "Worker":
                    workers++;
                    break;
                case "SecurityGuard":
                    guards++;
                    break;
                case "Boss":
                    bosses++;
                    break;
            }
        }

        Debug.Log(
            "[RobotEcosystemProbe] summary context=" + context
            + " workers=" + workers
            + " guards=" + guards
            + " bosses=" + bosses
            + " robots=" + roles.Count);

        foreach (var evt in summaryEvents)
        {
            int count = counts.TryGetValue(evt, out int value) ? value : 0;
            Debug.Log("[RobotEcosystemProbe] summary event=" + evt + " count=" + count + " called=" + (count > 0));
        }

        foreach (var pair in roles)
        {
            string robotId = pair.Key;
            string firstTask = firstTasks.TryGetValue(robotId, out var task) ? task : "none";
            string firstSlot = firstSlots.TryGetValue(robotId, out var slot) ? slot : "none";
            Debug.Log(
                "[RobotEcosystemProbe] summary robotId=" + robotId
                + " role=" + pair.Value
                + " firstTask=" + firstTask
                + " firstSlotOutcome=" + firstSlot);
        }
    }

    public static void DumpWorkerSummary(string context)
    {
        if (!RobotNewPipelineRuntime.EnableEcosystemProbe)
            return;

        Dictionary<string, string> roles;
        Dictionary<string, string> targets;
        Dictionary<string, string> ownership;
        Dictionary<string, Queue<string>> transitions;
        int duplicateAttachIgnored;
        int releaseNonOwnerIgnored;

        lock (gate)
        {
            roles = new Dictionary<string, string>(robotRoles);
            targets = new Dictionary<string, string>(robotCurrentTargetWaypoint);
            ownership = new Dictionary<string, string>(robotCurrentMachineOwnership);
            transitions = new Dictionary<string, Queue<string>>();
            foreach (var pair in workerRecentTransitions)
                transitions[pair.Key] = new Queue<string>(pair.Value);

            duplicateAttachIgnored = eventCounts.TryGetValue("slot_attach_ignored_duplicate", out int dup) ? dup : 0;
            releaseNonOwnerIgnored = eventCounts.TryGetValue("slot_release_ignored_non_owner", out int nonOwner) ? nonOwner : 0;
        }

        int workerCount = 0;
        foreach (var pair in roles)
        {
            if (pair.Value == RobotRole.Worker.ToString())
                workerCount++;
        }

        Debug.Log(
            "[RobotEcosystemProbe] worker-summary"
            + " context=" + context
            + " workers=" + workerCount
            + " slot_attach_ignored_duplicate=" + duplicateAttachIgnored
            + " slot_release_ignored_non_owner=" + releaseNonOwnerIgnored);

        foreach (var pair in roles)
        {
            if (pair.Value != RobotRole.Worker.ToString())
                continue;

            string robotId = pair.Key;
            string target = targets.TryGetValue(robotId, out var targetValue) ? targetValue : "none";
            string owner = ownership.TryGetValue(robotId, out var ownerValue) ? ownerValue : "none";
            string transitionTail = transitions.TryGetValue(robotId, out var queue) && queue.Count > 0
                ? string.Join("|", queue.ToArray())
                : "none";

            Debug.Log(
                "[RobotEcosystemProbe] worker-summary"
                + " robotId=" + robotId
                + " target=" + target
                + " owner=" + owner
                + " transitions=" + transitionTail);
        }
    }

    private static void Copy(Dictionary<string, int> source, Dictionary<string, int> destination)
    {
        foreach (var pair in source)
            destination[pair.Key] = pair.Value;
    }

    private static void Copy(Dictionary<string, string> source, Dictionary<string, string> destination)
    {
        foreach (var pair in source)
            destination[pair.Key] = pair.Value;
    }

    private static void Increment(string eventName)
    {
        if (eventCounts.TryGetValue(eventName, out int count))
            eventCounts[eventName] = count + 1;
        else
            eventCounts[eventName] = 1;
    }

    private static string DescribeTaskTarget(RobotTask task)
    {
        if (task == null || task.Payload == null)
            return "none";

        if (task.Payload is RoomWaypoint waypoint && waypoint != null)
            return waypoint.type + "@" + waypoint.WorldPos.ToString("F2");

        if (task.Payload is RobotPlayerChaseTarget chaseTarget)
        {
            string waypointText = chaseTarget.Waypoint != null
                ? chaseTarget.Waypoint.type + "@" + chaseTarget.Waypoint.WorldPos.ToString("F2")
                : "null";
            string finalText = chaseTarget.HasPlayerPosition
                ? chaseTarget.PlayerPosition.ToString("F2")
                : "none";
            return "playerWaypoint:" + waypointText + " final:" + finalText;
        }

        if (task.Payload is BaseMachine machine && machine != null)
        {
            var waypointOnMachine = machine.GetComponent<RoomWaypoint>() ?? machine.GetComponentInParent<RoomWaypoint>();
            if (waypointOnMachine != null)
                return waypointOnMachine.type + "@" + waypointOnMachine.WorldPos.ToString("F2");
            return "machine:" + machine.name;
        }

        if (task.Payload is Component component && component != null)
            return "component:" + component.name;

        if (task.Payload is GameObject gameObject && gameObject != null)
            return "gameobject:" + gameObject.name;

        return task.Payload.ToString();
    }

    private static void Log(string robotId, string eventName, string payload)
    {
        int localSeq;
        lock (gate)
        {
            sequence++;
            localSeq = sequence;
        }

        Debug.Log(
            "[RobotEcosystemProbe] seq=" + localSeq
            + " time=" + Time.time.ToString("F3")
            + " robotId=" + robotId
            + " event=" + eventName
            + " payload=" + payload);
    }

    private static string ResolveRobotId(MonoBehaviour owner)
    {
        if (owner == null)
            return "unknown";
        return owner.name + "#" + owner.GetInstanceID();
    }
}
