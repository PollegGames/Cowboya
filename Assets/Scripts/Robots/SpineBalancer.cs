using UnityEngine;

/// <summary>
/// Keeps targetHead aligned on an axis from groundProbe with optional forward/back lean.
/// Move = SmoothDamp in FixedUpdate so it cooperates with physics-driven IK.
/// </summary>
public sealed class SpineBalancer : MonoBehaviour
{
 
      [Header("Refs")]
    [SerializeField] Transform targetHead;
    
    [SerializeField] Transform hips;          // main body/root
    [SerializeField] Rigidbody2D hipsRb;      // for angular velocity
    [SerializeField] Transform groundProbe;

    [Header("Motion")]
    [SerializeField] float smoothTime = 0.12f; // slower target motion = less chatter
    [SerializeField] float maxSpeed = 50f;

    [Header("Upright Reference")]
    [SerializeField] bool useGroundNormal = true;
    [SerializeField] float groundRayLen = 2f;
    [SerializeField] LayerMask groundMask = ~0;

    [Header("External Lean (deg)")]
    [SerializeField] float externalLeanDeg = 0f;

    [Header("Auto-Balance PD")]
    [SerializeField] bool autoBalance = true;
    [SerializeField] float kp = 0.45f;  // lower than before
    [SerializeField] float kd = 0.35f;  // higher damping

    [Header("Stability Filters")]
    [SerializeField] float deadZoneDeg = 2f;          // ignore tiny errors
    [SerializeField] float tiltLpTau = 0.15f;         // low-pass time constant (s)
    [SerializeField] float maxAutoLean = 12f;         // clamp magnitude (deg)
    [SerializeField] float maxLeanRateDegPerSec = 180f; // clamp rate (deg/s)
    float _spineLen;
    Vector3 _vel;

    // filtered signals
    float _tiltFiltDeg;
    float _tiltFiltPrevDeg;
    float _autoLeanOutDeg; // rate-limited output

    void Awake()
    {
        if (!targetHead || !groundProbe || !hips) { enabled = false; Debug.LogError("[SpineBalancer2D] Missing refs"); return; }
        _spineLen = Vector3.Distance(groundProbe.position, targetHead.position);
        if (_spineLen <= Mathf.Epsilon) _spineLen = 1f;

        // init filtered tilt
        float initTilt = MeasureTiltDeg(GetUpRef());
        _tiltFiltDeg = _tiltFiltPrevDeg = initTilt;
        _autoLeanOutDeg = 0f;
    }

    void FixedUpdate()
    {
        Vector2 upRef = GetUpRef();

        // 1) Measure and filter tilt
        float dt = Time.fixedDeltaTime;
        float rawTilt = MeasureTiltDeg(upRef);
        float alpha = 1f - Mathf.Exp(-dt / Mathf.Max(tiltLpTau, 1e-4f)); // 0..1
        _tiltFiltDeg = Mathf.Lerp(_tiltFiltDeg, rawTilt, alpha);

        // 2) Dead-zone
        float tiltForCtrl = Mathf.Abs(_tiltFiltDeg) < deadZoneDeg ? 0f : _tiltFiltDeg;

        // 3) Derivative (from filtered signal)
        float tiltVelDeg = (_tiltFiltDeg - _tiltFiltPrevDeg) / dt;
        _tiltFiltPrevDeg = _tiltFiltDeg;
        if (Mathf.Abs(tiltVelDeg) < deadZoneDeg * 2f) tiltVelDeg = 0f;

        // 4) PD command
        float autoCmdDeg = autoBalance ? (-kp * tiltForCtrl) + (-kd * tiltVelDeg) : 0f;
        autoCmdDeg = Mathf.Clamp(autoCmdDeg, -maxAutoLean, +maxAutoLean);

        // 5) Rate limit the auto-lean output
        float maxStep = maxLeanRateDegPerSec * dt;
        float step = Mathf.Clamp(autoCmdDeg - _autoLeanOutDeg, -maxStep, +maxStep);
        _autoLeanOutDeg += step;

        // 6) Sum with external lean
        float totalLeanDeg = Mathf.Clamp(externalLeanDeg + _autoLeanOutDeg, -80f, 80f);

        // 7) Drive target along upright axis
        Vector2 dir = Quaternion.AngleAxis(totalLeanDeg, Vector3.forward) * upRef.normalized;
        Vector3 desired = groundProbe.position + (Vector3)(dir.normalized * _spineLen);

        targetHead.position = Vector3.SmoothDamp(
            targetHead.position, desired, ref _vel, smoothTime, maxSpeed, dt
        );
    }

    Vector2 GetUpRef()
    {
        if (!useGroundNormal) return Vector2.up;
        RaycastHit2D hit = Physics2D.Raycast(groundProbe.position, Vector2.down, groundRayLen, groundMask);
        return hit.collider ? hit.normal : Vector2.up;
    }

    float MeasureTiltDeg(Vector2 upRef)
    {
        // Positive when hips lean toward +X relative to reference up.
        return Vector2.SignedAngle(hips.up, upRef);
    }

    // External API
    public void SetExternalLean(float deg) => externalLeanDeg = deg;
    public void AddExternalLean(float ddeg) => externalLeanDeg += ddeg;
    public void ResetExternalLean() => externalLeanDeg = 0f;
    public void RecomputeSpineLength()
    {
        _spineLen = Vector3.Distance(groundProbe.position, targetHead.position);
        if (_spineLen <= Mathf.Epsilon) _spineLen = 1f;
    }

    Vector2 GetUpReference2D()
    {
        if (!useGroundNormal) return Vector2.up;

        // Raycast from probe downward to get surface normal
        Vector2 origin = groundProbe.position;
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, groundRayLen, groundMask);
        if (hit.collider) return hit.normal;  // normal is “up” from ground
        return Vector2.up; // fallback
    }

    float ComputeAutoLeanDeg(Vector2 upRef)
    {
        // Tilt angle of hips vs upRef. Positive if hips lean to +X.
        Vector2 hipsUp = hips.up; // world-space
        float tiltDeg = SignedAngle2D(hipsUp, upRef);

        // PD: command a lean that reduces tilt and damps angular velocity
        float angVelDeg = hipsRb ? hipsRb.angularVelocity : 0f; // deg/sec in 2D
        float cmd = (-kp * tiltDeg) + (-kd * angVelDeg);

        return Mathf.Clamp(cmd, -maxAutoLean, +maxAutoLean);
    }

    static float SignedAngle2D(Vector2 from, Vector2 to)
    {
        float ang = Vector2.SignedAngle(from, to); // [-180,180], +CCW around +Z
        // We want positive when leaning to +X. In side-scrollers, that convention matches SignedAngle.
        return ang;
    }

}