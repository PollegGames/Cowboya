using System;
using System.Collections;
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
    [SerializeField] private float followerChaseRefreshSeconds = 0.5f;

    private IWaypointService waypointService;
    private IRobotRespawnService respawnService;
    private MachineSecurityManager securityManager;
    private SecurityMachine homeSecurityMachine;
    private BaseMachine pendingReactivateMachine;
    private Coroutine reactivateRoutine;
    private Coroutine waitAtMachineRoutine;
    private Coroutine followerChaseRoutine;
    [SerializeField] private float reactivateArrivalTimeoutSeconds = 8f;
    [SerializeField] private float reactivateProximityThreshold = 2f;
    [SerializeField] private bool logReactivation = true;

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
        StopFollowerChaseRefresh();
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

    public void SetSecurityManager(MachineSecurityManager manager)
    {
        securityManager = manager;
    }

    /// <summary>
    /// Called when a machine relevant to this robot changes state.
    /// </summary>
    public void OnMachineStateChanged(object machine, bool isOn)
    {
        if (heart == null)
            return;

        RobotTask task = BuildTaskForMachine(machine, isOn);
        if (task != null)
        {
            heart.CompleteCurrentTask();
            heart.TryPushTask(task);
        }
    }

    /// <summary>
    /// Allows external systems (e.g., security manager) to enqueue a specific task without
    /// running it through the machine-state mapping.
    /// </summary>
    public void PushExplicitTask(RobotTaskType type, object payload = null)
    {
        if (heart == null)
            return;

        float? timeout = config != null ? config.GetTimeout(type) : (float?)null;
        int urgency = config != null ? config.GetUrgency(type) : 0;
        heart.TryPushTask(new RobotTask(type, payload, timeout, urgency));

        if (type == RobotTaskType.ReactivateMachine && payload is BaseMachine machine)
            pendingReactivateMachine = machine;
    }

    protected virtual void HandleTaskChanged(RobotTask task)
    {
        if (task == null)
            return;

        if (taskHandlers != null && taskHandlers.TryHandle(task.Type, task.Payload, this))
        {
            HandleFollowerChaseRefresh(task);
            return;
        }

        if (TryFallbackHandle(task))
        {
            HandleFollowerChaseRefresh(task);
            return;
        }

        HandleFollowerChaseRefresh(task);
        Debug.LogWarning($"[{nameof(RobotBrain)}] No handler for task {task.Type} on {name}");
    }

    public RobotBodyController Body => body;
    public RobotMemory Memory => memory;
    public RobotBrainConfig Config => config;
    public IWaypointService WaypointService => waypointService;
    public RobotHeart Heart => heart;
    public SecurityMachine HomeSecurityMachine => homeSecurityMachine;
    public BaseMachine PendingReactivateMachine => pendingReactivateMachine;
    public float ReactivateArrivalTimeoutSeconds => reactivateArrivalTimeoutSeconds;
    public float ReactivateProximityThreshold => reactivateProximityThreshold;
    public bool LogReactivation => logReactivation;
    public bool IsSecurityGuard => heart != null && heart.Role == RobotRole.SecurityGuard;

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
        {
            if (Body != null && Body.AttackController != null)
                Body.AttackController.StopAttacking();
            heart.RequestEndAttack(player);
        }
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
            case RobotTaskType.Rest:
            case RobotTaskType.GuardPost:
            case RobotTaskType.ReturnHome:
            case RobotTaskType.Patrol:
            case RobotTaskType.Investigate:
            case RobotTaskType.Idle:
            case RobotTaskType.SpawnFollowers:
                {
                    return TryMoveToPayload(task.Payload);
                }
            case RobotTaskType.ChasePlayer:
                if (waypointService != null && waypointService.ClosestWaypointToPlayer != null)
                {
                    body.SetDestination(waypointService.ClosestWaypointToPlayer, includeUnavailable: true);
                    return true;
                }
                if (memory != null && memory.LastKnownPlayerPosition != Vector3.zero)
                {
                    body.SetDestination(memory.LastKnownPlayerPosition, includeUnavailable: true);
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

        if (machine is SecurityMachine security && isOn)
            homeSecurityMachine = security;

        object payload = ResolvePayload(machine, type.Value, isOn);
        float? timeout = config != null ? config.GetTimeout(type.Value) : (float?)null;
        int urgency = config != null ? config.GetUrgency(type.Value) : 0;
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
            return isOn ? RobotTaskType.SpawnFollowers : RobotTaskType.Rest;

        if (machine is RoomWaypoint)
            return isOn ? RobotTaskType.WorkAtMachine : RobotTaskType.Rest;

        if (!isOn)
        {
            if (heart != null && heart.Role == RobotRole.Worker)
                return RobotTaskType.Rest;
            return RobotTaskType.ReactivateMachine;
        }

        if (config != null && config.ResumeWorkOnMachineOn)
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

    public void CompleteCurrentTask()
    {
        heart?.CompleteCurrentTask();
    }

    public void RunWaitAtMachineRoutine(BaseMachine machine, float waitSeconds)
    {
        if (waitAtMachineRoutine != null)
            StopCoroutine(waitAtMachineRoutine);
        waitAtMachineRoutine = StartCoroutine(WaitAtMachineRoutine(machine, waitSeconds));
    }

    private IEnumerator WaitAtMachineRoutine(BaseMachine machine, float waitSeconds)
    {
        float duration = Mathf.Max(0f, waitSeconds);
        if (duration > 0f)
        {
            float endTime = Time.time + duration;
            while (Time.time < endTime)
                yield return null;
        }

        if (heart != null)
            heart.CompleteCurrentTask();

        waitAtMachineRoutine = null;
    }

    public bool HasArrivedAtExpectedMachine(BaseMachine machine, RoomWaypoint expectedWaypoint)
    {
        if (body == null || machine == null)
            return false;

        if (expectedWaypoint != null)
        {
            if (!ReferenceEquals(body.CurrentTarget, expectedWaypoint))
                return false;
            return body.HasArrivedAtDestination();
        }

        if (!body.HasArrivedAtDestination())
            return false;

        float threshold = Mathf.Max(0.1f, reactivateProximityThreshold);
        float dist = Vector2.Distance(body.transform.position, machine.transform.position);
        return dist <= threshold;
    }

    public void StartReactivateRoutine(IEnumerator routine)
    {
        if (routine == null)
            return;

        if (reactivateRoutine != null)
            StopCoroutine(reactivateRoutine);
        reactivateRoutine = StartCoroutine(routine);
    }

    public void EndReactivateRoutine()
    {
        reactivateRoutine = null;
    }

    public void CompleteReactivateTask(BaseMachine machine, bool reached)
    {
        if (ReferenceEquals(pendingReactivateMachine, machine))
            pendingReactivateMachine = null;

        heart?.CompleteCurrentTask();

        if (heart != null && heart.Role == RobotRole.SecurityGuard)
            SendGuardHomeOrRest();
    }

    private void SendGuardHomeOrRest()
    {
        if (securityManager != null && securityManager.RequestGuardPost(this))
            return;

        if (waypointService != null)
        {
            var securityPoint = waypointService.GetFirstFreeSecurityPoint();
            if (securityPoint != null)
            {
                PushExplicitTask(RobotTaskType.GuardPost, securityPoint);
                return;
            }

            var restPoint = waypointService.GetFirstRestPoint();
            if (restPoint != null)
            {
                PushExplicitTask(RobotTaskType.Rest, restPoint);
                return;
            }
        }

        PushExplicitTask(RobotTaskType.Rest);
    }

    private void HandleFollowerChaseRefresh(RobotTask task)
    {
        if (heart == null || heart.Role != RobotRole.Follower)
            return;

        if (task.Type == RobotTaskType.ChasePlayer)
            StartFollowerChaseRefresh();
        else
            StopFollowerChaseRefresh();
    }

    private void StartFollowerChaseRefresh()
    {
        if (followerChaseRoutine != null)
            StopCoroutine(followerChaseRoutine);
        followerChaseRoutine = StartCoroutine(FollowerChaseRefreshLoop());
    }

    private void StopFollowerChaseRefresh()
    {
        if (followerChaseRoutine == null)
            return;
        StopCoroutine(followerChaseRoutine);
        followerChaseRoutine = null;
    }

    private IEnumerator FollowerChaseRefreshLoop()
    {
        float interval = Mathf.Max(0.1f, followerChaseRefreshSeconds);
        var wait = new WaitForSeconds(interval);
        while (heart != null && heart.CurrentTask != null && heart.CurrentTask.Type == RobotTaskType.ChasePlayer)
        {
            if (memory != null && memory.PlayerInAttackZone)
            {
                body?.StopMovement();
                yield return wait;
                continue;
            }

            if (body != null && body.HasActivePath)
            {
                yield return wait;
                continue;
            }

            taskHandlers?.TryHandle(RobotTaskType.ChasePlayer, heart.CurrentTask.Payload, this);
            yield return wait;
        }
        followerChaseRoutine = null;
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
        [SerializeField] private float waitAtMachineSeconds = 5f;

        public RobotRole Role => role;
        public bool ResumeWorkOnMachineOn => resumeWorkOnMachineOn;
        public float WaitAtMachineSeconds => waitAtMachineSeconds;

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
            case RobotTaskType.SpawnFollowers:
                return 100000; // stay assigned to the spawner with no timeout
            case RobotTaskType.ReactivateMachine:
                return reactivateDurationSeconds > 0f ? Time.time + reactivateDurationSeconds : (float?)null;
            case RobotTaskType.WaitAtMachine:
                return waitAtMachineSeconds > 0f ? Time.time + waitAtMachineSeconds : (float?)null;
            default:
                return defaultTimeoutSeconds > 0f ? Time.time + defaultTimeoutSeconds : (float?)null;
            }
        }

        public int GetUrgency(RobotTaskType type)
        {
            // Placeholder urgency mapping; refine by role/task.
            switch (type)
            {
                case RobotTaskType.ReactivateMachine:
                    return 80;
                case RobotTaskType.WaitAtMachine:
                    return 70;
                default:
                    return 50;
            }
        }

        [SerializeField] private float restDurationSeconds = 3f;
        [SerializeField] private float guardPostDurationSeconds = 300f;
        [SerializeField] private float workDurationSeconds = 120f;
        [SerializeField] private float reactivateDurationSeconds = 30f;
    }
