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
    [SerializeField] private MonoBehaviour waypointServiceBehaviour;
    [SerializeField] private RobotStateController stateController;

    private IWaypointService waypointService;
    private IRobotRespawnService respawnService;

    private void Awake()
    {
        if (heart == null)
            heart = GetComponent<RobotHeart>();
        if (body == null)
            body = GetComponent<RobotBodyController>();
        if (memory == null)
            memory = GetComponent<RobotMemory>();
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();
        if (taskHandlers == null)
            taskHandlers = ResolveHandlersForRole(heart != null ? heart.Role : RobotRole.Worker);
        waypointService = waypointServiceBehaviour as IWaypointService;
        if (waypointService != null && body != null)
            body.Initialize(waypointService, waypointService, respawnService);
    }

    private void OnEnable()
    {
        if (heart != null)
            heart.OnTaskChanged += HandleTaskChanged;
        if (stateController != null)
            stateController.OnStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        if (heart != null)
            heart.OnTaskChanged -= HandleTaskChanged;
        if (stateController != null)
            stateController.OnStateChanged -= HandleStateChanged;
    }

    public void InitializeServices(IWaypointService waypointService, IRobotRespawnService respawnService)
    {
        this.waypointService = waypointService;
        this.respawnService = respawnService;
        if (body != null && waypointService != null)
            body.Initialize(waypointService, waypointService, respawnService);
        if (memory != null && respawnService != null)
            memory.SetRespawnService(respawnService);
    }

    /// <summary>
    /// Called when a machine relevant to this robot changes state.
    /// </summary>
    public void OnMachineStateChanged(object machine, bool isOn)
    {
        if (heart == null || config == null)
            return;

        RobotTask task = BuildTaskForMachine(machine, isOn);
        if (task != null)
        {
            if (!isOn && heart.CurrentTask != null && heart.CurrentTask.Type == RobotTaskType.WorkAtMachine)
                heart.CompleteCurrentTask();
            heart.TryPushTask(task);
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
    public IWaypointService WaypointService => waypointService;

    /// <summary>
    /// Entry point for perception to report that the player entered or left melee range.
    /// </summary>
    /// <param name="isInside">True when the player is inside the attack zone.</param>
    /// <param name="player">Player transform reported by perception.</param>
    public void OnPlayerInAttackZoneChanged(bool isInside, Transform player)
    {
        if (heart == null)
            return;

        if (isInside)
            heart.RequestAttackTarget(player);
        else
            heart.RequestEndAttack(player);
    }

    public void RequestAttackTarget(Transform target)
    {
        heart?.RequestAttackTarget(target);
    }

    public void OnDamageTaken(int damage)
    {
        if (memory != null)
            memory.RegisterAttack();

        if (stateController != null)
        {
            if (stateController.CurrentState == RobotState.Dead || stateController.CurrentState == RobotState.Faint)
            {
                HandleStateChanged(stateController.CurrentState);
            }
        }
    }

    private void HandleStateChanged(RobotState newState)
    {
        if (heart == null)
            return;

        if (newState == RobotState.Dead || newState == RobotState.Faint)
        {
            heart.ResetIntentStack(false);
        }
    }

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

    private RobotTask BuildTaskForMachine(object machine, bool isOn)
    {
        RobotTaskType? type = ResolveTaskType(machine, isOn);
        if (!type.HasValue)
            return null;

        object payload = ResolvePayload(machine, type.Value, isOn);
        float? timeout = config.GetTimeout(type.Value);
        int urgency = config.GetUrgency(type.Value);
        return new RobotTask(type.Value, payload, timeout, urgency);
    }

    private RobotTaskType? ResolveTaskType(object machine, bool isOn)
    {
        if (machine is SecurityMachine)
            return isOn ? RobotTaskType.GuardPost : RobotTaskType.ReactivateMachine;

        if (machine is RestingMachine)
            return isOn ? RobotTaskType.WorkAtMachine : RobotTaskType.Rest;

        if (machine is FactoryMachine)
            return isOn ? RobotTaskType.WorkAtMachine : RobotTaskType.Rest;

        if (machine is SpawningMachine)
            return isOn ? RobotTaskType.WorkAtMachine : RobotTaskType.Rest;

        if (machine is RoomWaypoint)
            return isOn ? RobotTaskType.WorkAtMachine : RobotTaskType.Rest;

        if (!isOn)
        {
            if (heart != null && heart.Role == RobotRole.Worker)
                return RobotTaskType.Rest;
            return RobotTaskType.ReactivateMachine;
        }

        if (config.ResumeWorkOnMachineOn)
            return RobotTaskType.WorkAtMachine;

        return null;
    }

    private object ResolvePayload(object machine, RobotTaskType type, bool isOn)
    {
        switch (type)
        {
            case RobotTaskType.WorkAtMachine:
                if (machine is RestingMachine && waypointService != null)
                {
                    var workPoint = waypointService.GetLeastUsedFreeWorkPoint();
                    if (workPoint != null)
                        return workPoint;
                }
                if (machine == null && waypointService != null)
                {
                    var poi = waypointService.GetWorkOrRestPoint();
                    if (poi != null)
                        return poi;
                }
                break;
            case RobotTaskType.Rest:
                if (machine is FactoryMachine && waypointService != null)
                {
                    var restPoint = waypointService.GetFirstRestPoint();
                    if (restPoint != null)
                        return restPoint;
                }
                if (machine == null && waypointService != null)
                {
                    var restPoint = waypointService.GetFirstRestPoint();
                    if (restPoint != null)
                        return restPoint;
                }
                break;
            case RobotTaskType.GuardPost:
                if (machine == null && waypointService != null)
                {
                    var guardPoint = waypointService.GetFirstFreeSecurityPoint();
                    if (guardPoint != null)
                        return guardPoint;
                }
                break;
            case RobotTaskType.ReactivateMachine:
                if (machine == null && waypointService != null)
                {
                    var startPoint = waypointService.GetStartPoint();
                    if (startPoint != null)
                        return startPoint;
                }
                break;
        }

        if (machine == null && memory != null && memory.LastVisitedPoint != null)
            return memory.LastVisitedPoint;

        return machine;
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

            // If the machine does not expose a RoomWaypoint, still move toward its position.
            body.SetDestination(machine.transform.position);
            return true;
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
        switch (type)
        {
            case RobotTaskType.Rest:
                return restDurationSeconds > 0f ? Time.time + restDurationSeconds : (float?)null;
            case RobotTaskType.GuardPost:
                return guardPostDurationSeconds > 0f ? Time.time + guardPostDurationSeconds : (float?)null;
            case RobotTaskType.WorkAtMachine:
                return workDurationSeconds > 0f ? Time.time + workDurationSeconds : (float?)null;
            case RobotTaskType.ReactivateMachine:
                return reactivateDurationSeconds > 0f ? Time.time + reactivateDurationSeconds : (float?)null;
            default:
                return defaultTimeoutSeconds > 0f ? Time.time + defaultTimeoutSeconds : (float?)null;
        }
    }

    public int GetUrgency(RobotTaskType type)
    {
        // Placeholder urgency mapping; refine by role/task.
        return type == RobotTaskType.ReactivateMachine ? 80 : 50;
    }

    [SerializeField] private float restDurationSeconds = 3f;
    [SerializeField] private float guardPostDurationSeconds = 300f;
    [SerializeField] private float workDurationSeconds = 120f;
    [SerializeField] private float reactivateDurationSeconds = 30f;
}
