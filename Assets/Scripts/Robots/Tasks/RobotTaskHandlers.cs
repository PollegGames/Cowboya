using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configurable task-to-action mapping for RobotBrain. Shared across roles by data.
/// </summary>
[CreateAssetMenu(menuName = "RobotAI/RobotTaskHandlers", fileName = "RobotTaskHandlers")]
public class RobotTaskHandlers : ScriptableObject
{
    [SerializeField] private List<RobotTaskHandlerEntry> entries = new();

    private Dictionary<RobotTaskType, IRobotTaskHandler> cached;

    private void OnEnable()
    {
        cached = null;
    }

    public bool TryHandle(RobotTaskType type, object payload, RobotBrain brain)
    {
        EnsureCache();
        if (cached.TryGetValue(type, out var handler) && handler != null)
        {
            handler.Execute(brain, payload);
            return true;
        }
        return false;
    }

    private void EnsureCache()
    {
        if (cached != null)
            return;
        cached = new Dictionary<RobotTaskType, IRobotTaskHandler>();
        foreach (var entry in entries)
        {
            if (entry == null || entry.Handler == null)
                continue;
            cached[entry.Type] = entry.Handler;
        }
    }
}

[Serializable]
public class RobotTaskHandlerEntry
    {
        public RobotTaskType Type;
        public ScriptableRobotTaskHandler Handler;
    }

public interface IRobotTaskHandler
{
    void Execute(RobotBrain brain, object payload);
}

/// <summary>
/// Base Scriptable handler so we can author per-role profiles in data.
/// </summary>
public abstract class ScriptableRobotTaskHandler : ScriptableObject, IRobotTaskHandler
{
    public abstract void Execute(RobotBrain brain, object payload);
}
