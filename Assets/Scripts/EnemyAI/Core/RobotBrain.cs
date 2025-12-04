using System;
using UnityEngine;

/// <summary>
/// Brain mediates between perception/events and the Heart intent stack,
/// applying a role config to decide which task to surface.
/// </summary>
[RequireComponent(typeof(RobotHeart))]
public class RobotBrain : MonoBehaviour
{
    [SerializeField] private RobotHeart heart;
    [SerializeField] private RobotBrainConfig config;
    [SerializeField] private RobotBodyController body;
    [SerializeField] private RobotMemory memory;
    [SerializeField] private RobotTaskHandlers taskHandlers;
    [SerializeField] private RoleTaskHandlerBinding[] roleTaskHandlers;

    private void Awake()
    {
        if (heart == null)
            heart = GetComponent<RobotHeart>();
        if (body == null)
            body = GetComponent<RobotBodyController>();
        if (memory == null)
            memory = GetComponent<RobotMemory>();
        if (taskHandlers == null)
            taskHandlers = ResolveHandlersForRole(heart != null ? heart.Role : RobotRole.Worker);
    }

    private void OnEnable()
    {
        if (heart != null)
            heart.OnTaskChanged += HandleTaskChanged;
    }

    private void OnDisable()
    {
        if (heart != null)
            heart.OnTaskChanged -= HandleTaskChanged;
    }

    /// <summary>
    /// Called when a machine relevant to this robot changes state.
    /// </summary>
    public void OnMachineStateChanged(object machine, bool isOn)
    {
        if (heart == null || config == null)
            return;

        if (!isOn)
        {
            heart.TryPushTask(new RobotTask(RobotTaskType.ReactivateMachine, machine, config.GetTimeout(RobotTaskType.ReactivateMachine), config.GetUrgency(RobotTaskType.ReactivateMachine)));
        }
        else if (config.ResumeWorkOnMachineOn)
        {
            heart.TryPushTask(new RobotTask(RobotTaskType.WorkAtMachine, machine, config.GetTimeout(RobotTaskType.WorkAtMachine), config.GetUrgency(RobotTaskType.WorkAtMachine)));
        }
    }

    protected virtual void HandleTaskChanged(RobotTask task)
    {
        if (task == null)
            return;

        if (taskHandlers != null && taskHandlers.TryHandle(task.Type, task.Payload, this))
            return;

        if (TryFallbackHandle(task))
            return;

        Debug.LogWarning($"[{nameof(RobotBrain)}] No handler for task {task.Type} on {name}");
    }

    public RobotBodyController Body => body;
    public RobotMemory Memory => memory;
    public RobotBrainConfig Config => config;

    private bool TryFallbackHandle(RobotTask task)
    {
        if (body == null)
            return false;

        switch (task.Type)
        {
            case RobotTaskType.WorkAtMachine:
            case RobotTaskType.ReactivateMachine:
            case RobotTaskType.Rest:
            case RobotTaskType.GuardPost:
            case RobotTaskType.ReturnHome:
            case RobotTaskType.Patrol:
            case RobotTaskType.Investigate:
            case RobotTaskType.Idle:
                return TryMoveToPayload(task.Payload);
            case RobotTaskType.ChasePlayer:
                if (memory != null && memory.LastKnownPlayerPosition != Vector3.zero)
                {
                    body.SetDestination(memory.LastKnownPlayerPosition);
                    return true;
                }
                return false;
            default:
                return false;
        }
    }

    private bool TryMoveToPayload(object payload)
    {
        if (payload is RoomWaypoint waypoint && waypoint != null)
        {
            body.SetDestination(waypoint);
            return true;
        }

        if (payload is BaseMachine machine && machine != null)
        {
            var target = machine.GetComponent<RoomWaypoint>();
            if (target != null)
            {
                body.SetDestination(target);
                return true;
            }
        }

        if (payload is Vector3 v3)
        {
            body.SetDestination(v3);
            return true;
        }

        if (payload is Vector2 v2)
        {
            body.SetDestination(v2);
            return true;
        }

        return false;
    }

    private RobotTaskHandlers ResolveHandlersForRole(RobotRole role)
    {
        if (roleTaskHandlers == null)
            return null;
        foreach (var binding in roleTaskHandlers)
        {
            if (binding != null && binding.Role == role && binding.Handlers != null)
                return binding.Handlers;
        }
        return null;
    }
}

[System.Serializable]
public class RoleTaskHandlerBinding
{
    public RobotRole Role;
    public RobotTaskHandlers Handlers;
}

/// <summary>
/// Lightweight per-role configuration for the Brain/Heart pairing.
/// </summary>
[Serializable]
public class RobotBrainConfig
{
    [SerializeField] private RobotRole role = RobotRole.Worker;
    [SerializeField] private bool resumeWorkOnMachineOn = true;
    [SerializeField] private float defaultTimeoutSeconds = 10f;

    public RobotRole Role => role;
    public bool ResumeWorkOnMachineOn => resumeWorkOnMachineOn;

    public float? GetTimeout(RobotTaskType type)
    {
        // Can be expanded per role/task later.
        return defaultTimeoutSeconds > 0f ? Time.time + defaultTimeoutSeconds : (float?)null;
    }

    public int GetUrgency(RobotTaskType type)
    {
        // Placeholder urgency mapping; refine by role/task.
        return type == RobotTaskType.ReactivateMachine ? 80 : 50;
    }
}
