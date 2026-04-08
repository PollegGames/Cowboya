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
    [SerializeField] private RobotMemory memory;
    [SerializeField] private RobotBrainConfig config;
    [SerializeField] private int overrideStackDepth = 0;
    [SerializeField] private bool seedPrimaryTask = true;
    [SerializeField] private bool logStackChanges = false;
    [SerializeField] private bool logPrimaryTaskDecisions = false;

    private RobotTaskStack taskStack;

    public RobotTask CurrentTask => taskStack?.Current;
    public RobotRole Role => role;

    private void Awake()
    {
        if (memory == null)
            memory = GetComponent<RobotMemory>();
        if (config == null)
        {
            var brain = GetComponent<RobotBrain>();
            if (brain != null)
                config = brain.Config;
        }
        ResetIntentStack(false);
    }

    private void Update()
    {
        if (taskStack == null)
            return;
        bool removed = taskStack.RemoveExpired(Time.time);
        bool reseeded = removed && EnsurePrimaryTaskSeeded();
        if (removed || reseeded)
            NotifyTaskChanged();
    }

    /// <summary>
    /// Rebuilds the intent stack using the configured precedence rules.
    /// </summary>
    /// <param name="notifyListeners">If true, raises OnTaskChanged after the reset.</param>
    public void ResetIntentStack(bool notifyListeners = true)
    {
        taskStack = new RobotTaskStack(GetStackDepth(), BuildPrecedenceTable());
        if (seedPrimaryTask)
        {
            var primary = BuildPrimaryTask();
            if (primary != null)
                taskStack.PushOrRefresh(primary, Time.time);
        }
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
    public bool CompleteCurrentTask(bool reseedPrimary = true)
    {
        if (taskStack == null)
            return false;
        bool changed = taskStack.CompleteCurrent();
        bool reseeded = changed && reseedPrimary && EnsurePrimaryTaskSeeded();
        if (changed || reseeded)
            NotifyTaskChanged();
        return changed;
    }

    /// <summary>
    /// Returns the full stack for debugging or brain inspection.
    /// </summary>
    public IReadOnlyList<RobotTask> GetTasks() => taskStack?.Tasks;

    /// <summary>
    /// Removes all tasks of the given type.
    /// </summary>
    public bool RemoveTasksOfType(RobotTaskType type)
    {
        if (taskStack == null)
            return false;
        bool removed = taskStack.RemoveTasksOfType(type);
        if (removed)
            NotifyTaskChanged();
        return removed;
    }

    /// <summary>
    /// Requests that the heart consider attacking a specific player target.
    /// </summary>
    /// <param name="player">Transform of the player to attack.</param>
    public void RequestAttackTarget(Transform player)
    {
        if (taskStack == null || player == null)
            return;

        if (ShouldAttackPlayer())
        {
            PushAttackTask(player);
            return;
        }

        HandleAttackDeclined(player);
    }

    /// <summary>
    /// Handles the end of an attack opportunity, removing attack intent and potentially chasing.
    /// </summary>
    /// <param name="player">Player that left the attack zone.</param>
    public void RequestEndAttack(Transform player)
    {
        if (taskStack == null)
            return;

        bool removed = taskStack.RemoveTasksOfType(RobotTaskType.AttackTarget);
        bool reseeded = removed && EnsurePrimaryTaskSeeded();
        if (removed || reseeded)
            NotifyTaskChanged();

        if (memory != null && memory.LastKnownPlayerPosition != Vector3.zero)
        {
            TryPushTask(new RobotTask(
                RobotTaskType.ChasePlayer,
                memory.LastKnownPlayerPosition,
                config != null ? config.GetTimeout(RobotTaskType.ChasePlayer) : (float?)null,
                config != null ? config.GetUrgency(RobotTaskType.ChasePlayer) : 0));
        }
    }

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
            { RobotTaskType.WaitAtMachine, 70 },
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
        if (logStackChanges)
        {
            Debug.Log($"[RobotHeart] {name} intent={DescribePrimaryIntent(CurrentTask)} current={DescribeTask(CurrentTask)} stack={DescribeStack()}");
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

    private string DescribePrimaryIntent(RobotTask task)
    {
        if (task == null)
            return "None";
        switch (task.Type)
        {
            case RobotTaskType.AttackTarget:
                return "Attack";
            case RobotTaskType.ChasePlayer:
            case RobotTaskType.Flee:
            case RobotTaskType.ReactivateMachine:
            case RobotTaskType.ReturnHome:
            case RobotTaskType.Patrol:
            case RobotTaskType.Investigate:
                return "Move";
            case RobotTaskType.WorkAtMachine:
            case RobotTaskType.GuardPost:
            case RobotTaskType.Rest:
            case RobotTaskType.SpawnFollowers:
            case RobotTaskType.Cower:
            case RobotTaskType.WaitAtMachine:
            case RobotTaskType.Idle:
                return "Stay";
            default:
                return "Unknown";
        }
    }

    private string DescribeStack()
    {
        if (taskStack == null || taskStack.Tasks == null || taskStack.Tasks.Count == 0)
            return "[]";
        var parts = new string[taskStack.Tasks.Count];
        for (int i = 0; i < taskStack.Tasks.Count; i++)
            parts[i] = DescribeTask(taskStack.Tasks[i]);
        return "[" + string.Join(" > ", parts) + "]";
    }

    private RobotTask BuildPrimaryTask()
    {
        var brain = GetComponent<RobotBrain>();
        object payload = null;
        RobotTaskType type;

        switch (role)
        {
            case RobotRole.Worker:
                type = RobotTaskType.ReturnHome;
                payload = brain != null && brain.WaypointService != null ? brain.WaypointService.GetStartPoint() : null;
                break;
            case RobotRole.SecurityGuard:
                type = RobotTaskType.GuardPost;
                payload = brain != null && brain.WaypointService != null ? brain.WaypointService.GetFirstFreeSecurityPoint() : null;
                break;
            case RobotRole.Follower:
                type = RobotTaskType.ChasePlayer;
                payload = brain != null && brain.WaypointService != null ? brain.WaypointService.ClosestWaypointToPlayer : null;
                break;
            case RobotRole.WorkerSpawner:
                type = RobotTaskType.SpawnFollowers;
                payload = null;
                break;
            case RobotRole.Boss:
                type = RobotTaskType.ReturnHome;
                if (brain != null && brain.WaypointService != null)
                {
                    payload = brain.WaypointService.GetEndPoint();
                    if (payload == null)
                        payload = brain.WaypointService.GetStartPoint();
                }
                break;
            default:
                return null;
        }

        float? timeout = config != null ? config.GetTimeout(type) : (float?)null;
        int urgency = config != null ? config.GetUrgency(type) : 0;
        return new RobotTask(type, payload, timeout, urgency);
    }

    private bool EnsurePrimaryTaskSeeded()
    {
        if (!seedPrimaryTask || taskStack == null)
            return false;
        if (taskStack.Tasks != null && taskStack.Tasks.Count > 0)
            return false;

        var primary = BuildPrimaryTask();
        if (primary == null)
            return false;

        return taskStack.PushOrRefresh(primary, Time.time);
    }

    private bool ShouldAttackPlayer()
    {
        if (memory != null && !memory.PlayerInAttackZone && !memory.WasRecentlyAttacked)
            return false;

        if (memory != null && memory.WasRecentlyAttacked)
            return true;

        switch (role)
        {
            case RobotRole.SecurityGuard:
            case RobotRole.Follower:
            case RobotRole.Boss:
                return true;
            default:
                return false;
        }
    }

    private void PushAttackTask(Transform player)
    {
        float? expireAt = config != null ? config.GetTimeout(RobotTaskType.AttackTarget) : (float?)null;
        int urgency = config != null ? config.GetUrgency(RobotTaskType.AttackTarget) : 0;
        TryPushTask(new RobotTask(RobotTaskType.AttackTarget, player, expireAt, urgency));
    }

    private void HandleAttackDeclined(Transform player)
    {
        if (role == RobotRole.Worker)
        {
            TryPushTask(new RobotTask(RobotTaskType.Flee, player));
            return;
        }

        if (memory != null && memory.LastKnownPlayerPosition != Vector3.zero)
        {
            TryPushTask(new RobotTask(
                RobotTaskType.ChasePlayer,
                memory.LastKnownPlayerPosition,
                config != null ? config.GetTimeout(RobotTaskType.ChasePlayer) : (float?)null,
                config != null ? config.GetUrgency(RobotTaskType.ChasePlayer) : 0));
            return;
        }

        TryPushTask(new RobotTask(RobotTaskType.Cower, player));
    }
}
