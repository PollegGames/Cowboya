using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LIFO stack used by the new robot pipeline.
/// No precedence table, no global reordering.
/// </summary>
public class RobotTaskStackNew
{
    private readonly List<RobotTask> stack = new List<RobotTask>();
    [SerializeField] private int maxDepth = 20;

    public RobotTaskStackNew()
    {
    }

    public RobotTask Current => stack.Count > 0 ? stack[stack.Count - 1] : null;

    public IReadOnlyList<RobotTask> Tasks => stack;

    /// <summary>
    /// Pushes a task to the top, or refreshes/moves an existing one.
    /// Returns true if top task changed.
    /// </summary>
    public bool PushOrRefresh(RobotTask task)
    {
        if (task == null)
            return false;

        var before = Current;

        int existingIndex = FindTaskIndex(task);
        if (existingIndex >= 0)
            stack.RemoveAt(existingIndex);

        stack.Add(task);
        TrimToDepth();

        return !IsSameTask(before, Current);
    }

    public bool CompleteCurrent()
    {
        if (stack.Count == 0)
            return false;

        stack.RemoveAt(stack.Count - 1);
        return true;
    }

    public bool RemoveTasksOfType(RobotTaskType type)
    {
        int before = stack.Count;
        stack.RemoveAll(t => t.Type == type);
        return before != stack.Count;
    }

    /// <summary>
    /// Replaces every sequential Collector phase with one authoritative phase task.
    /// </summary>
    public bool ReplaceCollectorFamily(RobotTask task)
    {
        if (task == null || !IsCollectorFamily(task.Type))
            return false;

        var before = Current;
        stack.RemoveAll(existing => existing != null && IsCollectorFamily(existing.Type));
        stack.Add(task);
        TrimToDepth();
        return !IsSameTask(before, Current);
    }

    /// <summary>
    /// Returns whether a task belongs to the sequential Collector mission family.
    /// </summary>
    public static bool IsCollectorFamily(RobotTaskType type)
    {
        switch (type)
        {
            case RobotTaskType.CollectorStandby:
            case RobotTaskType.CollectorLaunch:
            case RobotTaskType.CollectorFlyToTarget:
            case RobotTaskType.CollectorGatherCargo:
            case RobotTaskType.CollectorReturnHome:
            case RobotTaskType.CollectorAbortAndReturn:
            case RobotTaskType.CollectorDock:
                return true;
            default:
                return false;
        }
    }

    private int FindTaskIndex(RobotTask incoming)
    {
        for (int i = 0; i < stack.Count; i++)
        {
            if (IsSameTask(stack[i], incoming))
                return i;
        }

        return -1;
    }

    private void TrimToDepth()
    {
        int overflow = stack.Count - maxDepth;
        if (overflow > 0)
            stack.RemoveRange(0, overflow);
    }

    private static bool IsSameTask(RobotTask left, RobotTask right)
    {
        if (left == null && right == null)
            return true;
        if (left == null || right == null)
            return false;
        return left.Type == right.Type && Equals(left.Payload, right.Payload);
    }
}
