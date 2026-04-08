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

    private void Awake()
    {
        if (brain == null)
            brain = GetComponent<RobotBrainNew>();
        if (body == null)
            body = GetComponent<RobotBodyController>();
        if (memory == null)
            memory = GetComponent<RobotMemoryNew>();

        taskRuntime = new RobotTaskNew();
        taskStack = new RobotTaskStackNew();
    }

    private void OnEnable()
    {
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

    private void ReactToBrainOptions(BrainOption options)
    {
        currentOptions = options;
    }

    private void OnPlannedTask(RobotTask planned)
    {
        if (planned == null)
            return;

        taskStack.PushOrRefresh(planned);
        StartTopTaskIfChanged();
    }

    /// <summary>
    /// Called by body/animation systems when the active task is completed.
    /// </summary>
    public void CompleteCurrentTask()
    {
        if (taskStack == null)
            return;

        CancelScheduledTaskSignal();
        taskStack.CompleteCurrent();
        if (taskStack.Current == null)
            taskStack.PushOrRefresh(BuildDefaultTask());

        StartTopTaskIfChanged();
    }

    /// <summary>
    /// Called by body/animation systems when the active task cannot continue for now.
    /// </summary>
    public void BlockCurrentTask()
    {
        CancelScheduledTaskSignal();
        // Task stays on top; we keep it as a debug point and wait for new brain planning.
    }

    /// <summary>
    /// Pushes a task from runtime execution (Task layer) and refreshes top evaluation.
    /// </summary>
    public void QueueTask(RobotTask task)
    {
        if (taskStack == null || task == null)
            return;

        taskStack.PushOrRefresh(task);
        StartTopTaskIfChanged();
    }

    /// <summary>
    /// Schedules a completion signal for the current task after a delay.
    /// </summary>
    public void ScheduleCompleteCurrentTask(float delaySeconds)
    {
        CancelScheduledTaskSignal();
        scheduledTaskSignal = StartCoroutine(CompleteCurrentTaskAfterDelay(Mathf.Max(0f, delaySeconds)));
    }

    private void StartTopTaskIfChanged()
    {
        var newTop = taskStack.Current;
        if (IsSameTask(activeTopTask, newTop))
            return;

        CancelScheduledTaskSignal();
        if (activeTopTask != null)
        {
            var reason = newTop == null ? TaskExitReason.Completed : TaskExitReason.Replanned;
            taskRuntime.Exit(reason);
        }

        activeTopTask = newTop;
        OnCurrentTaskChanged?.Invoke(activeTopTask);

        if (activeTopTask == null)
            return;

        var context = new RobotTaskContextNew
        {
            Role = role,
            CurrentTask = activeTopTask,
            Payload = activeTopTask.Payload,
            Options = currentOptions,
            Heart = this,
            Body = body,
            Memory = memory
        };
        taskRuntime.Enter(context);
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
        switch (role)
        {
            case RobotRole.Worker:
                return new RobotTask(RobotTaskType.WorkAtMachine);
            case RobotRole.SecurityGuard:
                return new RobotTask(RobotTaskType.GuardPost);
            case RobotRole.Follower:
                return new RobotTask(RobotTaskType.ChasePlayer);
            case RobotRole.WorkerSpawner:
                return new RobotTask(RobotTaskType.SpawnFollowers);
            case RobotRole.Boss:
                return new RobotTask(RobotTaskType.Patrol);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

}
