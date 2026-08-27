using UnityEngine;

/// <summary>
/// Executes Worker Collector navigation and cube carrying while reporting physical facts through Brain.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RobotBodyController))]
public class WorkerCollectorBodyController : MonoBehaviour, IWorkerCollectorTaskBody
{
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private RobotBodyController robotBody;
    [SerializeField] private RobotBrainNew brain;
    [Min(0.05f)]
    [SerializeField] private float pickupDistance = 0.6f;
    [Min(0.05f)]
    [SerializeField] private float cubeScanInterval = 0.5f;

    private WorkerCollectorMissionAssignment currentAssignment;
    private WhiteCubeCargo subscribedTarget;
    private int commandToken;
    private Command command;
    private Collider2D[] robotColliders;
    private Collider2D[] cargoColliders;

    private enum Command
    {
        None,
        MovingToCube,
        MovingToDropOff,
        WaitingAtDropOff
    }

    public Transform CarryAnchor => carryAnchor;
    public RobotBrainNew Brain => brain;
    public WorkerCollectorMissionAssignment CurrentAssignment => currentAssignment;

    private void Awake() => ResolveReferences();

    private void OnDisable()
    {
        WorkerCollectorMissionAssignment disabledAssignment = currentAssignment;
        UnsubscribeTarget();
        if (disabledAssignment != null && disabledAssignment.Target != null)
        {
            if (disabledAssignment.Target.Pickup != null
                && disabledAssignment.Target.transform.parent == carryAnchor)
                disabledAssignment.Target.Pickup.OnRelease(Vector2.zero);
            disabledAssignment.Target.ReleaseClaim(disabledAssignment.Claim);
        }
        currentAssignment = null;
        RestoreCargoCollisions();
        robotBody?.StopMovement();
        CancelInvoke(nameof(FindCube));
        command = Command.None;
    }

    private void Update()
    {
        if (currentAssignment == null)
            return;

        if (!IsTargetValid())
        {
            ReportTargetLost();
            return;
        }

        switch (command)
        {
            case Command.MovingToCube:
                if (DistanceFromCarryAnchor(currentAssignment.Target.transform.position) <= pickupDistance
                    || (robotBody != null && robotBody.HasArrivedAtDestination()))
                {
                    robotBody?.StopMovement();
                    command = Command.None;
                    brain?.OnWorkerCollectorBodyObservation(
                        WorkerCollectorBodyObservation.TargetApproach(currentAssignment, commandToken));
                }
                break;

            case Command.MovingToDropOff:
                if (robotBody != null && robotBody.HasArrivedAtDestination())
                {
                    robotBody.StopMovement();
                    command = Command.None;
                    brain?.OnWorkerCollectorBodyObservation(
                        WorkerCollectorBodyObservation.DropOffApproach(currentAssignment, commandToken));
                }
                break;
        }
    }

    /// <summary>
    /// Requests an assignment from the registered room capabilities.
    /// </summary>
    public void FindCube()
    {
        ResolveReferences();
        CancelInvoke(nameof(FindCube));
        if (!WorkerCollectorMissionService.RequestAssignment(this) && isActiveAndEnabled)
            Invoke(nameof(FindCube), cubeScanInterval);
    }

    public void BeginMoveToCube(WorkerCollectorMissionAssignment assignment)
    {
        if (!PrepareCommand(assignment))
            return;
        command = Command.MovingToCube;
        RoomWaypoint waypoint = assignment.Source.ApproachWaypoint;
        if (robotBody != null && waypoint != null)
        {
            Vector3 carryOffset = carryAnchor != null ? carryAnchor.position - transform.position : Vector3.zero;
            Vector3 workerApproachPosition = assignment.Target.transform.position - carryOffset;
            robotBody.SetDestination(waypoint, workerApproachPosition, includeUnavailable: true);
        }
    }

    public void GrabCube(WorkerCollectorMissionAssignment assignment)
    {
        if (!PrepareCommand(assignment))
            return;

        WhiteCubeCargo target = assignment.Target;
        if (target == null
            || DistanceFromCarryAnchor(target.transform.position) > pickupDistance
            || !target.TryBeginCarry(assignment.Claim, carryAnchor))
        {
            ReportTargetLost();
            return;
        }

        IgnoreCargoCollisions(target);
        brain?.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.Cargo(assignment, commandToken, secured: true));
    }

    public void BeginMoveToDropOff(WorkerCollectorMissionAssignment assignment)
    {
        if (!PrepareCommand(assignment))
            return;
        command = Command.MovingToDropOff;
        RoomWaypoint waypoint = assignment.DropOff.ApproachWaypoint;
        if (robotBody != null && waypoint != null)
            robotBody.SetDestination(waypoint, assignment.DropOff.WaitPosition, includeUnavailable: true);
    }

    public void WaitAtDropOff(WorkerCollectorMissionAssignment assignment)
    {
        if (!PrepareCommand(assignment))
            return;
        command = Command.WaitingAtDropOff;
        robotBody?.StopMovement();
    }

    public void CancelCurrentCommand(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment != null && !ReferenceEquals(currentAssignment, assignment))
            return;
        robotBody?.StopMovement();
        CancelInvoke(nameof(FindCube));
        command = Command.None;
    }

    public void StopAllActuators()
    {
        robotBody?.StopMovement();
        CancelInvoke(nameof(FindCube));
        command = Command.None;
    }

    private bool PrepareCommand(WorkerCollectorMissionAssignment assignment)
    {
        ResolveReferences();
        if (assignment == null || !assignment.HasRequiredReferences)
            return false;

        commandToken++;
        if (!ReferenceEquals(currentAssignment, assignment))
        {
            UnsubscribeTarget();
            currentAssignment = assignment;
            subscribedTarget = assignment.Target;
            if (subscribedTarget != null)
                subscribedTarget.OnClaimLost += HandleClaimLost;
        }

        if (!IsTargetValid())
        {
            ReportTargetLost();
            return false;
        }
        return true;
    }

    private bool IsTargetValid() => currentAssignment != null
        && currentAssignment.Target != null
            && currentAssignment.Target.IsClaimValid(currentAssignment.Claim);

    private float DistanceFromCarryAnchor(Vector3 position)
    {
        Vector3 origin = carryAnchor != null ? carryAnchor.position : transform.position;
        return Vector2.Distance(origin, position);
    }

    private void HandleClaimLost(WhiteCubeClaim claim)
    {
        if (currentAssignment != null && claim == currentAssignment.Claim)
            ReportTargetLost();
    }

    private void ReportTargetLost()
    {
        WorkerCollectorMissionAssignment lost = currentAssignment;
        if (lost == null)
            return;

        robotBody?.StopMovement();
        command = Command.None;
        RestoreCargoCollisions();
        UnsubscribeTarget();
        currentAssignment = null;
        commandToken++;
        brain?.OnWorkerCollectorBodyObservation(
            WorkerCollectorBodyObservation.TargetLost(lost, commandToken));
    }

    private void UnsubscribeTarget()
    {
        if (subscribedTarget != null)
            subscribedTarget.OnClaimLost -= HandleClaimLost;
        subscribedTarget = null;
    }

    private void IgnoreCargoCollisions(WhiteCubeCargo cargo)
    {
        RestoreCargoCollisions();
        if (cargo == null)
            return;
        robotColliders = GetComponentsInChildren<Collider2D>(true);
        cargoColliders = cargo.GetComponentsInChildren<Collider2D>(true);
        SetCollisionIgnore(true);
    }

    private void RestoreCargoCollisions()
    {
        SetCollisionIgnore(false);
        robotColliders = null;
        cargoColliders = null;
    }

    private void SetCollisionIgnore(bool ignore)
    {
        if (robotColliders == null || cargoColliders == null)
            return;
        for (int i = 0; i < robotColliders.Length; i++)
        {
            if (robotColliders[i] == null)
                continue;
            for (int j = 0; j < cargoColliders.Length; j++)
            {
                if (cargoColliders[j] != null)
                    Physics2D.IgnoreCollision(robotColliders[i], cargoColliders[j], ignore);
            }
        }
    }

    private void ResolveReferences()
    {
        if (carryAnchor == null)
            carryAnchor = transform.Find("CubeCarryAnchor");
        if (robotBody == null)
            robotBody = GetComponent<RobotBodyController>();
        if (brain == null)
            brain = GetComponent<RobotBrainNew>();
    }

    private void Reset() => ResolveReferences();
}
