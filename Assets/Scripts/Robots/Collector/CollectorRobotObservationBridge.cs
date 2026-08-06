using UnityEngine;

/// <summary>
/// Forwards discrete Collector body observations to Brain's factual ingress API.
/// It does not select tasks or operate physical components.
/// </summary>
[DisallowMultipleComponent]
public sealed class CollectorRobotObservationBridge : MonoBehaviour {
    [SerializeField] private CollectorRobotBodyController body;
    [SerializeField] private RobotBrainNew brain;

    private bool subscribed;
    private CollectorMissionAssignment subscribedAssignment;
    private DeadRobotCollectable subscribedTarget;

    public int AcceptedObservationCount { get; private set; }
    public int RejectedObservationCount { get; private set; }

    private void Awake() {
        ResolveReferences();
    }

    private void OnEnable() {
        ResolveReferences();
        Subscribe();
    }

    private void OnDisable() {
        Unsubscribe();
    }

    /// <summary>
    /// Wires the body event source and Brain ingress target.
    /// This is editor-safe and may be called by the prefab builder.
    /// </summary>
    public void ConfigureReferences(CollectorRobotBodyController source, RobotBrainNew targetBrain) {
        bool shouldResubscribe = isActiveAndEnabled && subscribed;
        if (shouldResubscribe)
            Unsubscribe();

        body = source;
        brain = targetBrain;

        if (isActiveAndEnabled)
            Subscribe();
    }

    private void ResolveReferences() {
        if (body == null)
            body = GetComponent<CollectorRobotBodyController>();
        if (brain == null)
            brain = GetComponent<RobotBrainNew>();
    }

    private void Subscribe() {
        if (subscribed || body == null)
            return;

        body.OnObservation += HandleObservation;
        body.OnAssignmentChanged += HandleAssignmentChanged;
        subscribed = true;
        HandleAssignmentChanged(body.CurrentAssignment);
    }

    private void Unsubscribe() {
        if (!subscribed)
            return;

        UnsubscribeFromTarget();
        if (body != null) {
            body.OnObservation -= HandleObservation;
            body.OnAssignmentChanged -= HandleAssignmentChanged;
        }
        subscribed = false;
    }

    private void HandleObservation(CollectorBodyObservation observation) {
        if (body != null
            && body.IsObservationCurrent(observation)
            && brain != null
            && brain.OnCollectorBodyObservation(observation)) {
            AcceptedObservationCount++;
        } else {
            RejectedObservationCount++;
        }
    }

    private void HandleAssignmentChanged(CollectorMissionAssignment assignment) {
        if (ReferenceEquals(subscribedAssignment, assignment))
            return;

        UnsubscribeFromTarget();
        subscribedAssignment = assignment;
        DeadRobotCollectable target = assignment != null ? assignment.Target : null;
        if (target == null)
            return;

        subscribedTarget = target;

        subscribedTarget.OnInvalidated += HandleTargetInvalidated;
        subscribedTarget.OnClaimLost += HandleTargetInvalidated;
    }

    private void HandleTargetInvalidated(CollectorTargetClaim claim) {
        CollectorMissionAssignment assignment = subscribedAssignment;
        if (assignment == null
            || claim != assignment.Claim
            || body == null
            || !ReferenceEquals(body.CurrentAssignment, assignment)) {
            return;
        }

        if (brain != null && brain.OnCollectorTargetInvalidated(assignment))
            AcceptedObservationCount++;
        else
            RejectedObservationCount++;
    }

    private void UnsubscribeFromTarget() {
        if (!ReferenceEquals(subscribedTarget, null)) {
            subscribedTarget.OnInvalidated -= HandleTargetInvalidated;
            subscribedTarget.OnClaimLost -= HandleTargetInvalidated;
        }

        subscribedTarget = null;
        subscribedAssignment = null;
    }
}
