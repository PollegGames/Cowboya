using System;
using System.Collections.Generic;
using CowBoya.Robots;
using UnityEngine;

/// <summary>
/// Physical Body facade used by Collector tasks. It translates one selected task into
/// force flight, local sensing, cargo operation, magnet aim, and discrete observations.
/// It never chooses the next mission task.
/// </summary>
[DisallowMultipleComponent]
public sealed class CollectorRobotBodyController : MonoBehaviour, ICollectorTaskBody, IPooledObject {
    [Header("Authored Puppet")]
    [SerializeField] private Rigidbody2D bodyRigidbody;
    [SerializeField] private Rigidbody2D magnetRigidbody;
    [SerializeField] private Transform masterMagnet;
    [SerializeField] private HingeJoint2D magnetHinge;
    [SerializeField] private SimplePuppetBinder puppetBinder;

    [Header("Physical Components")]
    [SerializeField] private CollectorFlightMotor2D flightMotor;
    [SerializeField] private CollectorObstacleSensor2D obstacleSensor;
    [SerializeField] private CollectorMagnetController2D magnetController;
    [SerializeField] private CollectorFlightVisuals flightVisuals;

    [Header("Flight Profiles")]
    [SerializeField]
    private CollectorFlightProfile launchProfile =
        new CollectorFlightProfile(2.2f, 4f, 4f, 16f, 55f);
    [SerializeField]
    private CollectorFlightProfile outboundProfile =
        new CollectorFlightProfile(2f, 4f, 5f, 16f, 55f);
    [SerializeField]
    private CollectorFlightProfile loadedProfile =
        new CollectorFlightProfile(1.5f, 5f, 3.25f, 10f, 45f);
    [SerializeField]
    private CollectorFlightProfile dockingProfile =
        new CollectorFlightProfile(1.5f, 6f, 2.5f, 9f, 40f);

    [Header("Targets and Arrival")]
    [SerializeField] private Vector2 targetHoverOffset = new Vector2(0f, 0.75f);
    [SerializeField] private Vector2 gatherHoverOffset = new Vector2(0f, 0.6f);
    [SerializeField, Min(0.01f)] private float launchArrivalRadius = 0.6f;
    [SerializeField, Min(0.01f)] private float targetArrivalRadius = 0.7f;
    [SerializeField, Min(0.01f)] private float dockApproachRadius = 0.6f;
    [SerializeField, Min(0.01f)] private float intakeArrivalRadius = 0.25f;
    [SerializeField, Min(0.01f)] private float arrivalSpeedLimit = 1f;
    [SerializeField, Min(0f)] private float arrivalSettleTime = 0.35f;
    [SerializeField, Min(0f)] private float dockingSettleTime = 0.5f;

    [Header("Bounded Stall Recovery")]
    [SerializeField, Min(0.1f)] private float stallTimeout = 3f;
    [SerializeField, Min(0.01f)] private float minimumProgress = 0.15f;
    [SerializeField, Min(0)] private int maximumRecoveryAttempts = 2;
    [SerializeField, Min(0.1f)] private float recoveryDuration = 1f;
    [SerializeField, Min(0.1f)] private float recoveryOffsetDistance = 1.2f;

    [Header("Magnet Aim")]
    [SerializeField] private Vector2 magnetAimLocalAxis = Vector2.down;
    [SerializeField, Min(0f)] private float magnetAimSharpness = 10f;

    private readonly List<CollectorBodyObservation> pendingObservations =
        new List<CollectorBodyObservation>(4);

    private CollectorMissionAssignment currentAssignment;
    private Func<Vector2?> baseTargetProvider;
    private Func<Vector2?> magnetAimTargetProvider;
    private Func<int, CollectorBodyObservation> arrivalObservationFactory;
    private int commandToken;
    private float arrivalSettledTime;
    private bool arrivalPublished;
    private bool requireArrivalSpeedSettled;
    private float arrivalRadius;
    private float requiredArrivalSettleTime;
    private float bestDistance = float.PositiveInfinity;
    private float stallElapsed;
    private float recoveryRemaining;
    private int recoveryAttempts;
    private float recoveryBaselineDistance = float.PositiveInfinity;
    private Vector2 recoveryTarget;
    private bool hasRecoveryTarget;
    private bool flightFaultPublished;
    private bool forceBudgetWarningPublished;
    private bool suppressCargoObservations;
    private bool hasGatherHoldTarget;
    private Vector2 gatherHoldTarget;
    private bool initialized;
    private bool restPoseCached;
    private Vector3 bodyRestLocalPosition;
    private Quaternion bodyRestLocalRotation;
    private Vector3 magnetRestLocalPosition;
    private Quaternion magnetRestLocalRotation;
    private Quaternion masterMagnetRestLocalRotation;

    public event Action<CollectorBodyObservation> OnObservation;
    public event Action<CollectorMissionAssignment> OnAssignmentChanged;

    public CollectorMissionAssignment CurrentAssignment => currentAssignment;
    public int CurrentCommandToken => commandToken;
    public bool HasActiveCommand => currentAssignment != null && flightMotor != null && flightMotor.IsFlightActive;
    public Rigidbody2D BodyRigidbody => bodyRigidbody;
    public Rigidbody2D MagnetRigidbody => magnetRigidbody;
    public bool IsStallRecoveryActive => hasRecoveryTarget;
    public int StallRecoveryAttemptCount => recoveryAttempts;
    public Vector2 StallRecoveryTarget => recoveryTarget;
    public string LastFlightFaultReason { get; private set; }

    /// <summary>
    /// Returns whether an observation belongs to the exact active assignment and command epoch.
    /// Delayed callbacks from a replaced task or earlier pooled use therefore cannot publish.
    /// </summary>
    public bool IsObservationCurrent(CollectorBodyObservation observation) {
        return observation.Assignment != null
            && ReferenceEquals(observation.Assignment, currentAssignment)
            && observation.CommandToken == commandToken;
    }

    private void Awake() {
        EnsureInitialized();
    }

    private void OnEnable() {
        EnsureInitialized();
        SubscribeToMagnet();
    }

    private void Update() {
        StepAim(Time.deltaTime);
    }

    private void FixedUpdate() {
        StepPhysics(Time.fixedDeltaTime);
    }

    private void OnDisable() {
        StopAllActuators();
        UnsubscribeFromMagnet();
    }

    /// <summary>
    /// Wires all authored Rigidbody, binder, motor, sensor, cargo, and visual references.
    /// This is editor-safe and is the supported prefab-builder seam.
    /// </summary>
    public void ConfigureReferences(
        Rigidbody2D body,
        Rigidbody2D magnet,
        Transform masterMagnetTransform,
        HingeJoint2D hinge,
        SimplePuppetBinder binder,
        CollectorFlightMotor2D motor,
        CollectorObstacleSensor2D sensor,
        CollectorMagnetController2D cargoController,
        CollectorFlightVisuals visuals) {
        UnsubscribeFromMagnet();

        bodyRigidbody = body;
        magnetRigidbody = magnet;
        masterMagnet = masterMagnetTransform;
        magnetHinge = hinge;
        puppetBinder = binder;
        flightMotor = motor;
        obstacleSensor = sensor;
        magnetController = cargoController;
        flightVisuals = visuals;

        initialized = false;
        restPoseCached = false;
        EnsureInitialized();
    }

    /// <summary>
    /// Starts physical travel from the machine to its live launch-exit marker.
    /// </summary>
    public void BeginLaunch(CollectorMissionAssignment assignment) {
        BeginFlightCommand(
            assignment,
            GetLaunchTarget,
            launchProfile,
            launchArrivalRadius,
            arrivalSettleTime,
            requireSpeedSettled: true,
            token => CollectorBodyObservation.LaunchExit(assignment, token));
        StopCargoAcquisition();
    }

    /// <summary>
    /// Starts direct force flight toward the assigned corpse's live centre.
    /// </summary>
    public void BeginOutbound(CollectorMissionAssignment assignment) {
        BeginFlightCommand(
            assignment,
            GetOutboundTarget,
            outboundProfile,
            targetArrivalRadius,
            arrivalSettleTime,
            requireSpeedSettled: false,
            token => CollectorBodyObservation.TargetApproach(assignment, token));
        StopCargoAcquisition();
        magnetAimTargetProvider = GetOutboundTarget;
    }

    /// <summary>
    /// Holds at the reached collection position and enables magnetic acquisition.
    /// The aim continues tracking unsecured parts, but the flight target stays fixed so
    /// lifted cargo cannot pull the Collector upward through target feedback.
    /// </summary>
    public void BeginGathering(CollectorMissionAssignment assignment) {
        BeginFlightCommand(
            assignment,
            GetGatherTarget,
            loadedProfile,
            targetArrivalRadius,
            arrivalSettleTime,
            requireSpeedSettled: true,
            observationFactory: null);

        CaptureGatherHoldTarget();
        magnetAimTargetProvider = GetGatherAimTarget;
        if (magnetController != null
            && magnetController.isActiveAndEnabled
            && IsValidTargetAssignment(assignment)) {
            magnetController.BeginGathering(assignment);
        } else {
            QueueFlightFault("gather_magnet_or_target_unavailable");
        }
    }

    /// <summary>
    /// Carries secured cargo toward the machine's live dock-approach marker.
    /// </summary>
    public void BeginReturn(CollectorMissionAssignment assignment) {
        BeginFlightCommand(
            assignment,
            GetDockApproachTarget,
            loadedProfile,
            dockApproachRadius,
            dockingSettleTime,
            requireSpeedSettled: true,
            token => CollectorBodyObservation.DockApproach(assignment, token));

        if (magnetController != null
            && magnetController.isActiveAndEnabled
            && IsValidTargetAssignment(assignment)) {
            magnetController.BeginCarry(assignment);
        } else if (IsValidTargetAssignment(assignment)) {
            QueueFlightFault("return_magnet_unavailable");
        }
    }

    /// <summary>
    /// Synchronously releases owned cargo, stops acquisition, and returns to the dock empty.
    /// </summary>
    public void BeginAbortReturn(CollectorMissionAssignment assignment) {
        BeginFlightCommand(
            assignment,
            GetDockApproachTarget,
            outboundProfile,
            dockApproachRadius,
            dockingSettleTime,
            requireSpeedSettled: true,
            token => CollectorBodyObservation.DockApproach(assignment, token));

        if (magnetController != null) {
            suppressCargoObservations = true;
            magnetController.StopAcquisition();
            magnetController.ReleaseAll();
            suppressCargoObservations = false;
        }
    }

    /// <summary>
    /// Starts reduced-speed flight toward the machine's live intake marker.
    /// Intake acceptance itself remains owned by the machine trigger.
    /// </summary>
    public void BeginDocking(CollectorMissionAssignment assignment) {
        BeginFlightCommand(
            assignment,
            GetIntakeTarget,
            dockingProfile,
            intakeArrivalRadius,
            dockingSettleTime,
            requireSpeedSettled: true,
            observationFactory: null);

        if (magnetController != null
            && magnetController.isActiveAndEnabled
            && IsValidTargetAssignment(assignment)) {
            magnetController.BeginCarry(assignment);
        } else if (IsValidTargetAssignment(assignment)) {
            QueueFlightFault("dock_magnet_unavailable");
        }
    }

    /// <summary>
    /// Cancels only the matching task command. Existing magnetic cargo links remain owned
    /// so a Gather-to-Return-to-Dock replacement cannot drop secured cargo.
    /// </summary>
    public void CancelCurrentCommand(CollectorMissionAssignment assignment) {
        if (!ReferenceEquals(currentAssignment, assignment))
            return;

        InvalidateCommandToken();
        CancelPhysicalCommandKeepingCargo();
        SetCurrentAssignment(null);
    }

    /// <summary>
    /// Stops hover, movement, aim, visuals, acquisition, and every Collector-owned cargo link.
    /// With this method active the authored dynamic puppet falls normally.
    /// </summary>
    public void StopAllActuators() {
        InvalidateCommandToken();
        CancelPhysicalCommandKeepingCargo();

        if (magnetController != null) {
            suppressCargoObservations = true;
            magnetController.ReleaseAll();
            suppressCargoObservations = false;
        }

        SetCurrentAssignment(null);
        pendingObservations.Clear();
        obstacleSensor?.ClearMissionFilters();
        flightVisuals?.ResetVisual();
    }

    /// <summary>
    /// Restores the authored child-body poses and clears all velocities for safe pool reuse.
    /// </summary>
    public void ResetPhysicalState() {
        EnsureInitialized();
        StopAllActuators();

        RestoreRigidbodyPose(
            bodyRigidbody,
            bodyRestLocalPosition,
            bodyRestLocalRotation);
        RestoreRigidbodyPose(
            magnetRigidbody,
            magnetRestLocalPosition,
            magnetRestLocalRotation);

        RestoreMasterMagnetRestPose();
        obstacleSensor?.RefreshSelfColliders();
    }

    /// <summary>
    /// Executes one deterministic flight, cargo, arrival, recovery, and observation step.
    /// </summary>
    public void StepPhysics(float deltaTime) {
        EnsureInitialized();
        float safeDeltaTime = Mathf.Max(0f, deltaTime);

        if (currentAssignment != null
            && baseTargetProvider != null
            && (flightMotor == null || !flightMotor.isActiveAndEnabled)) {
            QueueFlightFault("flight_motor_inactive");
        }

        flightMotor?.StepPhysics(safeDeltaTime);
        if (magnetController != null && magnetController.isActiveAndEnabled)
            magnetController.StepPhysics(safeDeltaTime);

        // The motor reserves gravity support before maneuvering. A configured force
        // budget below that support force means reduced steering authority, not a dead
        // motor: it still applies the support force. Aborting on the first physics tick
        // made otherwise operational Collectors immediately return to the machine.
        if (flightMotor != null
            && flightMotor.ForceBudgetInsufficient
            && !forceBudgetWarningPublished) {
            forceBudgetWarningPublished = true;
            Debug.LogWarning(
                $"Collector '{name}' hover support exceeds its configured maneuver force budget; "
                + "flight will continue with reduced steering authority.",
                this);
        }

        if (flightVisuals != null && flightMotor != null)
            flightVisuals.SetFlightActive(flightMotor.IsFlightActive);

        StepStallRecovery(safeDeltaTime);
        StepArrival(safeDeltaTime);
        FlushPendingObservations();
    }

    /// <summary>
    /// Executes one deterministic master-magnet aiming step before the binder's LateUpdate.
    /// </summary>
    public void StepAim(float deltaTime) {
        EnsureInitialized();
        if (masterMagnet == null || magnetRigidbody == null || magnetAimTargetProvider == null)
            return;

        Vector2? target = magnetAimTargetProvider.Invoke();
        if (!target.HasValue)
            return;

        Vector2 direction = target.Value - magnetRigidbody.position;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Vector2 localAxis = magnetAimLocalAxis.sqrMagnitude > 0.0001f
            ? magnetAimLocalAxis.normalized
            : Vector2.down;
        float localAxisAngle = Mathf.Atan2(localAxis.y, localAxis.x) * Mathf.Rad2Deg;
        float desiredPuppetAngle =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - localAxisAngle;

        if (magnetHinge != null && magnetHinge.useLimits) {
            float currentJointAngle = magnetHinge.jointAngle;
            float requestedDelta = Mathf.DeltaAngle(magnetRigidbody.rotation, desiredPuppetAngle);
            float requestedJointAngle = currentJointAngle + requestedDelta;
            JointAngleLimits2D limits = magnetHinge.limits;
            float clampedJointAngle = Mathf.Clamp(
                requestedJointAngle,
                limits.min,
                limits.max);
            desiredPuppetAngle = magnetRigidbody.rotation
                + clampedJointAngle
                - currentJointAngle;
        }

        Quaternion masterRootRotation = puppetBinder != null && puppetBinder.MasterRoot != null
            ? puppetBinder.MasterRoot.rotation
            : GetParentRotation(masterMagnet);
        Quaternion puppetRootRotation = puppetBinder != null && puppetBinder.PuppetRoot != null
            ? puppetBinder.PuppetRoot.rotation
            : transform.rotation;
        Quaternion desiredPuppetRotation = Quaternion.Euler(0f, 0f, desiredPuppetAngle);
        Quaternion desiredMasterRotation = masterRootRotation
            * Quaternion.Inverse(puppetRootRotation)
            * desiredPuppetRotation;

        float blend = magnetAimSharpness > 0f
            ? 1f - Mathf.Exp(-magnetAimSharpness * Mathf.Max(0f, deltaTime))
            : 1f;
        masterMagnet.rotation = Quaternion.Slerp(
            masterMagnet.rotation,
            desiredMasterRotation,
            blend);
    }

    public void OnAcquireFromPool() {
        ResetPhysicalState();
    }

    public void OnReleaseToPool() {
        ResetPhysicalState();
    }

    private void BeginFlightCommand(
        CollectorMissionAssignment assignment,
        Func<Vector2?> targetProvider,
        CollectorFlightProfile profile,
        float commandArrivalRadius,
        float commandSettleTime,
        bool requireSpeedSettled,
        Func<int, CollectorBodyObservation> observationFactory) {
        EnsureInitialized();
        InvalidateCommandToken();
        CancelPhysicalCommandKeepingCargo();

        SetCurrentAssignment(assignment);
        baseTargetProvider = targetProvider;
        arrivalRadius = Mathf.Max(0.01f, commandArrivalRadius);
        requiredArrivalSettleTime = Mathf.Max(0f, commandSettleTime);
        requireArrivalSpeedSettled = requireSpeedSettled;
        arrivalObservationFactory = observationFactory;
        ResetCommandProgress();

        obstacleSensor?.SetAssignedTarget(assignment != null ? assignment.Target : null);
        if (obstacleSensor != null) {
            obstacleSensor.SetOwnedBodyPredicate(
                body => magnetController != null && magnetController.Owns(body));
        }

        string startFailure = GetFlightStartFailure(assignment);
        if (startFailure == null) {
            flightMotor.StartFlight(GetAdjustedFlightTarget, profile);
            flightVisuals?.SetFlightActive(true);
        } else {
            QueueFlightFault(startFailure);
        }
    }

    private void CancelPhysicalCommandKeepingCargo() {
        flightMotor?.StopFlight();
        flightVisuals?.SetFlightActive(false);
        magnetController?.StopAcquisition();
        baseTargetProvider = null;
        magnetAimTargetProvider = null;
        arrivalObservationFactory = null;
        hasGatherHoldTarget = false;
        gatherHoldTarget = default;
        ResetCommandProgress();
        RestoreMasterMagnetRestPose();
        pendingObservations.Clear();
    }

    private void ResetCommandProgress() {
        arrivalSettledTime = 0f;
        arrivalPublished = false;
        bestDistance = float.PositiveInfinity;
        stallElapsed = 0f;
        recoveryRemaining = 0f;
        recoveryAttempts = 0;
        recoveryBaselineDistance = float.PositiveInfinity;
        recoveryTarget = Vector2.zero;
        hasRecoveryTarget = false;
        flightFaultPublished = false;
        forceBudgetWarningPublished = false;
    }

    private void StepArrival(float deltaTime) {
        if (arrivalPublished
            || arrivalObservationFactory == null
            || bodyRigidbody == null
            || !TryGetBaseTarget(out Vector2 target)) {
            return;
        }

        bool insideRadius = Vector2.Distance(bodyRigidbody.position, target) <= arrivalRadius;
        bool speedSettled = !requireArrivalSpeedSettled
            || bodyRigidbody.linearVelocity.magnitude <= arrivalSpeedLimit;
        if (!insideRadius || !speedSettled) {
            arrivalSettledTime = 0f;
            return;
        }

        arrivalSettledTime += deltaTime;
        if (arrivalSettledTime < requiredArrivalSettleTime)
            return;

        arrivalPublished = true;
        QueueObservation(arrivalObservationFactory.Invoke(commandToken));
    }

    private void StepStallRecovery(float deltaTime) {
        if (flightFaultPublished
            || flightMotor == null
            || !flightMotor.IsFlightActive
            || bodyRigidbody == null) {
            return;
        }

        if (!TryGetBaseTarget(out Vector2 target)) {
            stallElapsed += deltaTime;
            if (stallElapsed >= stallTimeout)
                QueueFlightFault("live_target_unavailable_timeout");
            return;
        }

        float distance = Vector2.Distance(bodyRigidbody.position, target);
        if (distance <= Mathf.Max(arrivalRadius * 1.5f, 0.2f)) {
            ResetStallRecovery(distance);
            return;
        }

        if (hasRecoveryTarget) {
            recoveryRemaining -= deltaTime;
            float recoveryArrivalRadius = Mathf.Max(arrivalRadius, 0.2f);
            bool reachedRecoveryTarget = Vector2.Distance(
                bodyRigidbody.position,
                recoveryTarget) <= recoveryArrivalRadius;
            if (reachedRecoveryTarget || recoveryRemaining <= 0f) {
                hasRecoveryTarget = false;
                recoveryRemaining = 0f;
                bestDistance = distance;
                stallElapsed = 0f;
                Debug.Log(
                    $"[CollectorStallRecovery] robot={name} "
                    + $"attempt={recoveryAttempts}/{maximumRecoveryAttempts} "
                    + "state=retry_original_route",
                    this);
            }
            return;
        }

        if (float.IsPositiveInfinity(bestDistance)
            || distance <= bestDistance - minimumProgress) {
            bestDistance = distance;
            stallElapsed = 0f;
            if (recoveryAttempts > 0
                && distance <= recoveryBaselineDistance - minimumProgress) {
                recoveryAttempts = 0;
                recoveryBaselineDistance = float.PositiveInfinity;
            }
            return;
        }

        stallElapsed += deltaTime;
        if (stallElapsed < stallTimeout)
            return;

        if (recoveryAttempts < maximumRecoveryAttempts) {
            StartStallRecovery(target, distance);
            return;
        }

        QueueFlightFault("stalled_after_recovery");
    }

    private Vector2? GetAdjustedFlightTarget() {
        if (!TryGetBaseTarget(out Vector2 target))
            return null;

        return hasRecoveryTarget ? recoveryTarget : target;
    }

    private void StartStallRecovery(Vector2 routeTarget, float routeDistance) {
        if (bodyRigidbody == null)
            return;

        if (recoveryAttempts == 0 || float.IsPositiveInfinity(recoveryBaselineDistance))
            recoveryBaselineDistance = routeDistance;

        Vector2 escapeDirection = (recoveryAttempts & 1) == 0
            ? Vector2.right
            : Vector2.left;
        Vector2 recoveryOrigin = bodyRigidbody.position;
        recoveryTarget = recoveryOrigin
            + escapeDirection * Mathf.Max(0.1f, recoveryOffsetDistance);
        recoveryAttempts++;
        recoveryRemaining = Mathf.Max(0.1f, recoveryDuration);
        hasRecoveryTarget = true;
        stallElapsed = 0f;
        bestDistance = routeDistance;

        Debug.Log(
            $"[CollectorStallRecovery] robot={name} "
            + $"attempt={recoveryAttempts}/{maximumRecoveryAttempts} "
            + $"direction={(escapeDirection.x > 0f ? "right" : "left")} "
            + $"origin={recoveryOrigin} escapeTarget={recoveryTarget} "
            + $"routeTarget={routeTarget}",
            this);
    }

    private void ResetStallRecovery(float routeDistance) {
        stallElapsed = 0f;
        bestDistance = routeDistance;
        recoveryRemaining = 0f;
        recoveryAttempts = 0;
        recoveryBaselineDistance = float.PositiveInfinity;
        recoveryTarget = Vector2.zero;
        hasRecoveryTarget = false;
    }

    private bool TryGetBaseTarget(out Vector2 target) {
        target = default;
        if (baseTargetProvider == null)
            return false;

        Vector2? sampledTarget = baseTargetProvider.Invoke();
        if (!sampledTarget.HasValue)
            return false;

        target = sampledTarget.Value;
        return IsFinite(target.x) && IsFinite(target.y);
    }

    private Vector2? GetLaunchTarget() {
        return currentAssignment != null && currentAssignment.Home != null
            ? currentAssignment.Home.GetLaunchExitPosition()
            : null;
    }

    private Vector2? GetOutboundTarget() {
        if (!IsValidTargetAssignment(currentAssignment))
            return null;
        return currentAssignment.Target.GetLiveCollectionCenter(currentAssignment.Claim)
            + targetHoverOffset;
    }

    private Vector2? GetGatherTarget() {
        if (!IsValidTargetAssignment(currentAssignment))
            return null;

        Vector2? aimTarget = GetGatherAimTarget();
        if (!hasGatherHoldTarget)
            return aimTarget;
        if (!aimTarget.HasValue)
            return gatherHoldTarget;

        // Keep the safe hover height captured on arrival, but travel horizontally
        // toward the remaining unsecured parts. Holding both axes let a final limb
        // remain pinned along the floor while the magnet pulled from too far away;
        // following Y instead caused already lifted cargo to drag the Collector up.
        return new Vector2(aimTarget.Value.x, gatherHoldTarget.y);
    }

    private Vector2? GetGatherAimTarget() {
        if (!IsValidTargetAssignment(currentAssignment))
            return null;

        if (magnetController != null
            && magnetController.TryGetUnsecuredCenter(currentAssignment, out Vector2 center)) {
            return center + gatherHoverOffset;
        }

        return currentAssignment.Target.GetLiveCollectionCenter(currentAssignment.Claim)
            + gatherHoverOffset;
    }

    private void CaptureGatherHoldTarget() {
        if (bodyRigidbody != null) {
            gatherHoldTarget = bodyRigidbody.position;
            hasGatherHoldTarget = true;
            return;
        }

        Vector2? fallback = GetGatherAimTarget();
        hasGatherHoldTarget = fallback.HasValue;
        gatherHoldTarget = fallback.GetValueOrDefault();
    }

    private Vector2? GetDockApproachTarget() {
        return currentAssignment != null && currentAssignment.Home != null
            ? currentAssignment.Home.GetDockApproachPosition()
            : null;
    }

    private Vector2? GetIntakeTarget() {
        return currentAssignment != null && currentAssignment.Home != null
            ? currentAssignment.Home.GetIntakePosition()
            : null;
    }

    private void StopCargoAcquisition() {
        magnetController?.StopAcquisition();
    }

    private void HandleCargoStatusChanged(CollectorCargoStatus status) {
        if (suppressCargoObservations
            || !ReferenceEquals(status.Assignment, currentAssignment)
            || currentAssignment == null) {
            return;
        }

        QueueObservation(CollectorBodyObservation.Cargo(
            currentAssignment,
            commandToken,
            status.RequiredPartCount,
            status.SecuredPartCount,
            status.CargoSecure,
            status.CargoLost));
    }

    private string GetFlightStartFailure(CollectorMissionAssignment assignment) {
        if (assignment == null)
            return "assignment_missing";
        if (assignment.Home == null)
            return "home_machine_missing";
        if (flightMotor == null)
            return "flight_motor_missing";
        // During SetActive(true), Heart can start Launch before Unity reaches the
        // motor in component-enable order. The motor stores that pending command and
        // activates it from its own OnEnable, so only an authored disabled component
        // is an actual start failure here.
        if (!flightMotor.enabled)
            return "flight_motor_disabled";
        if (bodyRigidbody == null)
            return "body_rigidbody_missing";
        if (!bodyRigidbody.simulated)
            return "body_rigidbody_not_simulated";
        return null;
    }

    private void QueueFlightFault(string reason) {
        if (flightFaultPublished || currentAssignment == null)
            return;

        flightFaultPublished = true;
        LastFlightFaultReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason;
        Debug.LogWarning(
            $"Collector '{name}' reported flight fault '{LastFlightFaultReason}' "
            + $"(command token {commandToken}).",
            this);
        QueueObservation(CollectorBodyObservation.FlightFault(
            currentAssignment,
            commandToken));
    }

    private void QueueObservation(CollectorBodyObservation observation) {
        if (!IsObservationCurrent(observation)) {
            return;
        }

        pendingObservations.Add(observation);
    }

    private void FlushPendingObservations() {
        while (pendingObservations.Count > 0) {
            CollectorBodyObservation observation = pendingObservations[0];
            pendingObservations.RemoveAt(0);

            if (!IsObservationCurrent(observation)) {
                continue;
            }

            OnObservation?.Invoke(observation);
        }
    }

    private void EnsureInitialized() {
        if (initialized)
            return;

        ResolveAuthoredReferences();

        flightMotor?.ConfigureReferences(bodyRigidbody, magnetRigidbody, obstacleSensor);
        obstacleSensor?.ConfigureReferences(transform);
        magnetController?.ConfigureReferences(bodyRigidbody, magnetRigidbody);
        flightVisuals?.ConfigureReferences(
            FindPropellerPivot(),
            flightMotor);

        SubscribeToMagnet();
        CacheRestPose();
        initialized = true;
    }

    private void ResolveAuthoredReferences() {
        if (puppetBinder == null)
            puppetBinder = GetComponent<SimplePuppetBinder>();
        if (magnetHinge == null)
            magnetHinge = GetComponentInChildren<HingeJoint2D>(true);
        if (magnetRigidbody == null && magnetHinge != null)
            magnetRigidbody = magnetHinge.GetComponent<Rigidbody2D>();
        if (bodyRigidbody == null && magnetHinge != null)
            bodyRigidbody = magnetHinge.connectedBody;

        if (masterMagnet == null && puppetBinder != null) {
            for (int i = 0; i < puppetBinder.Pairs.Count; i++) {
                SimplePuppetBinder.BonePair pair = puppetBinder.Pairs[i];
                if (pair == null || pair.Master == null)
                    continue;
                if (magnetRigidbody == null || pair.Puppet == magnetRigidbody.transform) {
                    masterMagnet = pair.Master;
                    if (magnetRigidbody != null)
                        break;
                }
            }
        }

        if (flightMotor == null)
            flightMotor = GetComponent<CollectorFlightMotor2D>();
        if (obstacleSensor == null)
            obstacleSensor = GetComponent<CollectorObstacleSensor2D>();
        if (magnetController == null)
            magnetController = GetComponent<CollectorMagnetController2D>();
        if (flightVisuals == null)
            flightVisuals = GetComponent<CollectorFlightVisuals>();
    }

    private Transform FindPropellerPivot() {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++) {
            if (children[i] != null && children[i].name == "PropellerPivot")
                return children[i];
        }
        return null;
    }

    private void SubscribeToMagnet() {
        if (magnetController == null)
            return;
        magnetController.CargoStatusChanged -= HandleCargoStatusChanged;
        magnetController.CargoStatusChanged += HandleCargoStatusChanged;
    }

    private void UnsubscribeFromMagnet() {
        if (magnetController != null)
            magnetController.CargoStatusChanged -= HandleCargoStatusChanged;
    }

    private void CacheRestPose() {
        if (restPoseCached)
            return;

        if (bodyRigidbody != null) {
            bodyRestLocalPosition = bodyRigidbody.transform.localPosition;
            bodyRestLocalRotation = bodyRigidbody.transform.localRotation;
        }
        if (magnetRigidbody != null) {
            magnetRestLocalPosition = magnetRigidbody.transform.localPosition;
            magnetRestLocalRotation = magnetRigidbody.transform.localRotation;
        }
        if (masterMagnet != null)
            masterMagnetRestLocalRotation = masterMagnet.localRotation;

        restPoseCached = true;
    }

    private void RestoreMasterMagnetRestPose() {
        if (restPoseCached && masterMagnet != null)
            masterMagnet.localRotation = masterMagnetRestLocalRotation;
    }

    private static void RestoreRigidbodyPose(
        Rigidbody2D body,
        Vector3 localPosition,
        Quaternion localRotation) {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.transform.localPosition = localPosition;
        body.transform.localRotation = localRotation;
        body.Sleep();
    }

    private void InvalidateCommandToken() {
        commandToken++;
        if (commandToken <= 0)
            commandToken = 1;
    }

    private void SetCurrentAssignment(CollectorMissionAssignment assignment) {
        if (ReferenceEquals(currentAssignment, assignment))
            return;

        currentAssignment = assignment;
        OnAssignmentChanged?.Invoke(currentAssignment);
    }

    private static bool IsValidTargetAssignment(CollectorMissionAssignment assignment) {
        return assignment != null
            && assignment.Target != null
            && assignment.Claim.IsValid
            && assignment.Target.IsClaimValid(assignment.Claim);
    }

    private static Quaternion GetParentRotation(Transform target) {
        return target != null && target.parent != null
            ? target.parent.rotation
            : Quaternion.identity;
    }

    private static bool IsFinite(float value) {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
