using System;
using CowBoya.Robots;
using UnityEngine;

/// <summary>
/// Root-level transaction boundary for Collector pooling. ObjectPool only invokes
/// root IPooledObject components, so this component resets every child subsystem.
/// </summary>
[DisallowMultipleComponent]
public sealed class CollectorPoolLifecycle : MonoBehaviour, IPooledObject {
    [SerializeField] private RobotMemoryNew memory;
    [SerializeField] private RobotBrainNew brain;
    [SerializeField] private RobotHeartNew heart;
    [SerializeField] private RobotStateController stateController;
    [SerializeField] private JointBreaker jointBreaker;
    [SerializeField] private SimplePuppetBinder puppetBinder;
    [SerializeField] private CollectorRobotBodyController bodyController;
    [SerializeField] private CollectorMagnetController2D magnetController;
    [SerializeField] private CollectorFlightVisuals flightVisuals;
    [SerializeField] private CollectorRobotObservationBridge observationBridge;

    private Transform[] cachedTransforms;
    private Vector3[] restLocalPositions;
    private Quaternion[] restLocalRotations;
    private Vector3[] restLocalScales;
    private Rigidbody2D[] cachedBodies;
    private RigidbodyDefaults[] rigidbodyDefaults;
    private bool releasing;
    private bool releaseFinalized;
    private bool initialized;
    private string releaseReason;

    public bool IsReleaseInProgress => releasing;
    public string ReleaseReason => releaseReason;

    [Serializable]
    private struct RigidbodyDefaults {
        public RigidbodyType2D BodyType;
        public bool Simulated;
        public float Mass;
        public float GravityScale;
        public float LinearDamping;
        public float AngularDamping;
        public RigidbodyConstraints2D Constraints;
        public RigidbodyInterpolation2D Interpolation;
        public CollisionDetectionMode2D CollisionDetection;
    }

    private void Awake() {
        EnsureInitialized();
    }

    /// <summary>
    /// Deterministically wires the root lifecycle. This method is safe for editor prefab builders.
    /// </summary>
    public void ConfigureReferences(RobotMemoryNew robotMemory, RobotBrainNew robotBrain,
        RobotHeartNew robotHeart, RobotStateController robotState, JointBreaker breaker,
        SimplePuppetBinder binder, CollectorRobotBodyController collectorBody,
        CollectorMagnetController2D collectorMagnet, CollectorFlightVisuals visuals,
        CollectorRobotObservationBridge bridge) {
        memory = robotMemory;
        brain = robotBrain;
        heart = robotHeart;
        stateController = robotState;
        jointBreaker = breaker;
        puppetBinder = binder;
        bodyController = collectorBody;
        magnetController = collectorMagnet;
        flightVisuals = visuals;
        observationBridge = bridge;
        initialized = false;
        EnsureInitialized();
    }

    /// <summary>
    /// Gates callbacks and planning while the still-active Collector is being finalized.
    /// Task Exit remains owned by Heart when the GameObject is deactivated.
    /// </summary>
    public void PrepareForPoolRelease(string reason) {
        EnsureInitialized();
        if (releasing)
            return;

        releasing = true;
        releaseFinalized = false;
        releaseReason = string.IsNullOrWhiteSpace(reason) ? "unspecified" : reason;
        brain?.SetPlanPublicationSuspended(true);
        if (observationBridge != null)
            observationBridge.enabled = false;
    }

    /// <summary>
    /// Restores an inactive Collector to an inert, mission-free acquisition state.
    /// </summary>
    public void OnAcquireFromPool() {
        EnsureInitialized();
        releasing = true;
        releaseFinalized = false;
        releaseReason = null;

        brain?.SetPlanPublicationSuspended(true);
        if (observationBridge != null)
            observationBridge.enabled = false;

        RestoreRequiredSubsystemState();
        magnetController?.ReleaseAll();
        bodyController?.ResetPhysicalState();
        RestoreAuthoredState();
        jointBreaker?.RestoreAll();
        puppetBinder?.ClearRotationTargets();
        if (puppetBinder != null)
            puppetBinder.enabled = true;
        flightVisuals?.ResetVisual();

        memory?.ResetAll(notify: false);
        brain?.ResetPlanningCache();
        heart?.ConfigureRole(RobotRole.Collector, resetStack: true);

        if (stateController != null) {
            RobotStats stats = new EnemyRobotFactory().CreateRobot();
            stats.RobotName = "Collector";
            stateController.Stats = stats;
        }

        if (observationBridge != null)
            observationBridge.enabled = true;
        brain?.SetPlanPublicationSuspended(false);
        releasing = false;
    }

    private void RestoreRequiredSubsystemState() {
        CollectorFlightMotor2D flightMotor = GetComponent<CollectorFlightMotor2D>();
        CollectorObstacleSensor2D obstacleSensor = GetComponent<CollectorObstacleSensor2D>();

        if (flightMotor != null)
            flightMotor.enabled = true;
        if (obstacleSensor != null)
            obstacleSensor.enabled = true;
        if (bodyController != null)
            bodyController.enabled = true;
        if (magnetController != null)
            magnetController.enabled = true;
        if (flightVisuals != null)
            flightVisuals.enabled = true;
    }

    /// <summary>
    /// Performs idempotent final cleanup after the supported inactive pool release sequence.
    /// </summary>
    public void OnReleaseToPool() {
        EnsureInitialized();
        if (releaseFinalized)
            return;

        if (!releasing)
            PrepareForPoolRelease("pool_release");

        CollectorMissionAssignment assignment = memory != null
            ? memory.Snapshot.Collector.Assignment
            : null;

        magnetController?.ReleaseAll();
        bodyController?.StopAllActuators();
        bodyController?.ResetPhysicalState();
        flightVisuals?.ResetVisual();
        puppetBinder?.ClearRotationTargets();

        if (assignment != null && assignment.Target != null
            && assignment.Target.IsClaimValid(assignment.Claim)) {
            assignment.Target.ReleaseClaim(assignment.Claim);
        }

        memory?.ResetAll(notify: false);
        brain?.ResetPlanningCache();
        heart?.ResetIntentStack(repopulateDefaultTask: false);
        RestoreAuthoredState();
        releaseFinalized = true;
    }

    private void EnsureInitialized() {
        if (initialized)
            return;

        if (memory == null)
            memory = GetComponent<RobotMemoryNew>();
        if (brain == null)
            brain = GetComponent<RobotBrainNew>();
        if (heart == null)
            heart = GetComponent<RobotHeartNew>();
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();
        if (jointBreaker == null)
            jointBreaker = GetComponent<JointBreaker>();
        if (puppetBinder == null)
            puppetBinder = GetComponent<SimplePuppetBinder>();
        if (bodyController == null)
            bodyController = GetComponent<CollectorRobotBodyController>();
        if (magnetController == null)
            magnetController = GetComponent<CollectorMagnetController2D>();
        if (flightVisuals == null)
            flightVisuals = GetComponent<CollectorFlightVisuals>();
        if (observationBridge == null)
            observationBridge = GetComponent<CollectorRobotObservationBridge>();

        CacheAuthoredState();
        initialized = true;
    }

    private void CacheAuthoredState() {
        cachedTransforms = GetComponentsInChildren<Transform>(true);
        restLocalPositions = new Vector3[cachedTransforms.Length];
        restLocalRotations = new Quaternion[cachedTransforms.Length];
        restLocalScales = new Vector3[cachedTransforms.Length];
        for (int i = 0; i < cachedTransforms.Length; i++) {
            Transform cachedTransform = cachedTransforms[i];
            restLocalPositions[i] = cachedTransform.localPosition;
            restLocalRotations[i] = cachedTransform.localRotation;
            restLocalScales[i] = cachedTransform.localScale;
        }

        cachedBodies = GetComponentsInChildren<Rigidbody2D>(true);
        rigidbodyDefaults = new RigidbodyDefaults[cachedBodies.Length];
        for (int i = 0; i < cachedBodies.Length; i++) {
            Rigidbody2D body = cachedBodies[i];
            rigidbodyDefaults[i] = new RigidbodyDefaults {
                BodyType = body.bodyType,
                Simulated = body.simulated,
                Mass = body.mass,
                GravityScale = body.gravityScale,
                LinearDamping = body.linearDamping,
                AngularDamping = body.angularDamping,
                Constraints = body.constraints,
                Interpolation = body.interpolation,
                CollisionDetection = body.collisionDetectionMode
            };
        }
    }

    private void RestoreAuthoredState() {
        if (cachedTransforms != null) {
            for (int i = 1; i < cachedTransforms.Length; i++) {
                Transform cachedTransform = cachedTransforms[i];
                if (cachedTransform == null)
                    continue;
                cachedTransform.localPosition = restLocalPositions[i];
                cachedTransform.localRotation = restLocalRotations[i];
                cachedTransform.localScale = restLocalScales[i];
            }
        }

        if (cachedBodies == null)
            return;
        for (int i = 0; i < cachedBodies.Length; i++) {
            Rigidbody2D body = cachedBodies[i];
            if (body == null)
                continue;

            RigidbodyDefaults defaults = rigidbodyDefaults[i];
            body.bodyType = defaults.BodyType;
            body.simulated = defaults.Simulated;
            body.mass = defaults.Mass;
            body.gravityScale = defaults.GravityScale;
            body.linearDamping = defaults.LinearDamping;
            body.angularDamping = defaults.AngularDamping;
            body.constraints = defaults.Constraints;
            body.interpolation = defaults.Interpolation;
            body.collisionDetectionMode = defaults.CollisionDetection;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.Sleep();
        }

        Physics2D.SyncTransforms();
    }
}
