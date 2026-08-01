using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum SpawnRobotCollectorPanelAxis {
    LocalX,
    LocalY,
    LocalZ
}

public class SpawnRobotCollectorController : MonoBehaviour {
    private const string DefaultTopPanelName = "Spawn Top";
    private const string DefaultBottomPanelName = "Spawn Bottom";

    [Header("Robot Detection")]
    [SerializeField] private PositionTriggerZone robotDetectionZone;
    [SerializeField, Min(0.02f)] private float scanInterval = 0.2f;

    [Header("Panels")]
    [SerializeField] private Transform topPanel;
    [FormerlySerializedAs("backgroundPanel")]
    [SerializeField] private Transform bottomPanel;
    [SerializeField] private SpawnRobotCollectorPanelAxis retractAxis = SpawnRobotCollectorPanelAxis.LocalZ;
    [SerializeField, Range(0f, 1f)] private float openScaleMultiplier = 0f;
    [SerializeField] private float topFixedEdgeLocalPosition = 3.590796f;
    [SerializeField] private float bottomFixedEdgeLocalPosition = -5.5264487f;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float openDuration = 1f;
    [SerializeField, Min(0f)] private float openHoldDuration = 1f;
    [SerializeField, Min(0.01f)] private float closeDuration = 1f;

    private readonly HashSet<RobotStateController> detectedDeadRobots = new();
    private Vector3 topClosedLocalPosition;
    private Vector3 topClosedLocalScale;
    private Vector3 bottomClosedLocalPosition;
    private Vector3 bottomClosedLocalScale;
    private Coroutine panelRoutine;
    private float nextScanTime;
    private bool initialized;
    private bool cycleRequested;
    private bool isCycling;

    public event Action OnPanelsOpenReady;
    public event Action OnCycleCompleted;

    public bool IsCycling => isCycling;

    private void Awake() {
        Initialize();
    }

    private void OnEnable() {
        Initialize();
        nextScanTime = 0f;
    }

    private void Update() {
        if (Time.time < nextScanTime)
            return;

        nextScanTime = Time.time + Mathf.Max(0.02f, scanInterval);
        ScanForDeadRobots();
    }

    private void OnDisable() {
        if (panelRoutine != null)
        {
            StopCoroutine(panelRoutine);
            panelRoutine = null;
        }

        cycleRequested = false;
        isCycling = false;
        ResetPanels();
    }

    /// <summary>
    /// Checks every robot collider in the configured zone and opens the machine when a new dead robot is found.
    /// </summary>
    public bool ScanForDeadRobots() {
        ResolveDetectionZone();
        if (robotDetectionZone == null)
            return false;

        RemoveInvalidDetectedRobots();

        Collider2D[] colliders = robotDetectionZone.GetOverlappingColliders();
        bool foundNewDeadRobot = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D detectedCollider = colliders[i];
            if (detectedCollider == null)
                continue;

            RobotStateController robot = detectedCollider.GetComponentInParent<RobotStateController>();
            if (robot == null || robot.CurrentState != RobotState.Dead)
                continue;

            if (detectedDeadRobots.Add(robot))
                foundNewDeadRobot = true;
        }

        if (foundNewDeadRobot)
            PlayOpenCloseCycle();

        return foundNewDeadRobot;
    }

    /// <summary>
    /// Collapses the top and bottom panels toward their outer edges, then restores them.
    /// Calls made during a cycle are combined into one pending cycle.
    /// </summary>
    public void PlayOpenCloseCycle() {
        if (!isActiveAndEnabled)
            return;

        Initialize();
        if (!HasValidPanels())
            return;

        if (isCycling)
        {
            cycleRequested = true;
            return;
        }

        panelRoutine = StartCoroutine(OpenCloseRoutine());
    }

    /// <summary>
    /// Restores the top and bottom panels to their startup transforms.
    /// </summary>
    public void ResetPanels() {
        if (!initialized)
            return;

        ResetPanel(topPanel, topClosedLocalPosition, topClosedLocalScale);
        ResetPanel(bottomPanel, bottomClosedLocalPosition, bottomClosedLocalScale);
    }

    private IEnumerator OpenCloseRoutine() {
        do
        {
            cycleRequested = false;
            isCycling = true;

            yield return AnimatePanelsRoutine(0f, 1f, openDuration);
            OnPanelsOpenReady?.Invoke();

            if (openHoldDuration > 0f)
                yield return new WaitForSeconds(openHoldDuration);

            yield return AnimatePanelsRoutine(1f, 0f, closeDuration);
            ResetPanels();
            OnCycleCompleted?.Invoke();
        }
        while (cycleRequested && isActiveAndEnabled);

        isCycling = false;
        panelRoutine = null;
    }

    private IEnumerator AnimatePanelsRoutine(float fromOpenAmount, float toOpenAmount, float duration) {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            float normalizedTime = elapsed / safeDuration;
            float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
            ApplyPanelOpenAmount(Mathf.Lerp(fromOpenAmount, toOpenAmount, easedTime));

            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyPanelOpenAmount(toOpenAmount);
    }

    private void ApplyPanelOpenAmount(float openAmount) {
        float clampedOpenAmount = Mathf.Clamp01(openAmount);
        ApplyPanelOpenAmount(
            topPanel,
            topClosedLocalPosition,
            topClosedLocalScale,
            topFixedEdgeLocalPosition,
            clampedOpenAmount);
        ApplyPanelOpenAmount(
            bottomPanel,
            bottomClosedLocalPosition,
            bottomClosedLocalScale,
            bottomFixedEdgeLocalPosition,
            clampedOpenAmount);
    }

    private void ApplyPanelOpenAmount(
        Transform panel,
        Vector3 closedLocalPosition,
        Vector3 closedLocalScale,
        float fixedEdgeLocalPosition,
        float openAmount) {
        if (panel == null)
            return;

        Vector3 scale = closedLocalScale;
        float closedAxisScale = GetAxisValue(closedLocalScale);
        float openAxisScale = closedAxisScale * openScaleMultiplier;
        SetAxisValue(ref scale, Mathf.Lerp(closedAxisScale, openAxisScale, openAmount));

        Vector3 anchorLocalPosition = Vector3.zero;
        SetAxisValue(ref anchorLocalPosition, fixedEdgeLocalPosition);
        Vector3 closedAnchorOffset = panel.localRotation * Vector3.Scale(closedLocalScale, anchorLocalPosition);
        Vector3 scaledAnchorOffset = panel.localRotation * Vector3.Scale(scale, anchorLocalPosition);

        panel.localScale = scale;
        panel.localPosition = closedLocalPosition + closedAnchorOffset - scaledAnchorOffset;
    }

    private void Initialize() {
        if (initialized)
            return;

        ResolveDetectionZone();
        ResolvePanels();

        if (!HasValidPanels())
        {
            Debug.LogWarning($"{nameof(SpawnRobotCollectorController)} on '{name}' could not find both collector panels.", this);
            return;
        }

        topClosedLocalPosition = topPanel.localPosition;
        topClosedLocalScale = topPanel.localScale;
        bottomClosedLocalPosition = bottomPanel.localPosition;
        bottomClosedLocalScale = bottomPanel.localScale;
        initialized = true;
    }

    private void ResolveDetectionZone() {
        if (robotDetectionZone == null)
            robotDetectionZone = GetComponentInChildren<PositionTriggerZone>(true);
    }

    private void ResolvePanels() {
        if (topPanel == null)
            topPanel = transform.Find(DefaultTopPanelName);

        if (bottomPanel == null)
            bottomPanel = transform.Find(DefaultBottomPanelName);
    }

    private bool HasValidPanels() {
        return topPanel != null && bottomPanel != null;
    }

    private void RemoveInvalidDetectedRobots() {
        detectedDeadRobots.RemoveWhere(robot => robot == null || robot.CurrentState != RobotState.Dead);
    }

    private static void ResetPanel(Transform panel, Vector3 closedLocalPosition, Vector3 closedLocalScale) {
        if (panel == null)
            return;

        panel.localPosition = closedLocalPosition;
        panel.localScale = closedLocalScale;
    }

    private float GetAxisValue(Vector3 value) {
        switch (retractAxis)
        {
            case SpawnRobotCollectorPanelAxis.LocalY:
                return value.y;
            case SpawnRobotCollectorPanelAxis.LocalZ:
                return value.z;
            default:
                return value.x;
        }
    }

    private void SetAxisValue(ref Vector3 value, float axisValue) {
        switch (retractAxis)
        {
            case SpawnRobotCollectorPanelAxis.LocalY:
                value.y = axisValue;
                break;
            case SpawnRobotCollectorPanelAxis.LocalZ:
                value.z = axisValue;
                break;
            default:
                value.x = axisValue;
                break;
        }
    }
}
