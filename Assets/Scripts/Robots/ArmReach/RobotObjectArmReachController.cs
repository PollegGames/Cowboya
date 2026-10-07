using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Drives one of two robot IK arm targets toward an object-selected world position.
/// </summary>
[DisallowMultipleComponent]
public class RobotObjectArmReachController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bodyReference;
    [Tooltip("Arm aiming preview. During collection, the grab task supplies the claimed cube from Memory automatically.")]
    [SerializeField] private Transform target;
    [SerializeField] private Transform leftArmSolverTarget;
    [SerializeField] private Transform rightArmSolverTarget;
    [SerializeField] private Behaviour leftArmIkSolver;
    [SerializeField] private Behaviour rightArmIkSolver;
    [SerializeField] private Transform leftHandEffector;
    [SerializeField] private Transform rightHandEffector;

    [Header("Motion")]
    [Min(0.01f)]
    [SerializeField] private float followSpeed = 15f;
    [Min(0.01f)]
    [SerializeField] private float returnSpeed = 10f;
    [Min(0.01f)]
    [SerializeField] private float rotationReturnSpeed = 720f;
    [Min(0.01f)]
    [Tooltip("World-space radius of the aim orbit and maximum distance for an object pickup target.")]
    [SerializeField] private float maximumReach = 3f;
    [Min(0f)]
    [Tooltip("Center band in which the previously selected arm remains active.")]
    [SerializeField] private float sideSwitchThreshold = 0.1f;

    [Header("Reach Visualization")]
    [SerializeField] private bool showReachGizmos = true;
    [SerializeField] private bool drawGizmosOnlyWhenSelected;
    [Range(8, 96)]
    [SerializeField] private int arcSegments = 32;
    [SerializeField] private Color leftArcColor = new Color(0.2f, 0.65f, 1f, 0.9f);
    [SerializeField] private Color rightArcColor = new Color(1f, 0.45f, 0.15f, 0.9f);
    [SerializeField] private Color targetLineColor = new Color(1f, 1f, 0.2f, 0.9f);
    [SerializeField] private Color sideSwitchColor = new Color(1f, 1f, 1f, 0.45f);

    private Vector3 leftRestLocalPosition;
    private Vector3 rightRestLocalPosition;
    private Quaternion leftRestLocalRotation;
    private Quaternion rightRestLocalRotation;
    private bool leftRestCaptured;
    private bool rightRestCaptured;
    private bool leftSolverDefaultEnabled = true;
    private bool rightSolverDefaultEnabled = true;
    private bool solverDefaultsCaptured;
    private CowboyArmSide preferredArm = CowboyArmSide.Right;
    private CowboyArmSide? activeArm;
    private Vector2 lastTargetDirection = Vector2.right;
    private bool hasTargetDirection;
    private bool followTargetDistance;
    private bool holdPose;
    private readonly Dictionary<Behaviour, Action<bool>> solverFlipSetters =
        new Dictionary<Behaviour, Action<bool>>();

    public Transform Target => target;
    public float ReachRadius => maximumReach;
    public CowboyArmSide? ActiveArm => activeArm;
    public Transform ActiveHandEffector => activeArm == CowboyArmSide.Left
        ? leftHandEffector
        : activeArm == CowboyArmSide.Right ? rightHandEffector : null;

    private void Awake()
    {
        ResolveReferences();
        CaptureRestPose();
        CaptureSolverDefaults();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureRestPose();
        CaptureSolverDefaults();
        activeArm = null;
    }

    private void OnDisable()
    {
        activeArm = null;
        holdPose = false;
        hasTargetDirection = false;
        RestoreRestPoseImmediate();
        RestoreSolverDefaults();
    }

    private void LateUpdate()
    {
        Tick(Time.deltaTime);
    }

    private void OnDrawGizmos()
    {
        if (showReachGizmos && !drawGizmosOnlyWhenSelected)
            DrawReachGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (showReachGizmos && drawGizmosOnlyWhenSelected)
            DrawReachGizmos();
    }

    /// <summary>
    /// Changes the world object followed by the robot arms.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        followTargetDistance = false;
        holdPose = false;
    }

    /// <summary>
    /// Reaches the object's actual position, limited by the maximum reach radius.
    /// </summary>
    public void SetPickupTarget(Transform newTarget) {
        target = newTarget;
        followTargetDistance = true;
        holdPose = false;
    }

    /// <summary>
    /// Keeps the selected arm pose relative to the robot after an object is attached.
    /// </summary>
    public void HoldCurrentPose() {
        target = null;
        holdPose = activeArm.HasValue;
    }

    /// <summary>
    /// Stops reaching; both arm targets return to their captured rest poses.
    /// </summary>
    public void ClearTarget()
    {
        target = null;
        activeArm = null;
        holdPose = false;
    }

    /// <summary>
    /// Configures the core transforms. Prefabs may alternatively assign them in the Inspector.
    /// </summary>
    public void Configure(
        Transform body,
        Transform newTarget,
        Transform leftSolverTarget,
        Transform rightSolverTarget)
    {
        bodyReference = body;
        target = newTarget;
        followTargetDistance = false;
        holdPose = false;
        leftArmSolverTarget = leftSolverTarget;
        rightArmSolverTarget = rightSolverTarget;
        leftArmIkSolver = ResolveIkSolver(leftArmSolverTarget);
        rightArmIkSolver = ResolveIkSolver(rightArmSolverTarget);
        leftRestCaptured = false;
        rightRestCaptured = false;
        solverDefaultsCaptured = false;
        CaptureRestPose();
        CaptureSolverDefaults();
    }

    /// <summary>
    /// Advances the reach controller by a deterministic time step.
    /// </summary>
    public void Tick(float deltaTime)
    {
        ResolveReferences();
        CaptureRestPose();
        CaptureSolverDefaults();

        if (holdPose && activeArm.HasValue) {
            if (activeArm.Value == CowboyArmSide.Left) {
                ReturnToRest(rightArmSolverTarget, rightRestLocalPosition,
                    rightRestLocalRotation, rightRestCaptured, deltaTime);
            }
            else {
                ReturnToRest(leftArmSolverTarget, leftRestLocalPosition,
                    leftRestLocalRotation, leftRestCaptured, deltaTime);
            }
            RestoreSolverDefaults();
            return;
        }

        if (target == null || bodyReference == null)
        {
            activeArm = null;
            ReturnBothArms(deltaTime);
            RestoreSolverDefaults();
            return;
        }

        float horizontalDelta = target.position.x - bodyReference.position.x;
        preferredArm = SelectArm(horizontalDelta, sideSwitchThreshold, preferredArm);
        activeArm = ResolveAvailableArm(preferredArm, leftArmSolverTarget, rightArmSolverTarget);

        if (!activeArm.HasValue)
        {
            ReturnBothArms(deltaTime);
            RestoreSolverDefaults();
            return;
        }

        Vector3 destination = ResolveRadialTargetPosition();
        ApplySolverFlip(destination.x >= bodyReference.position.x);

        if (activeArm.Value == CowboyArmSide.Left)
        {
            FollowTarget(leftArmSolverTarget, destination, deltaTime);
            ReturnToRest(
                rightArmSolverTarget,
                rightRestLocalPosition,
                rightRestLocalRotation,
                rightRestCaptured,
                deltaTime);
        }
        else
        {
            FollowTarget(rightArmSolverTarget, destination, deltaTime);
            ReturnToRest(
                leftArmSolverTarget,
                leftRestLocalPosition,
                leftRestLocalRotation,
                leftRestCaptured,
                deltaTime);
        }

        RestoreSolverDefaults();
    }

    /// <summary>
    /// Selects an arm with hysteresis around the body's vertical center line.
    /// </summary>
    public static CowboyArmSide SelectArm(
        float horizontalDelta,
        float threshold,
        CowboyArmSide previousArm)
    {
        float safeThreshold = Mathf.Max(0f, threshold);
        if (horizontalDelta > safeThreshold)
            return CowboyArmSide.Right;
        if (horizontalDelta < -safeThreshold)
            return CowboyArmSide.Left;
        return previousArm;
    }

    /// <summary>
    /// Projects the direction to a target onto a fixed 2D radius around the body.
    /// </summary>
    public static Vector3 CalculateReachPoint(
        Vector3 bodyPosition,
        Vector3 targetPosition,
        float reachRadius)
    {
        Vector2 bodyPoint = bodyPosition;
        Vector2 targetPoint = targetPosition;
        Vector2 offset = targetPoint - bodyPoint;
        float safeRadius = Mathf.Max(0f, reachRadius);
        if (offset.sqrMagnitude <= Mathf.Epsilon)
            return bodyPosition;

        targetPoint = bodyPoint + offset.normalized * safeRadius;

        return new Vector3(targetPoint.x, targetPoint.y, bodyPosition.z);
    }

    /// <summary>
    /// Keeps a pickup target at its actual distance without extending beyond maximum reach.
    /// </summary>
    public static Vector3 CalculatePickupReachPoint(
        Vector3 bodyPosition,
        Vector3 targetPosition,
        float reachRadius) {
        Vector2 bodyPoint = bodyPosition;
        Vector2 offset = (Vector2)targetPosition - bodyPoint;
        Vector2 point = bodyPoint + Vector2.ClampMagnitude(offset, Mathf.Max(0f, reachRadius));
        return new Vector3(point.x, point.y, bodyPosition.z);
    }

    private Vector3 ResolveRadialTargetPosition()
    {
        if (followTargetDistance)
            return CalculatePickupReachPoint(bodyReference.position, target.position, maximumReach);

        Vector2 offset = target.position - bodyReference.position;
        if (offset.sqrMagnitude > Mathf.Epsilon)
        {
            lastTargetDirection = offset.normalized;
            hasTargetDirection = true;
        }

        Vector2 direction = hasTargetDirection ? lastTargetDirection : Vector2.right;
        Vector2 point = (Vector2)bodyReference.position
            + direction * Mathf.Max(0f, maximumReach);
        return new Vector3(point.x, point.y, bodyReference.position.z);
    }

    private void ResolveReferences()
    {
        if (bodyReference == null)
        {
            RobotBodyController robotBody = GetComponent<RobotBodyController>();
            bodyReference = robotBody != null && robotBody.BodyReference != null
                ? robotBody.BodyReference
                : transform;
        }
        if (leftArmSolverTarget == null)
            leftArmSolverTarget = FindDescendant("LArm_Solver_Target");
        if (rightArmSolverTarget == null)
            rightArmSolverTarget = FindDescendant("RArm_Solver_Target");
        if (leftHandEffector == null)
            leftHandEffector = FindDescendant("LHand_Effector");
        if (rightHandEffector == null)
            rightHandEffector = FindDescendant("RHand_Effector");
        if (leftArmIkSolver == null)
            leftArmIkSolver = ResolveIkSolver(leftArmSolverTarget);
        if (rightArmIkSolver == null)
            rightArmIkSolver = ResolveIkSolver(rightArmSolverTarget);
    }

    private void CaptureRestPose()
    {
        if (leftArmSolverTarget != null && !leftRestCaptured)
        {
            leftRestLocalPosition = leftArmSolverTarget.localPosition;
            leftRestLocalRotation = leftArmSolverTarget.localRotation;
            leftRestCaptured = true;
        }

        if (rightArmSolverTarget != null && !rightRestCaptured)
        {
            rightRestLocalPosition = rightArmSolverTarget.localPosition;
            rightRestLocalRotation = rightArmSolverTarget.localRotation;
            rightRestCaptured = true;
        }
    }

    private void CaptureSolverDefaults()
    {
        if (solverDefaultsCaptured)
            return;
        if (leftArmIkSolver != null)
            leftSolverDefaultEnabled = leftArmIkSolver.enabled;
        if (rightArmIkSolver != null)
            rightSolverDefaultEnabled = rightArmIkSolver.enabled;
        solverDefaultsCaptured = true;
    }

    private void RestoreSolverDefaults()
    {
        if (!solverDefaultsCaptured)
            return;
        if (leftArmIkSolver != null)
            leftArmIkSolver.enabled = leftSolverDefaultEnabled;
        if (rightArmIkSolver != null)
            rightArmIkSolver.enabled = rightSolverDefaultEnabled;
    }

    private void ReturnBothArms(float deltaTime)
    {
        ReturnToRest(
            leftArmSolverTarget,
            leftRestLocalPosition,
            leftRestLocalRotation,
            leftRestCaptured,
            deltaTime);
        ReturnToRest(
            rightArmSolverTarget,
            rightRestLocalPosition,
            rightRestLocalRotation,
            rightRestCaptured,
            deltaTime);
    }

    private void RestoreRestPoseImmediate()
    {
        if (leftArmSolverTarget != null && leftRestCaptured)
        {
            leftArmSolverTarget.localPosition = leftRestLocalPosition;
            leftArmSolverTarget.localRotation = leftRestLocalRotation;
        }

        if (rightArmSolverTarget != null && rightRestCaptured)
        {
            rightArmSolverTarget.localPosition = rightRestLocalPosition;
            rightArmSolverTarget.localRotation = rightRestLocalRotation;
        }
    }

    private void FollowTarget(Transform solverTarget, Vector3 destination, float deltaTime)
    {
        if (solverTarget == null)
            return;
        Vector3 solverDestination = destination;
        solverDestination.z = solverTarget.position.z;
        solverTarget.position = Vector3.MoveTowards(
            solverTarget.position,
            solverDestination,
            followSpeed * Mathf.Max(0f, deltaTime));
    }

    private void ReturnToRest(
        Transform solverTarget,
        Vector3 restLocalPosition,
        Quaternion restLocalRotation,
        bool hasRestPose,
        float deltaTime)
    {
        if (solverTarget == null || !hasRestPose)
            return;
        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        solverTarget.localPosition = Vector3.MoveTowards(
            solverTarget.localPosition,
            restLocalPosition,
            returnSpeed * safeDeltaTime);
        solverTarget.localRotation = Quaternion.RotateTowards(
            solverTarget.localRotation,
            restLocalRotation,
            rotationReturnSpeed * safeDeltaTime);
    }

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

    private static Behaviour ResolveIkSolver(Transform solverTarget)
    {
        if (solverTarget == null || solverTarget.parent == null)
            return null;
        Behaviour[] behaviours = solverTarget.parent.GetComponents<Behaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].GetType().Name == "LimbSolver2D")
                return behaviours[i];
        }
        return null;
    }

    private static CowboyArmSide? ResolveAvailableArm(
        CowboyArmSide preferred,
        Transform leftSolverTarget,
        Transform rightSolverTarget)
    {
        if (preferred == CowboyArmSide.Left && leftSolverTarget != null)
            return CowboyArmSide.Left;
        if (preferred == CowboyArmSide.Right && rightSolverTarget != null)
            return CowboyArmSide.Right;
        if (leftSolverTarget != null)
            return CowboyArmSide.Left;
        if (rightSolverTarget != null)
            return CowboyArmSide.Right;
        return null;
    }

    private void DrawReachGizmos()
    {
        Transform centerReference = bodyReference;
        if (centerReference == null)
        {
            RobotBodyController robotBody = GetComponent<RobotBodyController>();
            centerReference = robotBody != null && robotBody.BodyReference != null
                ? robotBody.BodyReference
                : transform;
        }
        if (centerReference == null)
            return;

        Vector3 center = centerReference.position;
        float radius = Mathf.Max(0.01f, maximumReach);
        DrawArc(center, radius, 90f, 270f, leftArcColor);
        DrawArc(center, radius, -90f, 90f, rightArcColor);

        float threshold = Mathf.Max(0f, sideSwitchThreshold);
        Gizmos.color = sideSwitchColor;
        Gizmos.DrawLine(
            center + new Vector3(-threshold, -radius, 0f),
            center + new Vector3(-threshold, radius, 0f));
        Gizmos.DrawLine(
            center + new Vector3(threshold, -radius, 0f),
            center + new Vector3(threshold, radius, 0f));

        if (target == null)
            return;

        Vector3 reachPoint = followTargetDistance
            ? CalculatePickupReachPoint(center, target.position, radius)
            : CalculateReachPoint(center, target.position, radius);
        reachPoint.z = center.z;
        Vector3 targetPoint = target.position;
        targetPoint.z = center.z;
        Gizmos.color = targetLineColor;
        Gizmos.DrawLine(center, reachPoint);
        Gizmos.DrawWireSphere(reachPoint, Mathf.Max(0.04f, radius * 0.025f));
        if ((targetPoint - reachPoint).sqrMagnitude > 0.0001f)
            Gizmos.DrawLine(reachPoint, targetPoint);
    }

    private void ApplySolverFlip(bool targetIsRightSide)
    {
        SetSolverFlip(leftArmIkSolver, targetIsRightSide);
        SetSolverFlip(rightArmIkSolver, targetIsRightSide);
    }

    private void SetSolverFlip(Behaviour solver, bool flipValue)
    {
        if (solver == null)
            return;

        if (!solverFlipSetters.TryGetValue(solver, out Action<bool> setter) || setter == null)
        {
            setter = CreateFlipSetter(solver);
            solverFlipSetters[solver] = setter;
        }

        setter?.Invoke(flipValue);
    }

    private static Action<bool> CreateFlipSetter(Behaviour solver)
    {
        if (solver == null)
            return null;

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo property = solver.GetType().GetProperty("flip", Flags);
        if (property != null && property.PropertyType == typeof(bool) && property.GetSetMethod(true) != null)
            return value => property.SetValue(solver, value);

        FieldInfo field = solver.GetType().GetField("m_Flip", Flags);
        if (field != null && field.FieldType == typeof(bool))
            return value => field.SetValue(solver, value);

        return null;
    }

    private void DrawArc(
        Vector3 center,
        float radius,
        float startAngle,
        float endAngle,
        Color color)
    {
        int segments = Mathf.Clamp(arcSegments, 8, 96);
        Gizmos.color = color;
        Vector3 previous = PointOnCircle(center, radius, startAngle);
        for (int i = 1; i <= segments; i++)
        {
            float interpolation = i / (float)segments;
            float angle = Mathf.Lerp(startAngle, endAngle, interpolation);
            Vector3 current = PointOnCircle(center, radius, angle);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }

    private static Vector3 PointOnCircle(Vector3 center, float radius, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        return center + new Vector3(
            Mathf.Cos(angleRadians) * radius,
            Mathf.Sin(angleRadians) * radius,
            0f);
    }
}
