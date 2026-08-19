using UnityEngine;

/// <summary>
/// Procedurally moves one Master IK target toward an offered item, then restores
/// the authored rest pose. It deliberately does not own the exchange timer.
/// </summary>
public sealed class DocBotHandReachController : MonoBehaviour {
    [SerializeField] private Transform leftArmSolverTarget;
    [SerializeField] private Transform rightArmSolverTarget;
    [SerializeField, Min(0.01f)] private float movementSharpness = 8f;
    [SerializeField, Min(0.01f)] private float returnSharpness = 10f;
    [SerializeField, Min(0.01f)] private float maximumLocalReach = 2.5f;

    private Vector3 leftRestLocalPosition;
    private Vector3 rightRestLocalPosition;
    private bool leftRestPoseCached;
    private bool rightRestPoseCached;
    private Transform reachTarget;
    private DocBotHand activeHand;
    private bool isReaching;

    public bool IsReaching => isReaching;
    public DocBotHand ActiveHand => activeHand;

    /// <summary>
    /// Assigns the existing Master rig IK targets.
    /// </summary>
    public void Configure(Transform leftTarget, Transform rightTarget) {
        leftArmSolverTarget = leftTarget;
        rightArmSolverTarget = rightTarget;
        leftRestPoseCached = false;
        rightRestPoseCached = false;
        CacheRestPose();
    }

    private void Awake() {
        CacheRestPose();
    }

    private void OnEnable() {
        CacheRestPose();
    }

    private void OnDisable() {
        CancelReach();
        RestoreRestPoseImmediate();
    }

    private void LateUpdate() {
        AdvancePose(Time.deltaTime);
    }

    /// <summary>
    /// Advances the procedural Master targets using an explicit time step.
    /// </summary>
    public void AdvancePose(float deltaTime) {
        float safeDeltaTime = Mathf.Max(0f, deltaTime);
        UpdateTarget(
            leftArmSolverTarget,
            leftRestLocalPosition,
            DocBotHand.Left,
            safeDeltaTime);
        UpdateTarget(
            rightArmSolverTarget,
            rightRestLocalPosition,
            DocBotHand.Right,
            safeDeltaTime);

        if (isReaching && reachTarget == null) {
            CancelReach();
        }
    }

    /// <summary>
    /// Begins or redirects the procedural reach for one hand.
    /// </summary>
    public void BeginReach(DocBotHand hand, Transform target) {
        if (target == null) {
            CancelReach();
            return;
        }

        activeHand = hand;
        reachTarget = target;
        isReaching = true;
    }

    /// <summary>
    /// Returns both Master IK targets to their authored local positions.
    /// </summary>
    public void CancelReach() {
        reachTarget = null;
        isReaching = false;
    }

    private void UpdateTarget(
        Transform solverTarget,
        Vector3 restLocalPosition,
        DocBotHand hand,
        float deltaTime) {
        if (solverTarget == null || solverTarget.parent == null) {
            return;
        }

        bool shouldReach = isReaching && activeHand == hand && reachTarget != null;
        Vector3 desiredLocalPosition = restLocalPosition;
        float sharpness = returnSharpness;
        if (shouldReach) {
            Vector3 offeredLocalPosition = solverTarget.parent.InverseTransformPoint(reachTarget.position);
            Vector3 reachOffset = offeredLocalPosition - restLocalPosition;
            desiredLocalPosition = restLocalPosition
                + Vector3.ClampMagnitude(reachOffset, maximumLocalReach);
            sharpness = movementSharpness;
        }

        float blend = 1f - Mathf.Exp(-Mathf.Max(0.01f, sharpness) * deltaTime);
        solverTarget.localPosition = Vector3.Lerp(
            solverTarget.localPosition,
            desiredLocalPosition,
            blend);
    }

    private void CacheRestPose() {
        if (!leftRestPoseCached && leftArmSolverTarget != null) {
            leftRestLocalPosition = leftArmSolverTarget.localPosition;
            leftRestPoseCached = true;
        }

        if (!rightRestPoseCached && rightArmSolverTarget != null) {
            rightRestLocalPosition = rightArmSolverTarget.localPosition;
            rightRestPoseCached = true;
        }
    }

    private void RestoreRestPoseImmediate() {
        if (leftRestPoseCached && leftArmSolverTarget != null) {
            leftArmSolverTarget.localPosition = leftRestLocalPosition;
        }

        if (rightRestPoseCached && rightArmSolverTarget != null) {
            rightArmSolverTarget.localPosition = rightRestLocalPosition;
        }
    }
}
