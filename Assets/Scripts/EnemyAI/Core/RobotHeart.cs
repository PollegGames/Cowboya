using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Heart component that owns the intent stack and precedence rules.
/// </summary>
public class RobotHeart : MonoBehaviour
{
    public event Action<RobotTask> OnTaskChanged;

    [SerializeField] private RobotRole role = RobotRole.Worker;
    [SerializeField] private int overrideStackDepth = 0;
    [SerializeField] private bool seedIdleTask = true;
    [SerializeField] private bool logTaskChanges = false;

    private RobotTaskStack taskStack;

    public RobotTask CurrentTask => taskStack?.Current;
    public RobotRole Role => role;

    private void Awake()
    {
        ResetIntentStack(false);
    }

    private void Update()
    {
        if (taskStack == null)
            return;
        if (taskStack.RemoveExpired(Time.time))
            NotifyTaskChanged();
    }

    /// <summary>
    /// Rebuilds the intent stack using the configured precedence rules.
    /// </summary>
    /// <param name="notifyListeners">If true, raises OnTaskChanged after the reset.</param>
    public void ResetIntentStack(bool notifyListeners = true)
    {
        taskStack = new RobotTaskStack(GetStackDepth(), BuildPrecedenceTable());
        if (seedIdleTask)
            taskStack.PushOrRefresh(new RobotTask(RobotTaskType.Idle), Time.time);
        if (notifyListeners)
            NotifyTaskChanged();
    }

    /// <summary>
    /// Pushes or refreshes a task while respecting precedence and stack depth rules.
    /// </summary>
    /// <param name="task">The task to add or refresh.</param>
    /// <returns>True if the visible intent changed.</returns>
    public bool TryPushTask(RobotTask task)
    {
        if (taskStack == null)
            return false;
        bool changed = taskStack.PushOrRefresh(task, Time.time);
        if (changed)
            NotifyTaskChanged();
        return changed;
    }

    /// <summary>
    /// Marks the current task as complete and surfaces the next available intent.
    /// </summary>
    /// <returns>True if a task was removed.</returns>
    public bool CompleteCurrentTask()
    {
        if (taskStack == null)
            return false;
        bool changed = taskStack.CompleteCurrent();
        if (changed)
            NotifyTaskChanged();
        return changed;
    }

    /// <summary>
    /// Returns the full stack for debugging or brain inspection.
    /// </summary>
    public IReadOnlyList<RobotTask> GetTasks() => taskStack?.Tasks;

    private int GetStackDepth()
    {
        if (overrideStackDepth > 0)
            return overrideStackDepth;
        return role == RobotRole.Boss ? 1 : 3;
    }

    private Dictionary<RobotTaskType, int> BuildPrecedenceTable()
    {
        return new Dictionary<RobotTaskType, int>
        {
            { RobotTaskType.Flee, 100 },
            { RobotTaskType.AttackTarget, 90 },
            { RobotTaskType.ChasePlayer, 85 },
            { RobotTaskType.ReactivateMachine, 75 },
            { RobotTaskType.WorkAtMachine, 65 },
            { RobotTaskType.GuardPost, 65 },
            { RobotTaskType.SpawnFollowers, 65 },
            { RobotTaskType.ReturnHome, 60 },
            { RobotTaskType.Rest, 55 },
            { RobotTaskType.Investigate, 40 },
            { RobotTaskType.Patrol, 25 },
            { RobotTaskType.Cower, 20 },
            { RobotTaskType.Idle, 10 }
        };
    }

    private void NotifyTaskChanged()
    {
        OnTaskChanged?.Invoke(CurrentTask);
        if (logTaskChanges)
        {
            Debug.Log($"[RobotHeart] {name} -> {DescribeTask(CurrentTask)}");
        }
    }

    private string DescribeTask(RobotTask task)
    {
        if (task == null)
            return "None";
        string payload = "null";
        switch (task.Payload)
        {
            case RoomWaypoint waypoint:
                payload = waypoint.name;
                break;
            case BaseMachine machine:
                payload = machine.name;
                break;
            case Component component:
                payload = component.name;
                break;
            case null:
                payload = "null";
                break;
            default:
                payload = task.Payload.ToString();
                break;
        }
        return $"{task.Type} ({payload})";
    }
}
