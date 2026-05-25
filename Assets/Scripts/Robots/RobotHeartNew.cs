using System;
using System.Collections;
using UnityEngine;


/// <summary>
/// Heart component that owns the new LIFO task stack.
/// </summary>
public class RobotHeartNew : MonoBehaviour
{
    public event Action<RobotTask> OnCurrentTaskChanged;

    [SerializeField] private RobotBrainNew brain;
    [SerializeField] private RobotBodyController body;
    [SerializeField] private RobotMemoryNew memory;
    [SerializeField] private RobotRole role = RobotRole.Worker;

    private RobotTaskStackNew taskStack;
    private RobotTask activeTopTask;
    private IRobotTaskNew taskRuntime;
    private BrainOption currentOptions;
    private Coroutine scheduledTaskSignal;

    public RobotTask CurrentTask => taskStack?.Current;
    public RobotRole Role => role;

    public void ConfigureRole(RobotRole newRole, bool resetStack = true)
    {
        EnsureInitialized();

        if (role == newRole)
        {
            if (resetStack)
                ResetIntentStack(repopulateDefaultTask: true);
            return;
        }

        role = newRole;
        if (resetStack)
            ResetIntentStack(repopulateDefaultTask: true);
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        taskStack.PushOrRefresh(BuildDefaultTask());

        if (brain != null)
        {
            brain.UpdateBrainOption += ReactToBrainOptions;
            brain.UpdatePlannedTask += OnPlannedTask;

            if (brain.TryGetCurrentPlan(out var initialOptions, out var initialTask))
            {
                currentOptions = initialOptions;
                if (initialTask != null)
                    taskStack.PushOrRefresh(initialTask);
            }
        }

        StartTopTaskIfChanged();
    }

    private void OnDisable()
    {
        CancelScheduledTaskSignal();

        if (brain != null)
        {
            brain.UpdateBrainOption -= ReactToBrainOptions;
            brain.UpdatePlannedTask -= OnPlannedTask;
        }
    }

    private void Update()
    {
        if (!RobotNewPipelineRuntime.ShouldDriveGameplay)
            return;
        if (body == null || activeTopTask == null)
            return;

        if (activeTopTask.Type == RobotTaskType.ReactivateMachine && body.HasArrivedAtDestination())
        {
            CompleteReactivateTaskOnArrival(activeTopTask);
            return;
        }

        if (ShouldCompleteOnArrival(activeTopTask.Type) && body.HasArrivedAtDestination())
            CompleteCurrentTask();
    }

    private void ReactToBrainOptions(BrainOption options)
    {
        currentOptions = options;
    }

    private void OnPlannedTask(RobotTask planned)
    {
        if (planned == null)
            return;

        RemoveStalePerceptionTasksBeforePlan(planned);
        taskStack.PushOrRefresh(planned);
        RobotEcosystemProbe.RecordHeartPlannedTask(this, planned, taskStack.Current);
        RobotNewTrace.Log(
            this,
            eventSource: "HeartNew.OnPlannedTask",
            memoryDelta: "none",
            brainOptions: currentOptions,
            plannedTask: planned,
            heartCurrentTask: taskStack.Current,
            taskSignal: "push_refresh");
        StartTopTaskIfChanged();
    }

    /// <summary>
    /// Called by body/animation systems when the active task is completed.
    /// </summary>
    public void CompleteCurrentTask()
    {
        EnsureInitialized();
        if (taskStack == null)
            return;

        var before = taskStack.Current;
        bool resetAttackMemoryAfterPop = before != null && before.Type == RobotTaskType.Flee;
        if (!resetAttackMemoryAfterPop)
            ApplyMemoryTransitionsOnTaskCompletion(before);
        CancelScheduledTaskSignal();
        taskStack.CompleteCurrent();
        if (taskStack.Current == null)
            taskStack.PushOrRefresh(BuildDefaultTask());
        if (resetAttackMemoryAfterPop)
            memory?.ResetAttackMemory();

        RobotNewTrace.Log(
            this,
            eventSource: "HeartNew.CompleteCurrentTask",
            memoryDelta: "none",
            brainOptions: currentOptions,
            plannedTask: null,
            heartCurrentTask: taskStack.Current,
            taskSignal: "complete:" + (before != null ? before.Type.ToString() : "null"));
        StartTopTaskIfChanged();
    }

    private void ApplyMemoryTransitionsOnTaskCompletion(RobotTask completed)
    {
        if (memory == null || completed == null)
            return;

        if (role == RobotRole.Worker)
            return;

        switch (completed.Type)
        {
            case RobotTaskType.GoToMachine:
                if (body != null && body.CurrentTarget != null)
                    memory.SetLastVisitedPoint(body.CurrentTarget);
                memory.ChangeConnectionToMachine(true);
                break;

            case RobotTaskType.Patrol:
                if (role == RobotRole.Boss)
                {
                    var patrolTarget = ResolveWaypoint(completed.Payload);
                    if (patrolTarget == null && body != null)
                        patrolTarget = body.CurrentTarget;

                    if (patrolTarget != null)
                    {
                        memory.SetLastVisitedPoint(patrolTarget);
                        Debug.Log($"[Boss] Patrol completed at '{patrolTarget.name}'.", this);
                    }
                }
                break;

            case RobotTaskType.WorkAtMachine:
            case RobotTaskType.Rest:
                memory.ChangeConnectionToMachine(false);
                break;
        }
    }

    /// <summary>
    /// Called by body/animation systems when the active task cannot continue for now.
    /// </summary>
    public void BlockCurrentTask()
    {
        EnsureInitialized();
        CancelScheduledTaskSignal();
        RobotNewTrace.Log(
            this,
            eventSource: "HeartNew.BlockCurrentTask",
            memoryDelta: "none",
            brainOptions: currentOptions,
            plannedTask: null,
            heartCurrentTask: taskStack != null ? taskStack.Current : null,
            taskSignal: "block");
        // Task stays on top; we keep it as a debug point and wait for new brain planning.
    }

    /// <summary>
    /// Pushes a task from runtime execution (Task layer) and refreshes top evaluation.
    /// </summary>
    public void QueueTask(RobotTask task)
    {
        EnsureInitialized();
        if (taskStack == null || task == null)
            return;

        taskStack.PushOrRefresh(task);
        RobotNewTrace.Log(
            this,
            eventSource: "HeartNew.QueueTask",
            memoryDelta: "none",
            brainOptions: currentOptions,
            plannedTask: task,
            heartCurrentTask: taskStack.Current,
            taskSignal: "queue");
        StartTopTaskIfChanged();
    }

    /// <summary>
    /// Schedules a completion signal for the current task after a delay.
    /// </summary>
    public void ScheduleCompleteCurrentTask(float delaySeconds)
    {
        EnsureInitialized();
        CancelScheduledTaskSignal();
        if (!isActiveAndEnabled)
            return;

        scheduledTaskSignal = StartCoroutine(CompleteCurrentTaskAfterDelay(Mathf.Max(0f, delaySeconds)));
    }

    /// <summary>
    /// Backward-compatible reset hook used by legacy pool lifecycle code.
    /// </summary>
    /// <param name="repopulateDefaultTask">Whether to push the role default task after reset.</param>
    public void ResetIntentStack(bool repopulateDefaultTask = true)
    {
        EnsureInitialized();
        CancelScheduledTaskSignal();

        if (activeTopTask != null && taskRuntime != null)
            taskRuntime.Exit(BuildTaskContext(activeTopTask), TaskExitReason.Replanned);

        taskStack = new RobotTaskStackNew();
        if (repopulateDefaultTask)
            taskStack.PushOrRefresh(BuildDefaultTask());

        activeTopTask = null;
        if (!isActiveAndEnabled)
            return;

        StartTopTaskIfChanged();
    }

    private void StartTopTaskIfChanged()
    {
        EnsureInitialized();
        if (!isActiveAndEnabled)
            return;

        var newTop = taskStack.Current;
        if (IsSameTask(activeTopTask, newTop))
            return;

        var previousTop = activeTopTask;
        CancelScheduledTaskSignal();
        if (activeTopTask != null)
        {
            var reason = newTop == null ? TaskExitReason.Completed : TaskExitReason.Replanned;
            taskRuntime.Exit(BuildTaskContext(activeTopTask), reason);
        }

        activeTopTask = newTop;
        OnCurrentTaskChanged?.Invoke(activeTopTask);
        RobotEcosystemProbe.RecordHeartCurrentTaskChanged(this, activeTopTask);
        if (role == RobotRole.Worker && RobotNewPipelineRuntime.IsWorkerCycleValidationEnabled)
            RobotEcosystemProbe.RecordWorkerCycleTransition(this, previousTop, activeTopTask, "replan");

        if (activeTopTask == null)
            return;

        taskRuntime.Enter(BuildTaskContext(activeTopTask));
    }

    private void EnsureInitialized()
    {
        if (brain == null)
            brain = GetComponent<RobotBrainNew>();
        if (body == null)
            body = GetComponent<RobotBodyController>();
        if (memory == null)
            memory = GetComponent<RobotMemoryNew>();

        if (taskRuntime == null)
            taskRuntime = new RobotTaskNew();
        if (taskStack == null)
            taskStack = new RobotTaskStackNew();
    }

    private IEnumerator CompleteCurrentTaskAfterDelay(float delaySeconds)
    {
        if (delaySeconds > 0f)
            yield return new WaitForSeconds(delaySeconds);

        CompleteCurrentTask();
        scheduledTaskSignal = null;
    }

    private void CancelScheduledTaskSignal()
    {
        if (scheduledTaskSignal == null)
            return;

        StopCoroutine(scheduledTaskSignal);
        scheduledTaskSignal = null;
    }

    private static bool IsSameTask(RobotTask left, RobotTask right)
    {
        if (left == null && right == null)
            return true;
        if (left == null || right == null)
            return false;
        return left.Type == right.Type && Equals(left.Payload, right.Payload);
    }

    private RobotTask BuildDefaultTask()
    {
        RobotTask defaultTask;
        switch (role)
        {
            case RobotRole.Worker:
                defaultTask = new RobotTask(RobotTaskType.WorkAtMachine);
                break;
            case RobotRole.SecurityGuard:
                defaultTask = new RobotTask(RobotTaskType.GuardPost);
                break;
            case RobotRole.Follower:
                defaultTask = new RobotTask(RobotTaskType.ChasePlayer);
                break;
            case RobotRole.WorkerSpawner:
                defaultTask = new RobotTask(RobotTaskType.SpawnFollowers);
                break;
            case RobotRole.Boss:
                defaultTask = new RobotTask(RobotTaskType.Patrol);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        RobotEcosystemProbe.RecordHeartDefaultTask(this, defaultTask);
        return defaultTask;
    }

    private RobotTaskContextNew BuildTaskContext(RobotTask task)
    {
        return new RobotTaskContextNew
        {
            Role = role,
            CurrentTask = task,
            Payload = task != null ? task.Payload : null,
            Options = currentOptions,
            Heart = this,
            Body = body,
            Memory = memory
        };
    }

    private void RemoveStalePerceptionTasksBeforePlan(RobotTask planned)
    {
        if (taskStack == null || planned == null)
            return;

        if (planned.Type != RobotTaskType.AttackTarget && !currentOptions.HasFlag(BrainOption.CanAttack))
        {
            bool removedAttack = taskStack.RemoveTasksOfType(RobotTaskType.AttackTarget);
            if (removedAttack)
            {
                RobotNewTrace.Log(
                    this,
                    eventSource: "HeartNew.RemoveStalePerceptionTasks",
                    memoryDelta: "none",
                    brainOptions: currentOptions,
                    plannedTask: planned,
                    heartCurrentTask: taskStack.Current,
                    taskSignal: "remove:AttackTarget");
            }
        }

        if (planned.Type != RobotTaskType.ChasePlayer && !currentOptions.HasFlag(BrainOption.PlayerDetected))
        {
            bool removedChase = taskStack.RemoveTasksOfType(RobotTaskType.ChasePlayer);
            if (removedChase)
            {
                RobotNewTrace.Log(
                    this,
                    eventSource: "HeartNew.RemoveStalePerceptionTasks",
                    memoryDelta: "none",
                    brainOptions: currentOptions,
                    plannedTask: planned,
                    heartCurrentTask: taskStack.Current,
                    taskSignal: "remove:ChasePlayer");
            }
        }
    }

    private static bool ShouldCompleteOnArrival(RobotTaskType type)
    {
        switch (type)
        {
            case RobotTaskType.GoToMachine:
            case RobotTaskType.ReturnHome:
            case RobotTaskType.Patrol:
            case RobotTaskType.Flee:
                return true;
            default:
                return false;
        }
    }

    private void CompleteReactivateTaskOnArrival(RobotTask task)
    {
        BaseMachine machine = ResolveMachine(task != null ? task.Payload : null);
        if (machine == null)
            return;

        if (!machine.IsOn)
            machine.PowerOn();

        RobotNewTrace.Log(
            this,
            eventSource: "HeartNew.ReactivateMachine.Arrived",
            memoryDelta: "none",
            brainOptions: currentOptions,
            plannedTask: task,
            heartCurrentTask: CurrentTask,
            taskSignal: "reactivate_arrived");

        if (brain != null)
            brain.CompleteReactivateTask(machine, reached: true);
        else
            CompleteCurrentTask();
    }

    private static BaseMachine ResolveMachine(object payload)
    {
        if (payload is BaseMachine baseMachine)
            return baseMachine;

        if (payload is RoomWaypoint waypoint && waypoint != null)
            return waypoint.GetComponent<BaseMachine>() ?? waypoint.GetComponentInParent<BaseMachine>();

        if (payload is Component component && component != null)
            return component.GetComponent<BaseMachine>() ?? component.GetComponentInParent<BaseMachine>();

        if (payload is GameObject go && go != null)
            return go.GetComponent<BaseMachine>() ?? go.GetComponentInParent<BaseMachine>();

        return null;
    }

    private static RoomWaypoint ResolveWaypoint(object payload)
    {
        if (payload is RoomWaypoint waypoint)
            return waypoint;

        if (payload is Component component && component != null)
            return component.GetComponent<RoomWaypoint>() ?? component.GetComponentInParent<RoomWaypoint>();

        if (payload is GameObject go && go != null)
            return go.GetComponent<RoomWaypoint>() ?? go.GetComponentInParent<RoomWaypoint>();

        return null;
    }

}
