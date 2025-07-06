using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

[RequireComponent(typeof(EnemyStateMachine), typeof(EnemyMemory))]
public class EnemyController : BaseAgentController, IRobotNavigationListener
{
    public EnemyStateMachine stateMachine;
    private MovementMonitor movementMonitor;
    private List<Vector3> currentPath;
    private List<RoomWaypoint> currentPathWaypoints;

    private int pathIndex;
    private IWaypointQueries waypointQueries;
    public IWaypointService waypointService;

    [SerializeField] private PlayerStateController robotBehaviour;
    [SerializeField] private float arrivalThresholdX = 2f;
    [SerializeField] private float arrivalThresholdY = 2f;
    [SerializeField] private float deadZoneX = 5f;
    [SerializeField] private float deadZoneY = 5f;

    private RoomWaypoint lastAttemptedWaypoint;
    public EnemyMemory memory { get; private set; }

    public WorkerState workerState { get; set; } = WorkerState.Idle;

    private bool isWithinX = false;
    private bool isWithinY = false;

    private void Awake()
    {
        stateMachine = GetComponent<EnemyStateMachine>();
        memory = GetComponent<EnemyMemory>();
        movementMonitor = new MovementMonitor();
        animator = GetComponentInChildren<Animator>();
        robotBehaviour.OnStateChanged += HandleStateChange;
    }

    public void Initialize(IWaypointQueries waypointQueries, IWaypointService waypointNotifier, EnemiesSpawner spawner)
    {
        this.waypointQueries = waypointQueries;
        this.waypointService = waypointNotifier;
        waypointNotifier.Subscribe(this);
        memory.SetSpawner(spawner);
        stateMachine.ChangeState(new EnemyState_Idle(this, stateMachine, (IWaypointService)waypointQueries));
    }

    protected override void Update()
    {
        base.Update();
        HandleMovementAndStuckDetection();
    }

    private void HandleMovementAndStuckDetection()
    {
        if (currentPath == null || pathIndex >= currentPath.Count)
            return;

        Vector3 target = currentPath[pathIndex];
        Vector3 currentPos = bodyReference.position;
        float dx = target.x - currentPos.x;
        float dy = target.y - currentPos.y;

        bool nearX = Mathf.Abs(dx) <= arrivalThresholdX;
        bool nearY = Mathf.Abs(dy) <= arrivalThresholdY;

        if (nearX && nearY)
        {
            pathIndex++;
            isWithinX = isWithinY = false;

            if (pathIndex >= currentPath.Count)
                movementMonitor.Reset(currentPos);
            return;
        }

        isWithinX = UpdateAxisHysteresis(isWithinX, dx, arrivalThresholdX, deadZoneX);
        isWithinY = UpdateAxisHysteresis(isWithinY, dy, arrivalThresholdY, deadZoneY);

        SetMovement(isWithinX ? 0f : Mathf.Sign(dx));
        SetVerticalMovement(isWithinY ? 0f : Mathf.Sign(dy));

        MovementStatus status = movementMonitor.Update(Time.deltaTime, currentPos);
        if (status == MovementStatus.Stuck)
        {
            memory.OnStuck(this);
        }
        else if (status == MovementStatus.ShouldAttemptRecovery && currentPathWaypoints?.Count > 0)
        {
            Debug.LogWarning($"[Recovery] Reattempting destination: '{currentPathWaypoints[^1].name}'");
            SetDestination(currentPathWaypoints[^1]);
        }
    }

    private bool UpdateAxisHysteresis(bool withinThreshold, float delta, float threshold, float deadZone)
    {
        if (!withinThreshold && Mathf.Abs(delta) <= threshold) return true;
        if (withinThreshold && Mathf.Abs(delta) > deadZone) return false;
        return withinThreshold;
    }

    public void SetDestination(RoomWaypoint target)
    {
        RoomWaypoint start = GetClosestWaypoint(target);

        if (start == target)
        {
            Debug.LogError($"Already at destination {target.name}, no pathfinding needed.");
            return;
        }

        lastAttemptedWaypoint = start;

        var raw = waypointQueries.FindWorldPath(start, target);
        if (raw == null || raw.Count == 0)
        {
            Debug.LogError($"No path from {start.name} to {target.name}.");
            return;
        }

        if (raw[0] != start) raw.Insert(0, start);
        if (raw[^1] != target) raw.Add(target);

        currentPathWaypoints = raw;
        currentPath = raw.Select(wp => wp.WorldPos).ToList();
        pathIndex = 1;
    }

    public bool HasArrivedAtDestination() =>
        currentPath != null && currentPath.Count > 0 && pathIndex >= currentPath.Count;

    public void OnPathObsoleted(RoomWaypoint blockedWaypoint)
    {
        Debug.Log($"Path to {blockedWaypoint.name} is blocked. Recalculating...");
    }

    public RoomWaypoint GetClosestWaypoint(RoomWaypoint exclude = null)
    {
        var agentY = bodyReference.position.y;

        var candidates = waypointQueries.GetActiveWaypoints()
            .Where(wp => Mathf.Abs(wp.WorldPos.y - agentY) < 5f && wp != exclude)
            .OrderBy(wp => Vector2.Distance(bodyReference.position, wp.WorldPos))
            .ToList();

        foreach (var wp in candidates)
        {
            if (lastAttemptedWaypoint == null || !wp.Equals(lastAttemptedWaypoint))
            {
                lastAttemptedWaypoint = wp;
                return wp;
            }
        }

        return candidates.FirstOrDefault();
    }

    private void HandleStateChange(RobotState newState)
    {
        switch (newState)
        {
            case RobotState.Faint:
                Faint();
                break;
            case RobotState.Dead:
                Die();
                break;
            case RobotState.Alive:
                UpdateBalance(true);
                break;
        }
    }

    public void Faint()
    {
        UpdateBalance(false);
    }

    public void Die()
    {
        var jointBreaker = GetComponent<JointBreaker>();
        jointBreaker?.BreakAll();
        SceneController.instance.RobotKilled();

        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        yield return new WaitForSeconds(5f);
        Destroy(gameObject);
    }

    private void DisableAnimator()
    {
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    private void UpdateBalance(bool enabledBalance)
    {
        var bodyBalance = GetComponent<BodyBalance>();
        if (bodyBalance != null)
        {
            bodyBalance.UpdateBalance(enabledBalance);
        }
    }

    private void OnDrawGizmos()
    {
        if (currentPathWaypoints == null || currentPathWaypoints.Count <= pathIndex)
            return;

        Gizmos.color = Color.magenta;
        Vector3 prev = bodyReference.position;

        for (int i = pathIndex; i < currentPathWaypoints.Count; i++)
        {
            var wp = currentPathWaypoints[i];
            Gizmos.DrawLine(prev, wp.WorldPos);
            Gizmos.DrawSphere(wp.WorldPos, 0.1f);
            prev = wp.WorldPos;
        }
    }
}
