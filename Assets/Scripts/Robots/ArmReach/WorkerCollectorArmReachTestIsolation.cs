using UnityEngine;

/// <summary>
/// Keeps the TestingSceneBot Worker Collector passive while its arm reach is inspected.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class WorkerCollectorArmReachTestIsolation : MonoBehaviour
{
    private WorkerCollectorBodyController workerCollectorBody;
    private RobotBrainNew brain;
    private RobotHeartNew heart;
    private RobotBodyController robotBody;

    private bool workerCollectorBodyWasEnabled;
    private bool brainWasEnabled;
    private bool heartWasEnabled;
    private bool robotBodyWasEnabled;
    private bool stateCaptured;

    private void Awake()
    {
        ResolveReferences();
        CaptureState();
        Isolate();
    }

    private void OnEnable()
    {
        ResolveReferences();
        CaptureState();
        Isolate();
    }

    private void OnDisable()
    {
        RestoreState();
    }

    private void ResolveReferences()
    {
        if (workerCollectorBody == null)
            workerCollectorBody = GetComponent<WorkerCollectorBodyController>();
        if (brain == null)
            brain = GetComponent<RobotBrainNew>();
        if (heart == null)
            heart = GetComponent<RobotHeartNew>();
        if (robotBody == null)
            robotBody = GetComponent<RobotBodyController>();
    }

    private void CaptureState()
    {
        if (stateCaptured)
            return;
        workerCollectorBodyWasEnabled = workerCollectorBody != null && workerCollectorBody.enabled;
        brainWasEnabled = brain != null && brain.enabled;
        heartWasEnabled = heart != null && heart.enabled;
        robotBodyWasEnabled = robotBody != null && robotBody.enabled;
        stateCaptured = true;
    }

    private void Isolate()
    {
        robotBody?.StopMovement();
        if (workerCollectorBody != null)
            workerCollectorBody.enabled = false;
        if (brain != null)
            brain.enabled = false;
        if (heart != null)
            heart.enabled = false;
        if (robotBody != null)
            robotBody.enabled = false;
    }

    private void RestoreState()
    {
        if (!stateCaptured)
            return;
        if (workerCollectorBody != null)
            workerCollectorBody.enabled = workerCollectorBodyWasEnabled;
        if (brain != null)
            brain.enabled = brainWasEnabled;
        if (heart != null)
            heart.enabled = heartWasEnabled;
        if (robotBody != null)
            robotBody.enabled = robotBodyWasEnabled;
        stateCaptured = false;
    }
}
