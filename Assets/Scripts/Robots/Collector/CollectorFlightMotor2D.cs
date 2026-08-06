using System;
using UnityEngine;

/// <summary>
/// Serializable force-control settings for one Collector flight operation.
/// </summary>
[Serializable]
public sealed class CollectorFlightProfile {
    [SerializeField, Min(0f)] private float positionGain = 2f;
    [SerializeField, Min(0f)] private float velocityGain = 4f;
    [SerializeField, Min(0.01f)] private float maximumSpeed = 5f;
    [SerializeField, Min(0.01f)] private float maximumAcceleration = 16f;
    [SerializeField, Min(0.01f)] private float maximumForce = 55f;

    public CollectorFlightProfile() {
    }

    public CollectorFlightProfile(
        float positionGain,
        float velocityGain,
        float maximumSpeed,
        float maximumAcceleration,
        float maximumForce) {
        this.positionGain = Mathf.Max(0f, positionGain);
        this.velocityGain = Mathf.Max(0f, velocityGain);
        this.maximumSpeed = Mathf.Max(0.01f, maximumSpeed);
        this.maximumAcceleration = Mathf.Max(0.01f, maximumAcceleration);
        this.maximumForce = Mathf.Max(0.01f, maximumForce);
    }

    public float PositionGain => Mathf.Max(0f, positionGain);
    public float VelocityGain => Mathf.Max(0f, velocityGain);
    public float MaximumSpeed => Mathf.Max(0.01f, maximumSpeed);
    public float MaximumAcceleration => Mathf.Max(0.01f, maximumAcceleration);
    public float MaximumForce => Mathf.Max(0.01f, maximumForce);
}

/// <summary>
/// Applies capped hover and steering forces to the Collector body Rigidbody.
/// It never writes velocity, position, or angular state directly.
/// </summary>
[DisallowMultipleComponent]
public sealed class CollectorFlightMotor2D : MonoBehaviour {
    [Header("Physical References")]
    [SerializeField] private Rigidbody2D bodyRigidbody;
    [SerializeField] private Rigidbody2D magnetRigidbody;
    [SerializeField] private CollectorObstacleSensor2D obstacleSensor;

    private Func<Vector2?> liveTargetProvider;
    private CollectorFlightProfile activeProfile;
    private bool flightActive;

    public bool IsFlightActive => flightActive && isActiveAndEnabled;
    public bool HasLiveTarget { get; private set; }
    public bool ForceBudgetInsufficient { get; private set; }
    public Vector2 CurrentTarget { get; private set; }
    public Vector2 LastDesiredVelocity { get; private set; }
    public Vector2 LastRequestedAcceleration { get; private set; }
    public Vector2 LastGravityCompensationForce { get; private set; }
    public Vector2 LastManeuverForce { get; private set; }
    public Vector2 LastAppliedForce { get; private set; }
    public float NormalizedThrust { get; private set; }
    public Rigidbody2D BodyRigidbody => bodyRigidbody;

    private void OnEnable() {
        TryActivatePendingFlight();
    }

    private void OnDisable() {
        // Keep the command provider/profile while the pooled root is being activated.
        // Heart can enter CollectorLaunch earlier in the same SetActive(true) pass than
        // Unity enables this component. A real task exit calls StopFlight explicitly.
        flightActive = false;
        ResetTelemetry();
    }

    /// <summary>
    /// Wires the two authored puppet bodies and the optional local-avoidance sensor.
    /// This is editor-safe and may be called by the prefab builder.
    /// </summary>
    public void ConfigureReferences(
        Rigidbody2D body,
        Rigidbody2D magnet,
        CollectorObstacleSensor2D sensor) {
        bodyRigidbody = body;
        magnetRigidbody = magnet;
        obstacleSensor = sensor;
    }

    /// <summary>
    /// Starts force-controlled flight toward a live target provider.
    /// The provider is sampled on every deterministic physics step.
    /// </summary>
    public void StartFlight(Func<Vector2?> targetProvider, CollectorFlightProfile profile) {
        liveTargetProvider = targetProvider;
        activeProfile = profile;
        TryActivatePendingFlight();
        ResetTelemetry();
    }

    /// <summary>
    /// Stops applying all hover and steering forces.
    /// </summary>
    public void StopFlight() {
        flightActive = false;
        liveTargetProvider = null;
        activeProfile = null;
        ResetTelemetry();
    }

    /// <summary>
    /// Executes one deterministic force-control step. Edit Mode tests call this before
    /// advancing their PhysicsScene2D because Unity does not invoke FixedUpdate there.
    /// </summary>
    public void StepPhysics(float deltaTime) {
        _ = deltaTime;

        if (!IsFlightActive || bodyRigidbody == null || activeProfile == null) {
            ResetTelemetry();
            return;
        }

        Vector2 bodyPosition = bodyRigidbody.position;
        HasLiveTarget = TrySampleTarget(out Vector2 target);
        CurrentTarget = HasLiveTarget ? target : bodyPosition;

        Vector2 positionError = CurrentTarget - bodyPosition;
        Vector2 desiredVelocity = Vector2.ClampMagnitude(
            positionError * activeProfile.PositionGain,
            activeProfile.MaximumSpeed);

        if (HasLiveTarget && obstacleSensor != null) {
            Vector2 avoidanceVelocity = obstacleSensor.SampleAvoidance(
                bodyPosition,
                bodyRigidbody.linearVelocity,
                CurrentTarget);
            desiredVelocity = Vector2.ClampMagnitude(
                desiredVelocity + avoidanceVelocity,
                activeProfile.MaximumSpeed);
        }

        Vector2 requestedAcceleration = Vector2.ClampMagnitude(
            (desiredVelocity - bodyRigidbody.linearVelocity) * activeProfile.VelocityGain,
            activeProfile.MaximumAcceleration);

        float supportedMass = CalculateSupportedMass();
        float weightedGravityMass = CalculateWeightedGravityMass();
        Vector2 gravityCompensation = -Physics2D.gravity * weightedGravityMass;

        // Gravity support is reserved first. Using the conservative remainder keeps the
        // combined force within the configured budget without sacrificing hover to steering.
        float maneuverBudget = Mathf.Max(
            0f,
            activeProfile.MaximumForce - gravityCompensation.magnitude);
        Vector2 maneuverForce = Vector2.ClampMagnitude(
            requestedAcceleration * supportedMass,
            maneuverBudget);
        Vector2 appliedForce = gravityCompensation + maneuverForce;

        ForceBudgetInsufficient = gravityCompensation.magnitude > activeProfile.MaximumForce;
        bodyRigidbody.AddForce(appliedForce, ForceMode2D.Force);

        LastDesiredVelocity = desiredVelocity;
        LastRequestedAcceleration = requestedAcceleration;
        LastGravityCompensationForce = gravityCompensation;
        LastManeuverForce = maneuverForce;
        LastAppliedForce = appliedForce;
        NormalizedThrust = Mathf.Clamp01(
            appliedForce.magnitude / activeProfile.MaximumForce);
    }

    /// <summary>
    /// Samples the active live target without changing physical state.
    /// </summary>
    public bool TryGetLiveTarget(out Vector2 target) {
        if (!IsFlightActive) {
            target = default;
            return false;
        }

        return TrySampleTarget(out target);
    }

    private bool TrySampleTarget(out Vector2 target) {
        target = default;
        if (liveTargetProvider == null)
            return false;

        Vector2? sampledTarget = liveTargetProvider.Invoke();
        if (!sampledTarget.HasValue)
            return false;

        Vector2 value = sampledTarget.Value;
        if (!IsFinite(value.x) || !IsFinite(value.y))
            return false;

        target = value;
        return true;
    }

    private void TryActivatePendingFlight() {
        flightActive = isActiveAndEnabled
            && bodyRigidbody != null
            && activeProfile != null
            && liveTargetProvider != null;
    }

    private float CalculateSupportedMass() {
        float mass = GetSimulatedMass(bodyRigidbody);
        if (magnetRigidbody != null && magnetRigidbody != bodyRigidbody)
            mass += GetSimulatedMass(magnetRigidbody);
        return Mathf.Max(0.0001f, mass);
    }

    private float CalculateWeightedGravityMass() {
        float mass = GetWeightedGravityMass(bodyRigidbody);
        if (magnetRigidbody != null && magnetRigidbody != bodyRigidbody)
            mass += GetWeightedGravityMass(magnetRigidbody);
        return Mathf.Max(0f, mass);
    }

    private static float GetSimulatedMass(Rigidbody2D body) {
        return body != null && body.simulated ? Mathf.Max(0f, body.mass) : 0f;
    }

    private static float GetWeightedGravityMass(Rigidbody2D body) {
        if (body == null || !body.simulated)
            return 0f;
        return Mathf.Max(0f, body.mass) * Mathf.Max(0f, body.gravityScale);
    }

    private void ResetTelemetry() {
        HasLiveTarget = false;
        ForceBudgetInsufficient = false;
        CurrentTarget = default;
        LastDesiredVelocity = default;
        LastRequestedAcceleration = default;
        LastGravityCompensationForce = default;
        LastManeuverForce = default;
        LastAppliedForce = default;
        NormalizedThrust = 0f;
    }

    private static bool IsFinite(float value) {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
