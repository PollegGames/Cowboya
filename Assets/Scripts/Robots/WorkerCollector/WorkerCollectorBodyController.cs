using System.Reflection;
using UnityEngine;

/// <summary>
/// Executes Worker Collector tasks and records physical observations in Memory for Brain to evaluate.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RobotBodyController))]
[DefaultExecutionOrder(-20)]
public class WorkerCollectorBodyController : MonoBehaviour, IWorkerCollectorTaskBody
{
    [SerializeField] private Transform carryAnchor;
    [SerializeField] private Transform armSolverTarget;
    [SerializeField] private Transform armEffector;
    [SerializeField] private Transform leftPuppetHand;
    [SerializeField] private Transform rightPuppetHand;
    [SerializeField] private RobotObjectArmReachController armReachController;
    [SerializeField] private RobotBodyController robotBody;
    [SerializeField] private RobotBrainNew brain;
    [Min(0.05f)]
    [SerializeField] private float pickupDistance = 0.6f;
    [SerializeField] private Vector2 pickupArrivalThreshold = new Vector2(0.15f, 2f);
    [Min(0.05f)]
    [SerializeField] private float cubeScanInterval = 0.5f;
    [Min(0.1f)]
    [SerializeField] private float armReachSpeed = 2f;
    [Min(0.1f)]
    [SerializeField] private float armReturnSpeed = 6f;
    [Min(0.1f)]
    [SerializeField] private float armReachDiagnosticInterval = 3f;
    [Min(0.1f)]
    [SerializeField] private float armReachStartDistance = 1.2f;
    [Min(0.1f)]
    [SerializeField] private float minimumArmReachStartDistance = 0.8f;
    [Min(0.1f)]
    [SerializeField] private float maximumArmReachStartDistance = 1.6f;

    private WorkerCollectorMissionAssignment currentAssignment;
    private WhiteCubeCargo subscribedTarget;
    private int commandToken;
    private Command command;
    private Collider2D[] robotColliders;
    private Collider2D[] cargoColliders;
    private Vector3 armRestLocalPosition;
    private Quaternion armRestLocalRotation;
    private float nextArmReachDiagnosticAt;
    private bool armRestPoseCached;
    private bool holdArmPose;
    private Transform securedCarryAnchor;
    private RobotMemoryNew memory;

    private enum Command
    {
        None,
        MovingToSource,
        AcquiringCube,
        MovingToCube,
        ReachingForCube,
        MovingToDropOff,
        WaitingAtDropOff,
        WaitingForGarage,
        MovingToRest,
        Resting
    }

    public Transform CarryAnchor => EffectiveCarryAnchor;
    public RobotBrainNew Brain => brain;
    public WorkerCollectorMissionAssignment CurrentAssignment => currentAssignment;
    public Vector2 PickupArrivalThreshold => pickupArrivalThreshold;
    public float AcquisitionRadius => armReachController != null
        ? armReachController.ReachRadius + pickupDistance
        : maximumArmReachStartDistance + pickupDistance;

    private void Awake() => ResolveReferences();

    private void OnDisable()
    {
        WorkerCollectorMissionAssignment disabledAssignment = currentAssignment;
        UnsubscribeTarget();
        if (disabledAssignment != null && disabledAssignment.Target != null)
        {
            if (disabledAssignment.Target.Pickup != null
                && disabledAssignment.Target.transform.parent == EffectiveCarryAnchor)
                disabledAssignment.Target.Pickup.OnRelease(Vector2.zero);
            disabledAssignment.Target.ReleaseClaim(disabledAssignment.Claim);
            disabledAssignment.DropOff?.ReleaseReservation(disabledAssignment.Reservation);
        }
        currentAssignment = null;
        RestoreCargoCollisions();
        robotBody?.StopMovement();
        CancelInvoke(nameof(FindCube));
        CancelInvoke(nameof(ScanForCube));
        CancelInvoke(nameof(CompleteRest));
        command = Command.None;
        holdArmPose = false;
        securedCarryAnchor = null;
        armReachController?.ClearTarget();
        RestoreArmPoseImmediate();
    }

    private void Update()
    {
        if (currentAssignment == null)
            return;

        if (currentAssignment.Source == null || !currentAssignment.Source.isActiveAndEnabled
            || currentAssignment.DropOff == null || !currentAssignment.DropOff.isActiveAndEnabled)
        {
            ReportDestinationUnavailable();
            return;
        }

        if ((command == Command.MovingToCube
                || command == Command.ReachingForCube
                || command == Command.MovingToDropOff
                || command == Command.WaitingForGarage)
            && !IsTargetValid())
        {
            ReportTargetLost();
            return;
        }

        switch (command)
        {
            case Command.MovingToSource:
                if (robotBody != null && robotBody.HasArrivedAtDestination())
                {
                    robotBody.StopMovement();
                    command = Command.None;
                    Observe(WorkerCollectorBodyObservation.SourceApproach(currentAssignment, commandToken));
                }
                break;

            case Command.MovingToCube:
                bool navigationArrived = robotBody != null && robotBody.HasArrivedAtDestination();
                if (navigationArrived)
                {
                    robotBody?.StopMovement();
                    if (!ValidatePickupApproach())
                    {
                        ReportTargetLost();
                        break;
                    }
                    command = Command.None;
                    Observe(
                        WorkerCollectorBodyObservation.TargetApproach(currentAssignment, commandToken));
                }
                break;

            case Command.ReachingForCube:
                if (memory != null && memory.Snapshot.WorkerCollector.SourceApproachReached
                    && !currentAssignment.Source.IsTargetInCollectionArea(this, currentAssignment.Target))
                {
                    ReportTargetLost();
                    break;
                }
                Vector2 pickupPoint = GetPickupPoint(currentAssignment);
                Transform reachReference = GetActiveReachEffector();
                float reachDistance = CalculateReachDistance(currentAssignment);
                if (reachDistance <= pickupDistance)
                {
                    TrySecureReachedCube(reachDistance);
                }
                else if (Time.time >= nextArmReachDiagnosticAt)
                {
                    Debug.LogWarning(
                        $"[WorkerCollectorDiagnostics] Arm is still reaching: distance={reachDistance:F2}, "
                        + $"hand={(reachReference != null ? reachReference.position.ToString("F2") : "null")}, "
                        + $"solverTarget={(armSolverTarget != null ? armSolverTarget.position.ToString("F2") : "null")}, "
                        + $"pickupWorkerPlane={pickupPoint:F2}. Continuing until the cube is secured or lost.",
                        this);
                    nextArmReachDiagnosticAt = Time.time + armReachDiagnosticInterval;
                }
                break;

            case Command.MovingToDropOff:
                if (robotBody != null && robotBody.HasArrivedAtDestination())
                {
                    robotBody.StopMovement();
                    command = Command.None;
                    Observe(
                        WorkerCollectorBodyObservation.DropOffApproach(currentAssignment, commandToken));
                }
                break;

            case Command.WaitingForGarage:
                if (currentAssignment.DropOff.CanAcceptDelivery)
                    Observe(WorkerCollectorBodyObservation.GarageAvailability(
                        currentAssignment, commandToken, true));
                break;

            case Command.MovingToRest:
                if (robotBody != null && robotBody.HasArrivedAtDestination())
                {
                    robotBody.StopMovement();
                    command = Command.None;
                    Observe(
                        WorkerCollectorBodyObservation.RestApproach(currentAssignment, commandToken));
                }
                break;
        }
    }

    private void LateUpdate()
    {
        if (armReachController != null)
            return;

        if (armSolverTarget == null || !armRestPoseCached)
            return;

        if (command == Command.ReachingForCube
            && currentAssignment != null
            && currentAssignment.Target != null)
        {
            Vector2 pickupPoint = GetPickupPoint(currentAssignment);
            AdvanceArmSolverTarget(pickupPoint, Time.deltaTime);
            return;
        }

        if (holdArmPose)
            return;

        armSolverTarget.localPosition = Vector3.MoveTowards(
            armSolverTarget.localPosition,
            armRestLocalPosition,
            armReturnSpeed * Time.deltaTime);
        armSolverTarget.localRotation = Quaternion.RotateTowards(
            armSolverTarget.localRotation,
            armRestLocalRotation,
            armReturnSpeed * 90f * Time.deltaTime);
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

    /// <summary>
    /// Travels to the source's stable work waypoint before observing nearby cargo.
    /// </summary>
    public void BeginMoveToSource(WorkerCollectorMissionAssignment assignment) {
        if (!PrepareCommand(assignment, requireTarget: false))
            return;
        armReachController?.ClearTarget();
        holdArmPose = false;
        command = Command.MovingToSource;
        RoomWaypoint waypoint = assignment.Source.ApproachWaypoint;
        if (robotBody != null && waypoint != null)
            robotBody.SetDestination(waypoint, waypoint.WorldPos, pickupArrivalThreshold,
                includeUnavailable: true, replaceTargetWaypoint: true);
    }

    /// <summary>
    /// Observes local cargo while stationary; acquisition enters Memory before a grab task is chosen.
    /// </summary>
    public void AcquireCube(WorkerCollectorMissionAssignment assignment) {
        if (!PrepareCommand(assignment, requireTarget: false))
            return;
        robotBody?.StopMovement();
        command = Command.AcquiringCube;
        ScanForCube();
    }

    private void ScanForCube() {
        CancelInvoke(nameof(ScanForCube));
        if (command != Command.AcquiringCube || currentAssignment == null)
            return;
        if (!WorkerCollectorMissionService.RequestTarget(this, currentAssignment) && isActiveAndEnabled)
            Invoke(nameof(ScanForCube), cubeScanInterval);
    }

    public void BeginMoveToCube(WorkerCollectorMissionAssignment assignment)
    {
        if (!PrepareCommand(assignment))
            return;
        armReachController?.ClearTarget();
        holdArmPose = false;
        command = Command.MovingToCube;
        RoomWaypoint waypoint = assignment.Source.ApproachWaypoint;
        if (robotBody != null && waypoint != null)
        {
            Vector3 workerApproachPosition = CalculateBodyApproachPosition(assignment);
            robotBody.SetDestination(
                waypoint,
                workerApproachPosition,
                new Vector2(
                    Mathf.Max(0.01f, pickupArrivalThreshold.x),
                    Mathf.Max(0.01f, pickupArrivalThreshold.y)),
                includeUnavailable: true,
                replaceTargetWaypoint: true);
        }
    }

    public void GrabCube(WorkerCollectorMissionAssignment assignment)
    {
        if (!PrepareCommand(assignment))
            return;

        if (armReachController == null && armSolverTarget == null)
        {
            TrySecureReachedCube(DistanceFromCarryAnchor(GetPickupPoint(assignment)));
            return;
        }

        robotBody?.StopMovement();
        if (armReachController == null)
            FaceRightForPickup();
        armReachController?.SetPickupTarget(assignment.Target.transform);
        command = Command.ReachingForCube;
        nextArmReachDiagnosticAt = Time.time + armReachDiagnosticInterval;
        holdArmPose = false;
        Debug.Log(
            "[WorkerCollectorDiagnostics] WorkerCollectorGrabCube started; continuously reaching until secured. "
            + $"solverTarget={(armSolverTarget != null ? armSolverTarget.position.ToString("F2") : "null")}, "
            + $"effector={(armEffector != null ? armEffector.position.ToString("F2") : "null")}, "
            + $"pickupWorkerPlane={GetPickupPoint(assignment):F2}, speed={armReachSpeed:F2}.",
            this);
    }

    public void BeginMoveToDropOff(WorkerCollectorMissionAssignment assignment)
    {
        if (!PrepareCommand(assignment))
            return;
        command = Command.MovingToDropOff;
        RoomWaypoint waypoint = assignment.DropOff.ApproachWaypoint;
        if (robotBody != null && waypoint != null)
            robotBody.SetDestination(waypoint, assignment.DropOff.DeliveryPosition, includeUnavailable: true);
    }

    public void WaitAtDropOff(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment == null || !ReferenceEquals(currentAssignment, assignment))
            return;
        command = Command.WaitingAtDropOff;
        RoomWaypoint waypoint = assignment.DropOff != null ? assignment.DropOff.ApproachWaypoint : null;
        if (robotBody != null && waypoint != null)
            robotBody.SetDestination(waypoint, assignment.DropOff.WaitPosition, includeUnavailable: true);
        else
            robotBody?.StopMovement();
    }

    /// <summary>
    /// Holds carried cargo until garage availability is observed and Memory causes replanning.
    /// </summary>
    public void WaitForGarage(WorkerCollectorMissionAssignment assignment) {
        if (!PrepareCommand(assignment))
            return;
        robotBody?.StopMovement();
        command = Command.WaitingForGarage;
    }

    public void DepositCube(WorkerCollectorMissionAssignment assignment)
    {
        if (!PrepareCommand(assignment) || assignment.DropOff == null)
            return;

        UnsubscribeTarget();
        if (!assignment.DropOff.TryAccept(this, assignment, out bool waitingForBatch))
        {
            subscribedTarget = assignment.Target;
            if (subscribedTarget != null)
                subscribedTarget.OnClaimLost += HandleClaimLost;
            Observe(WorkerCollectorBodyObservation.GarageAvailability(assignment, commandToken, false));
            return;
        }

        RestoreCargoCollisions();
        securedCarryAnchor = null;
        holdArmPose = false;
        armReachController?.ClearTarget();
        command = waitingForBatch ? Command.WaitingAtDropOff : Command.None;
        Observe(
            WorkerCollectorBodyObservation.Delivery(assignment, commandToken, waitingForBatch));
    }

    public void BeginMoveToRest(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment == null || assignment.Rest == null)
        {
            ReportRestCompletedImmediately(assignment);
            return;
        }
        ResolveReferences();
        currentAssignment = assignment;
        commandToken++;
        command = Command.MovingToRest;
        RoomWaypoint waypoint = assignment.Rest.RestWaypoint;
        if (robotBody != null && waypoint != null)
            robotBody.SetDestination(waypoint, assignment.Rest.RestPosition, includeUnavailable: true);
    }

    public void Rest(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment == null || !ReferenceEquals(currentAssignment, assignment))
            return;
        robotBody?.StopMovement();
        command = Command.Resting;
        CancelInvoke(nameof(CompleteRest));
        float duration = assignment.Rest != null ? assignment.Rest.RestDuration : 0f;
        if (duration <= 0f)
            CompleteRest();
        else
            Invoke(nameof(CompleteRest), duration);
    }

    public void ReportBatchCompleted(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment == null || !ReferenceEquals(currentAssignment, assignment))
            return;
        commandToken++;
        Observe(
            WorkerCollectorBodyObservation.BatchProcessed(assignment, commandToken));
    }

    public void ReportBatchInterrupted(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment == null || !ReferenceEquals(currentAssignment, assignment))
            return;
        commandToken++;
        Observe(
            WorkerCollectorBodyObservation.BatchInterrupted(assignment, commandToken));
    }

    public void ReportDestinationUnavailable()
    {
        WorkerCollectorMissionAssignment assignment = currentAssignment;
        if (assignment == null)
            return;
        ReleaseCurrentCargo();
        StopAllActuators();
        currentAssignment = null;
        memory?.TryClearWorkerCollectorMission(assignment);
    }

    public void CancelCurrentCommand(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment != null && !ReferenceEquals(currentAssignment, assignment))
            return;
        robotBody?.StopMovement();
        CancelInvoke(nameof(FindCube));
        CancelInvoke(nameof(ScanForCube));
        CancelInvoke(nameof(CompleteRest));
        command = Command.None;
        if (!IsCarryingCurrentTarget())
        {
            holdArmPose = false;
            armReachController?.ClearTarget();
        }
    }

    public void StopAllActuators()
    {
        robotBody?.StopMovement();
        CancelInvoke(nameof(FindCube));
        CancelInvoke(nameof(ScanForCube));
        CancelInvoke(nameof(CompleteRest));
        command = Command.None;
        if (!IsCarryingCurrentTarget())
        {
            holdArmPose = false;
            armReachController?.ClearTarget();
        }
    }

    private bool PrepareCommand(WorkerCollectorMissionAssignment assignment, bool requireTarget = true)
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

        if (requireTarget && !IsTargetValid())
        {
            ReportTargetLost();
            return false;
        }
        return true;
    }

    private bool IsTargetValid() => currentAssignment != null
        && currentAssignment.Target != null
            && currentAssignment.Target.IsClaimValid(currentAssignment.Claim);

    private float DistanceFromCarryAnchor(Vector2 position)
    {
        Transform anchor = EffectiveCarryAnchor;
        Vector2 origin = anchor != null ? ToWorkerPlane(anchor.position) : ToWorkerPlane(transform.position);
        return Vector2.Distance(origin, position);
    }

    private Transform EffectiveCarryAnchor {
        get {
            if (securedCarryAnchor != null)
                return securedCarryAnchor;
            if (armReachController != null && armReachController.ActiveArm.HasValue) {
                Transform puppetHand = armReachController.ActiveArm.Value == CowboyArmSide.Left
                    ? leftPuppetHand
                    : rightPuppetHand;
                if (puppetHand != null)
                    return puppetHand;
            }
            return carryAnchor != null ? carryAnchor : rightPuppetHand;
        }
    }

    private bool IsCarryingCurrentTarget() => currentAssignment != null
        && currentAssignment.Target != null
        && currentAssignment.Target.State == WhiteCubeCargoState.Carried
        && currentAssignment.Target.transform.parent == EffectiveCarryAnchor;

    private void TrySecureReachedCube(float reachDistance)
    {
        WorkerCollectorMissionAssignment assignment = currentAssignment;
        WhiteCubeCargo target = assignment != null ? assignment.Target : null;
        Transform anchor = EffectiveCarryAnchor;
        if (target == null
            || anchor == null
            || assignment == null
            || !target.IsClaimValid(assignment.Claim))
        {
            Debug.LogWarning(
                $"[WorkerCollectorDiagnostics] Grab cannot continue: target={(target != null)}, "
                + $"anchor={(anchor != null)}, claimValid="
                + $"{(target != null && assignment != null && target.IsClaimValid(assignment.Claim))}.",
                this);
            ReportTargetLost();
            return;
        }

        if (reachDistance > pickupDistance)
            return;

        if (!target.TryBeginCarry(assignment.Claim, anchor))
        {
            if (!target.IsClaimValid(assignment.Claim))
            {
                ReportTargetLost();
                return;
            }

            command = Command.ReachingForCube;
            return;
        }

        command = Command.None;
        securedCarryAnchor = anchor;
        holdArmPose = true;
        armReachController?.HoldCurrentPose();
        IgnoreCargoCollisions(target);
        Transform grabbedHand = GetActiveReachEffector();
        Debug.Log(
            $"[WorkerCollectorDiagnostics] Arm grab succeeded: distance={reachDistance:F2}, "
            + $"hand={(grabbedHand != null ? grabbedHand.position.ToString("F2") : "null")}, "
            + $"cube={target.transform.position:F2}.",
            this);
        Observe(
            WorkerCollectorBodyObservation.Cargo(assignment, commandToken, secured: true));
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
        ReleaseCurrentCargo();
        holdArmPose = false;
        armReachController?.ClearTarget();
        currentAssignment = null;
        commandToken++;
        Observe(
            WorkerCollectorBodyObservation.TargetLost(lost, commandToken));
    }

    private void UnsubscribeTarget()
    {
        if (subscribedTarget != null)
            subscribedTarget.OnClaimLost -= HandleClaimLost;
        subscribedTarget = null;
    }

    private void ReleaseCurrentCargo() {
        UnsubscribeTarget();
        if (currentAssignment != null && currentAssignment.Target != null
            && currentAssignment.Target.Pickup != null
            && currentAssignment.Target.transform.parent == EffectiveCarryAnchor)
            currentAssignment.Target.Pickup.OnRelease(Vector2.zero);
        currentAssignment?.Target?.ReleaseClaim(currentAssignment.Claim);
        currentAssignment?.DropOff?.ReleaseReservation(currentAssignment.Reservation);
        RestoreCargoCollisions();
        securedCarryAnchor = null;
    }

    private void Observe(WorkerCollectorBodyObservation observation) {
        memory?.TryApplyWorkerCollectorObservation(observation);
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
        if (armSolverTarget == null)
            armSolverTarget = FindDescendant("RArm_Solver_Target");
        if (armEffector == null)
            armEffector = FindDescendant("RHand_Effector");
        if (leftPuppetHand == null)
            leftPuppetHand = FindPhysicalPuppetBone("LHand_Bone");
        if (rightPuppetHand == null)
            rightPuppetHand = FindPhysicalPuppetBone("RHand_Bone");
        if (armReachController == null)
            armReachController = GetComponent<RobotObjectArmReachController>();
        CacheArmRestPose();
        if (robotBody == null)
            robotBody = GetComponent<RobotBodyController>();
        if (brain == null)
            brain = GetComponent<RobotBrainNew>();
        if (memory == null)
            memory = GetComponent<RobotMemoryNew>();
    }

    private Vector2 GetPickupPoint(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment == null || assignment.Source == null || assignment.Target == null)
            return Vector2.zero;
        return assignment.Source.GetWorkerPlanePickupPoint(assignment.Target);
    }

    private float CalculateReachDistance(WorkerCollectorMissionAssignment assignment)
    {
        Transform reachReference = GetActiveReachEffector();
        return reachReference != null
            ? Vector2.Distance(ToWorkerPlane(reachReference.position), GetPickupPoint(assignment))
            : float.PositiveInfinity;
    }

    private Transform GetActiveReachEffector()
    {
        if (armReachController != null && armReachController.ActiveHandEffector != null)
            return armReachController.ActiveHandEffector;
        return armEffector != null ? armEffector : armSolverTarget;
    }

    private void AdvanceArmSolverTarget(Vector2 pickupPoint, float deltaTime)
    {
        if (armSolverTarget == null)
            return;
        Vector2 solverPoint = ToWorkerPlane(armSolverTarget.position);
        Vector2 nextPoint = Vector2.MoveTowards(
            solverPoint, pickupPoint, armReachSpeed * Mathf.Max(0f, deltaTime));
        Vector3 nextPosition = armSolverTarget.position;
        nextPosition.x = nextPoint.x;
        nextPosition.y = nextPoint.y;
        armSolverTarget.position = nextPosition;
    }

    private Vector3 CalculateBodyApproachPosition(WorkerCollectorMissionAssignment assignment)
    {
        Transform navigationBody = robotBody != null && robotBody.BodyReference != null
            ? robotBody.BodyReference
            : transform;
        Vector2 bodyPoint = ToWorkerPlane(navigationBody.position);
        Vector2 pickupPoint = GetPickupPoint(assignment);

        if (armReachController != null)
        {
            Vector2 approachDirection = pickupPoint - bodyPoint;
            if (approachDirection.sqrMagnitude <= Mathf.Epsilon)
                approachDirection = Vector2.right;
            Vector2 radialDestination = pickupPoint
                - approachDirection.normalized * armReachController.ReachRadius;
            return new Vector3(
                radialDestination.x,
                radialDestination.y,
                navigationBody.position.z);
        }

        Vector2 solverPoint = armSolverTarget != null
            ? ToWorkerPlane(armSolverTarget.position)
            : bodyPoint;
        Vector2 restTargetOffset = solverPoint - bodyPoint;
        Vector2 desiredRestTarget = pickupPoint + Vector2.left * armReachStartDistance;
        Vector2 destination = desiredRestTarget - restTargetOffset;
        return new Vector3(destination.x, destination.y, navigationBody.position.z);
    }

    private bool ValidatePickupApproach()
    {
        Transform navigationBody = robotBody != null && robotBody.BodyReference != null
            ? robotBody.BodyReference
            : transform;
        Vector2 pickupPoint = GetPickupPoint(currentAssignment);
        Vector2 bodyPoint = ToWorkerPlane(navigationBody.position);
        Vector2 solverPoint = armSolverTarget != null
            ? ToWorkerPlane(armSolverTarget.position)
            : ToWorkerPlane(EffectiveCarryAnchor != null
                ? EffectiveCarryAnchor.position
                : navigationBody.position);
        Vector2 effectorPoint = armEffector != null
            ? ToWorkerPlane(armEffector.position)
            : solverPoint;
        float startDistance = Vector2.Distance(solverPoint, pickupPoint);
        bool cubeIsRightOfWorker = pickupPoint.x > bodyPoint.x;
        float bodyDistance = Vector2.Distance(bodyPoint, pickupPoint);
        bool validDistance;
        if (armReachController != null)
        {
            float orbitTolerance = Mathf.Max(pickupDistance, 0.3f);
            validDistance = Mathf.Abs(bodyDistance - armReachController.ReachRadius)
                <= orbitTolerance;
        }
        else
        {
            validDistance = startDistance > pickupDistance
                && startDistance >= minimumArmReachStartDistance
                && startDistance <= maximumArmReachStartDistance;
        }

        Debug.Log(
            "[WorkerCollectorDiagnostics] Body reached cube approach; "
            + $"cubeWorld={currentAssignment.Target.transform.position:F2}, "
            + $"cubeLocal={currentAssignment.Target.transform.localPosition:F2}, "
            + $"pickupWorkerPlane={pickupPoint:F2}, bodyWorkerPlane={bodyPoint:F2}, "
            + $"solverTargetWorkerPlane={solverPoint:F2}, effectorWorkerPlane={effectorPoint:F2}, "
            + $"cubeIsRightOfWorker={cubeIsRightOfWorker}, startDistance={startDistance:F2}, "
            + $"bodyDistance={bodyDistance:F2}, orbitRadius="
            + $"{(armReachController != null ? armReachController.ReachRadius : 0f):F2}.",
            this);

        if ((armReachController != null || cubeIsRightOfWorker) && validDistance)
        {
            if (armReachController == null)
                FaceRightForPickup();
            return true;
        }

        Debug.LogWarning(
            "[WorkerCollectorDiagnostics] Unsafe cube approach; releasing the mission for a recoverable retry. "
            + $"cubeIsRightOfWorker={cubeIsRightOfWorker}, startDistance={startDistance:F2}, "
            + $"bodyDistance={bodyDistance:F2}, expectedOrbitRadius="
            + $"{(armReachController != null ? armReachController.ReachRadius : 0f):F2}.",
            this);
        return false;
    }

    private void FaceRightForPickup()
    {
        Animator rigAnimator = GetComponentInChildren<Animator>(true);
        if (rigAnimator != null)
            rigAnimator.SetFloat("Direction", 1f);

        FacingController facing = GetComponentInChildren<FacingController>(true);
        if (facing != null)
        {
            facing.SetArmFacing(true);
            facing.SetLegFacing(true);
        }

        if (armSolverTarget == null || armSolverTarget.parent == null)
            return;
        Behaviour[] solvers = armSolverTarget.parent.GetComponents<Behaviour>();
        for (int i = 0; i < solvers.Length; i++)
        {
            Behaviour solver = solvers[i];
            if (solver == null || solver.GetType().Name != "LimbSolver2D")
                continue;
            solver.enabled = true;
            FieldInfo flipField = solver.GetType().GetField(
                "m_Flip", BindingFlags.Instance | BindingFlags.NonPublic);
            flipField?.SetValue(solver, false);
            break;
        }
    }

    private static Vector2 ToWorkerPlane(Vector3 worldPosition) =>
        new Vector2(worldPosition.x, worldPosition.y);

    private void Reset() => ResolveReferences();

    private Transform FindDescendant(string objectName)
    {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            if (descendants[i] != null && descendants[i].name == objectName)
                return descendants[i];
        }
        return null;
    }

    private Transform FindPhysicalPuppetBone(string objectName) {
        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++) {
            Transform descendant = descendants[i];
            if (descendant != null
                && descendant.name == objectName
                && descendant.GetComponent<Rigidbody2D>() != null)
                return descendant;
        }
        return null;
    }

    private void CacheArmRestPose()
    {
        if (armRestPoseCached || armSolverTarget == null)
            return;
        armRestLocalPosition = armSolverTarget.localPosition;
        armRestLocalRotation = armSolverTarget.localRotation;
        armRestPoseCached = true;
    }

    private void RestoreArmPoseImmediate()
    {
        if (!armRestPoseCached || armSolverTarget == null)
            return;
        armSolverTarget.localPosition = armRestLocalPosition;
        armSolverTarget.localRotation = armRestLocalRotation;
    }

    private void CompleteRest()
    {
        WorkerCollectorMissionAssignment assignment = currentAssignment;
        if (assignment == null)
            return;
        command = Command.None;
        commandToken++;
        Observe(
            WorkerCollectorBodyObservation.RestFinished(assignment, commandToken));
    }

    private void ReportRestCompletedImmediately(WorkerCollectorMissionAssignment assignment)
    {
        if (assignment == null)
            return;
        currentAssignment = assignment;
        commandToken++;
        Observe(
            WorkerCollectorBodyObservation.RestApproach(assignment, commandToken));
    }
}
