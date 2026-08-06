using UnityEngine;

/// <summary>
/// Rotates the Collector's visual-only propeller in proportion to flight thrust.
/// </summary>
[DisallowMultipleComponent]
public sealed class CollectorFlightVisuals : MonoBehaviour {
    [SerializeField] private Transform propellerPivot;
    [SerializeField] private CollectorFlightMotor2D flightMotor;
    [SerializeField, Min(0f)] private float minimumHoverSpeed = 360f;
    [SerializeField, Min(0f)] private float maximumSpinSpeed = 1200f;
    [SerializeField, Min(0f)] private float spinAcceleration = 2400f;

    private Quaternion authoredLocalRotation;
    private float currentSpinSpeed;
    private bool initialized;
    private bool flightActive;

    public bool IsFlightActive => flightActive;
    public float CurrentSpinSpeed => currentSpinSpeed;

    private void Awake() {
        EnsureInitialized();
    }

    private void Update() {
        StepVisual(Time.deltaTime);
    }

    private void OnDisable() {
        ResetVisual();
    }

    /// <summary>
    /// Wires the authored visual pivot and the read-only thrust source.
    /// This is editor-safe and may be called by the prefab builder.
    /// </summary>
    public void ConfigureReferences(Transform pivot, CollectorFlightMotor2D motor) {
        propellerPivot = pivot;
        flightMotor = motor;
        initialized = false;
        EnsureInitialized();
    }

    /// <summary>
    /// Enables or ramps down propeller feedback without affecting physics.
    /// </summary>
    public void SetFlightActive(bool active) {
        flightActive = active && isActiveAndEnabled;
    }

    /// <summary>
    /// Executes one deterministic visual step for Edit Mode tests and normal Update.
    /// </summary>
    public void StepVisual(float deltaTime) {
        EnsureInitialized();
        if (propellerPivot == null)
            return;

        float normalizedThrust = flightMotor != null
            ? flightMotor.NormalizedThrust
            : 0f;
        float targetSpinSpeed = flightActive
            ? Mathf.Lerp(
                minimumHoverSpeed,
                maximumSpinSpeed,
                Mathf.Clamp01(normalizedThrust))
            : 0f;

        currentSpinSpeed = Mathf.MoveTowards(
            currentSpinSpeed,
            targetSpinSpeed,
            spinAcceleration * Mathf.Max(0f, deltaTime));

        if (Mathf.Approximately(currentSpinSpeed, 0f))
            return;

        propellerPivot.localRotation *= Quaternion.Euler(
            0f,
            0f,
            currentSpinSpeed * Mathf.Max(0f, deltaTime));
    }

    /// <summary>
    /// Immediately stops and restores the authored propeller pose for death or pooling.
    /// </summary>
    public void ResetVisual() {
        EnsureInitialized();
        flightActive = false;
        currentSpinSpeed = 0f;
        if (propellerPivot != null)
            propellerPivot.localRotation = authoredLocalRotation;
    }

    private void EnsureInitialized() {
        if (initialized)
            return;

        if (propellerPivot != null)
            authoredLocalRotation = propellerPivot.localRotation;
        initialized = true;
    }
}
