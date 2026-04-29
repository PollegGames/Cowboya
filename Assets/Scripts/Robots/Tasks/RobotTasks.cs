using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Enumerates the high level intents used by the new Heart/Brain stack.
/// </summary>
public enum RobotTaskType
{
    Idle,
    GoToMachine,
    AttachToMachine,
    WorkAtMachine,
    GuardPost,
    SearchForMachine,
    ReactivateMachine,
    WaitAtMachine,
    ChasePlayer,
    AttackTarget,
    Flee,
    Rest,
    SpawnFollowers,
    ReturnHome,
    Patrol,
    Investigate,
    Cower,
    Faint,
    Dead
}

/// <summary>
/// Represents a single intent entry in the Heart stack.
/// </summary>
[Serializable]
public class RobotTask
{
    public RobotTaskType Type { get; }
    public object Payload { get; }
    public float? ExpireAt { get; }
    public int Urgency { get; }

    public RobotTask(RobotTaskType type, object payload = null, float? expireAt = null, int urgency = 0)
    {
        Type = type;
        Payload = payload;
        ExpireAt = expireAt;
        Urgency = urgency;
    }

    public bool IsExpired(float now) => ExpireAt.HasValue && now >= ExpireAt.Value;

    public RobotTask WithPayload(object payload) => new RobotTask(Type, payload, ExpireAt, Urgency);

    public RobotTask WithExpiry(float? expireAt) => new RobotTask(Type, Payload, expireAt, Urgency);
}

/// <summary>
/// Maintains the stack of intents with precedence and depth rules.
/// </summary>
public class RobotTaskStack
{
    private readonly List<RobotTask> stack = new List<RobotTask>();
    private readonly Dictionary<RobotTaskType, int> precedence;
    private readonly int maxDepth;

    public RobotTaskStack(int maxDepth, Dictionary<RobotTaskType, int> precedence)
    {
        this.maxDepth = Mathf.Max(1, maxDepth);
        this.precedence = precedence ?? throw new ArgumentNullException(nameof(precedence));
    }

    public RobotTask Current => stack.Count > 0 ? stack[stack.Count - 1] : null;

    public IReadOnlyList<RobotTask> Tasks => stack;

    public bool PushOrRefresh(RobotTask task, float now)
    {
        if (task == null)
            return false;

        var previousCurrent = Current;
        RemoveExpired(now);

        int existingIndex = stack.FindIndex(t => t.Type == task.Type);
        if (existingIndex >= 0)
        {
            stack[existingIndex] = MergeTasks(stack[existingIndex], task);
            ReorderByPrecedence(existingIndex);
            return HasVisibleChange(previousCurrent, Current);
        }

        int incomingPrecedence = GetPrecedence(task.Type);
        int currentTopPrecedence = Current != null ? GetPrecedence(Current.Type) : int.MinValue;

        if (incomingPrecedence > currentTopPrecedence && ShouldClearLowerPrecedenceTasks(task.Type))
            stack.RemoveAll(t => GetPrecedence(t.Type) < incomingPrecedence);

        int insertIndex = GetInsertIndex(incomingPrecedence);
        stack.Insert(insertIndex, task);
        TrimToDepth();
        return HasVisibleChange(previousCurrent, Current);
    }

    public bool CompleteCurrent()
    {
        if (stack.Count == 0)
            return false;
        stack.RemoveAt(stack.Count - 1);
        return true;
    }

    public bool RemoveExpired(float now)
    {
        int before = stack.Count;
        stack.RemoveAll(t => t.IsExpired(now));
        return before != stack.Count;
    }

    public bool RemoveTasksOfType(RobotTaskType type)
    {
        int before = stack.Count;
        stack.RemoveAll(t => t.Type == type);
        return before != stack.Count;
    }

    private void TrimToDepth()
    {
        int overflow = stack.Count - maxDepth;
        if (overflow > 0)
            stack.RemoveRange(0, overflow);
    }

    private void ReorderByPrecedence(int index)
    {
        if (index < 0 || index >= stack.Count)
            return;
        var task = stack[index];
        stack.RemoveAt(index);
        int insertIndex = GetInsertIndex(GetPrecedence(task.Type));
        stack.Insert(insertIndex, task);
    }

    private int GetInsertIndex(int incomingPrecedence)
    {
        int lastLowerIndex = stack.FindLastIndex(t => GetPrecedence(t.Type) <= incomingPrecedence);
        return lastLowerIndex + 1;
    }

    private int GetPrecedence(RobotTaskType type)
    {
        return precedence.TryGetValue(type, out int value) ? value : 0;
    }

    private static bool ShouldClearLowerPrecedenceTasks(RobotTaskType type)
    {
        switch (type)
        {
            case RobotTaskType.AttackTarget:
            case RobotTaskType.ChasePlayer:
                // Combat should temporarily override without erasing long-running intents.
                return false;
            default:
                return true;
        }
    }

    private static RobotTask MergeTasks(RobotTask existing, RobotTask incoming)
    {
        float? expiry = incoming.ExpireAt ?? existing.ExpireAt;
        int urgency = incoming.Urgency != 0 ? incoming.Urgency : existing.Urgency;
        object payload = incoming.Payload ?? existing.Payload;
        return new RobotTask(existing.Type, payload, expiry, urgency);
    }

    private static bool HasVisibleChange(RobotTask before, RobotTask after)
    {
        if (before == null && after == null)
            return false;
        if (before == null || after == null)
            return true;
        return before.Type != after.Type || !Equals(before.Payload, after.Payload);
    }
}
