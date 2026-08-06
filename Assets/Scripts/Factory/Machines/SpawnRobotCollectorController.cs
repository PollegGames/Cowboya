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

/// <summary>
/// Owns dead-robot jobs for one Collector machine. It claims targets and performs
/// launch/dock handshakes, while Memory, Brain, Heart, and Tasks own robot intent.
/// </summary>
public class SpawnRobotCollectorController : MonoBehaviour {
    private const string DefaultTopPanelName = "Spawn Top";
    private const string DefaultBottomPanelName = "Spawn Bottom";
    private const string DefaultSpawnPointName = "SpawnPoint";
    private const string DefaultLaunchExitPointName = "LaunchExitPoint";
    private const string DefaultDockApproachPointName = "DockApproachPoint";
    private const string DefaultIntakePointName = "IntakePoint";
    private const string DefaultIntakeZoneName = "CollectorIntakeZone";

    [Header("Robot Detection")]
    [SerializeField] private PositionTriggerZone robotDetectionZone;
    [SerializeField, Min(0.02f)] private float scanInterval = 0.2f;

    [Header("Collector Mission")]
    [SerializeField] private GameObject collectorPrefab;
    [SerializeField] private Transform collectorParent;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform launchExitPoint;
    [SerializeField] private Transform dockApproachPoint;
    [SerializeField] private Transform intakePoint;
    [SerializeField] private Collider2D intakeZone;
    [SerializeField, Min(0.1f)] private float fallbackIntakeRadius = 0.8f;
    [SerializeField, Min(0f)] private float cargoIntakeMargin = 1.4f;
    [SerializeField, Min(1f)] private float abortReturnTimeout = 15f;

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
    private readonly List<DeadRobotCollectable> queuedTargets = new();
    private readonly HashSet<int> queuedTargetIds = new();
    private Vector3 topClosedLocalPosition;
    private Vector3 topClosedLocalScale;
    private Vector3 bottomClosedLocalPosition;
    private Vector3 bottomClosedLocalScale;
    private Coroutine panelRoutine;
    private float nextScanTime;
    private float abortDeadline;
    private int nextMissionId;
    private bool initialized;
    private bool cycleRequested;
    private bool isCycling;
    private bool shuttingDown;
    private bool finalizingMission;
    private bool waitForCollectorToClearIntake;
    private bool panelsFullyOpen;
    private bool panelsClosing;
    private string lastIntakeState;

    private CollectorMissionAssignment pendingMission;
    private CollectorMissionAssignment activeMission;
    private GameObject activeCollector;
    private RobotMemoryNew activeMemory;
    private RobotBrainNew activeBrain;
    private RobotStateController activeState;
    private CollectorPoolLifecycle activeLifecycle;

    public event Action OnPanelsOpenReady;
    public event Action OnCycleCompleted;

    public bool IsCycling => isCycling;
    public int QueuedTargetCount => queuedTargets.Count;
    public CollectorMissionAssignment PendingMission => pendingMission;
    public CollectorMissionAssignment ActiveMission => activeMission;
    public GameObject ActiveCollector => activeCollector;
    public GameObject CollectorPrefab => collectorPrefab;
    public Collider2D IntakeZone => intakeZone;
    public Transform LaunchExitPoint => launchExitPoint != null ? launchExitPoint : ResolveSpawnPoint();
    public Transform DockApproachPoint => dockApproachPoint != null ? dockApproachPoint : ResolveSpawnPoint();
    public Transform IntakePoint => intakePoint != null ? intakePoint : ResolveSpawnPoint();

    private void Awake() {
        Initialize();
    }

    private void OnEnable() {
        shuttingDown = false;
        Initialize();
        nextScanTime = 0f;
        RobotStateController.OnAnyRobotKilled -= HandleAnyRobotKilled;
        RobotStateController.OnAnyRobotKilled += HandleAnyRobotKilled;
    }

    private void Update() {
        if (Time.time >= nextScanTime) {
            nextScanTime = Time.time + Mathf.Max(0.02f, scanInterval);
            ScanForDeadRobots();
        }

        if (activeMission == null || activeCollector == null)
            return;

        TryProcessActiveIntake();
        if (abortDeadline > 0f && Time.time >= abortDeadline)
            EmergencyRecallActiveCollector("abort_return_timeout");
    }

    private void OnDisable() {
        RobotStateController.OnAnyRobotKilled -= HandleAnyRobotKilled;
        shuttingDown = true;

        if (panelRoutine != null) {
            StopCoroutine(panelRoutine);
            panelRoutine = null;
        }

        CancelPendingMission(requeue: false);
        if (activeMission != null) {
            if (activeState != null && activeState.CurrentState == RobotState.Dead)
                ReleaseActiveOwnershipAfterDeath();
            else
                ReleaseActiveCollectorWithoutCompleting("machine_disabled", requeueTarget: false);
        }

        detectedDeadRobots.Clear();
        queuedTargets.Clear();
        queuedTargetIds.Clear();
        cycleRequested = false;
        isCycling = false;
        panelsFullyOpen = false;
        panelsClosing = false;
        ResetPanels();
    }

    /// <summary>
    /// Returns the current world-space launch exit used by the flight body.
    /// </summary>
    public Vector2 GetLaunchExitPosition() {
        Transform point = LaunchExitPoint;
        return point != null ? (Vector2)point.position : (Vector2)transform.position;
    }

    /// <summary>
    /// Returns the current world-space dock holding point used by the flight body.
    /// </summary>
    public Vector2 GetDockApproachPosition() {
        Transform point = DockApproachPoint;
        return point != null ? (Vector2)point.position : (Vector2)transform.position;
    }

    /// <summary>
    /// Returns the current world-space intake point used by the flight body.
    /// </summary>
    public Vector2 GetIntakePosition() {
        Transform point = IntakePoint;
        return point != null ? (Vector2)point.position : (Vector2)transform.position;
    }

    /// <summary>
    /// Wires the Collector prefab and live machine markers. This is the supported editor-builder seam.
    /// </summary>
    public void ConfigureMissionReferences(GameObject prefab, Transform collectorSpawnPoint,
        Transform launchPoint, Transform dockPoint, Transform collectorIntakePoint,
        Collider2D collectorIntakeZone) {
        collectorPrefab = prefab;
        spawnPoint = collectorSpawnPoint;
        launchExitPoint = launchPoint;
        dockApproachPoint = dockPoint;
        intakePoint = collectorIntakePoint;
        intakeZone = collectorIntakeZone;
    }

    /// <summary>
    /// Checks every robot collider in the broad machine zone, creates corpse contracts,
    /// and queues newly discovered dead robots without duplicating child colliders.
    /// </summary>
    public bool ScanForDeadRobots() {
        ResolveDetectionZone();
        if (robotDetectionZone == null)
            return false;

        RemoveInvalidDetectedRobots();
        ReconcileQueuedTargets();

        Collider2D[] colliders = robotDetectionZone.GetOverlappingColliders();
        bool foundNewDeadRobot = false;
        for (int i = 0; i < colliders.Length; i++) {
            Collider2D detectedCollider = colliders[i];
            if (detectedCollider == null)
                continue;

            RobotStateController robot = detectedCollider.GetComponentInParent<RobotStateController>();
            if (!IsEligibleDeadRobot(robot))
                continue;

            if (detectedDeadRobots.Add(robot))
                foundNewDeadRobot = true;

            DeadRobotCollectable target = DeadRobotCollectable.EnsureFor(robot);
            QueueTarget(target);
        }

        if (isActiveAndEnabled)
            TryDispatchNextMission();

        if (foundNewDeadRobot && pendingMission == null && activeMission == null && queuedTargets.Count == 0)
            PlayOpenCloseCycle();

        return foundNewDeadRobot;
    }

    /// <summary>
    /// Claims the next deterministic queued corpse and begins the held-open launch cycle.
    /// </summary>
    public bool TryDispatchNextMission() {
        if (!isActiveAndEnabled || shuttingDown || finalizingMission || isCycling)
            return false;
        if (collectorPrefab == null || pendingMission != null || activeMission != null)
            return false;

        ReconcileQueuedTargets();
        SortQueuedTargets();
        for (int i = 0; i < queuedTargets.Count; i++) {
            DeadRobotCollectable target = queuedTargets[i];
            if (target == null)
                continue;

            int missionId = nextMissionId + 1;
            if (!target.TryClaim(missionId, this, out CollectorTargetClaim claim))
                continue;

            nextMissionId = missionId;
            pendingMission = new CollectorMissionAssignment(missionId, this, target, claim);
            RemoveQueuedTargetAt(i);

            if (HasValidPanels())
                PlayOpenCloseCycle();
            else
                DispatchPendingCollector();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Collapses the panels, holds them as required by launch/dock handshakes, then restores them.
    /// Calls made during a cycle request one follow-up evaluation after the current close.
    /// </summary>
    public void PlayOpenCloseCycle() {
        if (!isActiveAndEnabled)
            return;

        Initialize();
        if (!HasValidPanels())
            return;

        if (isCycling) {
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
        cycleRequested = false;
        isCycling = true;
        panelsClosing = false;
        panelsFullyOpen = false;

        yield return AnimatePanelsRoutine(0f, 1f, openDuration);
        panelsFullyOpen = true;
        HandlePanelsFullyOpen();
        OnPanelsOpenReady?.Invoke();

        bool missionHeldAtOpen = ShouldKeepPanelsOpen();
        float legacyHoldElapsed = 0f;
        while (isActiveAndEnabled && (ShouldKeepPanelsOpen()
            || (!missionHeldAtOpen && legacyHoldElapsed < openHoldDuration))) {
            legacyHoldElapsed += Time.deltaTime;
            yield return null;
        }

        panelsFullyOpen = false;
        panelsClosing = true;
        yield return AnimatePanelsRoutine(1f, 0f, closeDuration);
        panelsClosing = false;
        ResetPanels();
        OnCycleCompleted?.Invoke();

        bool replayRequested = cycleRequested;
        cycleRequested = false;
        isCycling = false;
        panelRoutine = null;

        if (!isActiveAndEnabled || shuttingDown)
            yield break;

        if (activeMission == null && pendingMission == null && TryDispatchNextMission())
            yield break;

        if (replayRequested || ShouldOpenForActiveReturn())
            PlayOpenCloseCycle();
    }

    private IEnumerator AnimatePanelsRoutine(float fromOpenAmount, float toOpenAmount, float duration) {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration) {
            float normalizedTime = elapsed / safeDuration;
            float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
            ApplyPanelOpenAmount(Mathf.Lerp(fromOpenAmount, toOpenAmount, easedTime));
            elapsed += Time.deltaTime;
            yield return null;
        }

        ApplyPanelOpenAmount(toOpenAmount);
    }

    private void HandlePanelsFullyOpen() {
        if (pendingMission != null) {
            DispatchPendingCollector();
            return;
        }

        TryGrantDockAccess();
    }

    private void TryGrantDockAccess() {
        if (!panelsFullyOpen)
            return;

        if (activeMission == null || activeBrain == null || activeMemory == null)
            return;

        CollectorMissionFacts facts = activeMemory.Snapshot.Collector;
        bool aborting = IsAborting(facts);
        if (!ReferenceEquals(facts.Assignment, activeMission)
            || !facts.DockApproachReached
            || (facts.CargoLost && !aborting)
            || facts.DockAccessGranted) {
            return;
        }

        if (aborting || facts.CargoSecure)
            activeBrain.OnCollectorDockAccessChanged(activeMission, true);
    }

    private void DispatchPendingCollector() {
        CollectorMissionAssignment assignment = pendingMission;
        if (assignment == null)
            return;

        if (assignment.Target == null || !assignment.Target.IsClaimValid(assignment.Claim)) {
            CancelPendingMission(requeue: true);
            return;
        }

        ObjectPool pool = ObjectPool.Instance;
        GameObject collector = pool != null
            ? pool.Get(collectorPrefab, collectorParent)
            : Instantiate(collectorPrefab, collectorParent);
        if (collector == null) {
            CancelPendingMission(requeue: true);
            return;
        }

        collector.SetActive(false);
        RobotHeartNew heart = collector.GetComponent<RobotHeartNew>();
        RobotBrainNew brain = collector.GetComponent<RobotBrainNew>();
        RobotMemoryNew memory = collector.GetComponent<RobotMemoryNew>();
        RobotStateController state = collector.GetComponent<RobotStateController>();
        CollectorPoolLifecycle lifecycle = collector.GetComponent<CollectorPoolLifecycle>();
        if (heart == null || brain == null || memory == null || state == null || lifecycle == null) {
            Debug.LogError($"Collector prefab '{collectorPrefab.name}' is missing its runtime pipeline wiring.", collectorPrefab);
            ReleaseCollectorInstance(collector, lifecycle, "invalid_prefab");
            CancelPendingMission(requeue: true);
            return;
        }

        heart.ConfigureRole(RobotRole.Collector, resetStack: true);
        brain.SetPlanPublicationSuspended(false);
        brain.ResetPlanningCache();
        state.Stats = new EnemyRobotFactory().CreateRobot();
        state.Stats.RobotName = $"Collector {assignment.MissionId}";
        collector.GetComponent<JointBreaker>()?.RestoreAll();
        PositionCollectorAtSpawn(collector);

        activeMission = assignment;
        activeCollector = collector;
        activeMemory = memory;
        activeBrain = brain;
        activeState = state;
        activeLifecycle = lifecycle;
        pendingMission = null;
        lastIntakeState = null;
        SubscribeToActiveCollector();

        if (!brain.OnCollectorMissionAssigned(assignment)) {
            UnsubscribeFromActiveCollector();
            ClearActiveReferences();
            ReleaseCollectorInstance(collector, lifecycle, "assignment_rejected");
            ReleaseOrRequeueAssignment(assignment, requeue: true);
            return;
        }

        abortDeadline = 0f;
        collector.SetActive(true);
    }

    private void PositionCollectorAtSpawn(GameObject collector) {
        Transform point = ResolveSpawnPoint();
        if (collector == null || point == null)
            return;

        // The machine is authored in the room's 3D XZ presentation hierarchy and its
        // world rotation commonly contains -90 degrees around X. The Collector is a
        // Rigidbody2D/Sprite rig and must remain in the world XY plane. Copying the
        // marker rotation made every sprite edge-on and projected the bone offsets out
        // of the physics plane. Preserve the prefab's authored planar rotation instead.
        Quaternion collectorRotation = collectorPrefab != null
            ? collectorPrefab.transform.rotation
            : Quaternion.identity;
        collector.transform.SetPositionAndRotation(point.position, collectorRotation);
        Rigidbody2D body = FindCollectorBody(collector);
        if (body != null)
            collector.transform.position += point.position - body.transform.position;

        Physics2D.SyncTransforms();
    }

    private void SubscribeToActiveCollector() {
        if (activeMemory != null) {
            activeMemory.OnMemoryChanged -= HandleActiveMemoryChanged;
            activeMemory.OnMemoryChanged += HandleActiveMemoryChanged;
        }

        if (activeState != null) {
            activeState.OnStateChanged -= HandleActiveStateChanged;
            activeState.OnStateChanged += HandleActiveStateChanged;
        }
    }

    private void UnsubscribeFromActiveCollector() {
        if (activeMemory != null)
            activeMemory.OnMemoryChanged -= HandleActiveMemoryChanged;
        if (activeState != null)
            activeState.OnStateChanged -= HandleActiveStateChanged;
    }

    private void HandleActiveMemoryChanged(MemoryChangeEvent change) {
        if (finalizingMission || activeMission == null || activeMemory == null)
            return;

        CollectorMissionFacts facts = change.Snapshot.Collector;
        if (!ReferenceEquals(facts.Assignment, activeMission))
            return;

        if (change.Type == MemoryChangeType.CollectorLaunchChanged && facts.LaunchExitReached)
            cycleRequested = false;

        if (change.Type == MemoryChangeType.CollectorCargoChanged && facts.CargoLost) {
            waitForCollectorToClearIntake = IsActiveCollectorInsideIntake();
            if (facts.DockAccessGranted)
                activeBrain?.OnCollectorDockAccessChanged(activeMission, false);
        }

        if (IsAborting(facts) && abortDeadline <= 0f)
            abortDeadline = Time.time + Mathf.Max(1f, abortReturnTimeout);

        if (change.Type == MemoryChangeType.CollectorDockChanged
            && facts.DockApproachReached
            && !facts.DockAccessGranted
            && (!facts.CargoLost || IsAborting(facts))) {
            if (panelsFullyOpen)
                TryGrantDockAccess();
            else if (!isCycling || panelsClosing)
                PlayOpenCloseCycle();
        }
    }

    private void HandleActiveStateChanged(RobotState state) {
        if (state != RobotState.Dead || activeMission == null)
            return;

        ReleaseActiveOwnershipAfterDeath();
    }

    private void HandleAnyRobotKilled(RobotStateController _) {
        if (isActiveAndEnabled)
            ScanForDeadRobots();
    }

    private void TryProcessActiveIntake() {
        if (finalizingMission || activeMission == null || activeBrain == null || activeMemory == null)
            return;

        CollectorMissionFacts facts = activeMemory.Snapshot.Collector;
        if (!ReferenceEquals(facts.Assignment, activeMission) || !facts.DockAccessGranted)
            return;
        if (!IsActiveCollectorInsideIntake()) {
            ReportIntakeState("waiting_for_collector");
            return;
        }

        bool aborting = IsAborting(facts);
        if (!aborting) {
            if (!facts.CargoSecure) {
                ReportIntakeState("waiting_for_secure_cargo");
                return;
            }
            if (intakeZone == null) {
                ReportIntakeState("intake_zone_missing");
                return;
            }
            if (activeMission.Target == null
                || !activeMission.Target.IsClaimValid(activeMission.Claim)) {
                ReportIntakeState("target_claim_invalid");
                return;
            }
            if (!activeMission.Target.AreAllRequiredPartsWithinIntake(
                intakeZone,
                activeMission.Claim,
                cargoIntakeMargin)) {
                ReportIntakeState("waiting_for_cargo_envelope");
                return;
            }
        }

        if (!activeBrain.OnCollectorIntakeConfirmed(activeMission)) {
            ReportIntakeState("brain_rejected_confirmation");
            return;
        }

        ReportIntakeState("ready_to_finalize");
        FinalizeIntake(aborting);
    }

    private void ReportIntakeState(string state) {
        if (lastIntakeState == state)
            return;

        lastIntakeState = state;
        Debug.Log(
            $"[CollectorIntake] machine={name} mission={activeMission?.MissionId ?? 0} state={state}",
            this);
    }

    private void FinalizeIntake(bool aborting) {
        if (finalizingMission || activeMission == null)
            return;

        finalizingMission = true;
        CollectorMissionAssignment assignment = activeMission;
        GameObject collector = activeCollector;
        CollectorPoolLifecycle lifecycle = activeLifecycle;
        UnsubscribeFromActiveCollector();
        lifecycle?.PrepareForPoolRelease(aborting ? "abort_intake" : "collection_complete");

        bool completedTarget = false;
        if (!aborting && assignment.Target != null)
            completedTarget = assignment.Target.CompleteCollection(assignment.Claim);

        if (completedTarget) {
            GameObject corpseRoot = assignment.Target.gameObject;
            ReleaseCompletedTarget(corpseRoot);
        } else {
            ReleaseOrRequeueAssignment(assignment, requeue: true);
        }

        ClearActiveReferences();
        ReleaseCollectorInstance(collector, lifecycle, aborting ? "abort_intake" : "collection_complete");
        finalizingMission = false;
        waitForCollectorToClearIntake = false;
        cycleRequested = queuedTargets.Count > 0;

        if (!isCycling)
            TryDispatchNextMission();
    }

    private void ReleaseActiveOwnershipAfterDeath() {
        if (activeMission == null)
            return;

        CollectorMissionAssignment assignment = activeMission;
        UnsubscribeFromActiveCollector();
        ReleaseOrRequeueAssignment(assignment, requeue: true);
        ClearActiveReferences();
        waitForCollectorToClearIntake = false;
        cycleRequested = queuedTargets.Count > 0;

        if (!isCycling)
            TryDispatchNextMission();
    }

    private void EmergencyRecallActiveCollector(string reason) {
        if (activeMission == null || finalizingMission)
            return;

        Debug.LogWarning($"Collector mission {activeMission.MissionId} was recalled: {reason}.", this);
        ReleaseActiveCollectorWithoutCompleting(reason, requeueTarget: true);
    }

    private void ReleaseActiveCollectorWithoutCompleting(string reason, bool requeueTarget) {
        if (activeMission == null)
            return;

        finalizingMission = true;
        CollectorMissionAssignment assignment = activeMission;
        GameObject collector = activeCollector;
        CollectorPoolLifecycle lifecycle = activeLifecycle;
        UnsubscribeFromActiveCollector();
        lifecycle?.PrepareForPoolRelease(reason);
        ReleaseOrRequeueAssignment(assignment, requeueTarget);
        ClearActiveReferences();
        ReleaseCollectorInstance(collector, lifecycle, reason);
        finalizingMission = false;
        waitForCollectorToClearIntake = false;
        cycleRequested = queuedTargets.Count > 0;

        if (!isCycling && !shuttingDown)
            TryDispatchNextMission();
    }

    private void ReleaseCollectorInstance(GameObject collector, CollectorPoolLifecycle lifecycle, string reason) {
        if (collector == null)
            return;

        lifecycle?.PrepareForPoolRelease(reason);
        collector.SetActive(false);
        ObjectPool pool = UnityEngine.Object.FindFirstObjectByType<ObjectPool>(FindObjectsInactive.Include);
        if (pool != null)
            pool.Release(collector);
        else if (Application.isPlaying)
            Destroy(collector);
        else
            DestroyImmediate(collector);
    }

    private static void ReleaseCompletedTarget(GameObject target) {
        if (target == null)
            return;

        ObjectPool pool = UnityEngine.Object.FindFirstObjectByType<ObjectPool>(FindObjectsInactive.Include);
        if (pool != null)
            pool.Release(target);
        else if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private void CancelPendingMission(bool requeue) {
        if (pendingMission == null)
            return;

        CollectorMissionAssignment assignment = pendingMission;
        pendingMission = null;
        ReleaseOrRequeueAssignment(assignment, requeue);
    }

    private void ReleaseOrRequeueAssignment(CollectorMissionAssignment assignment, bool requeue) {
        if (assignment == null || assignment.Target == null)
            return;

        if (assignment.Target.IsClaimValid(assignment.Claim))
            assignment.Target.ReleaseClaim(assignment.Claim);

        if (requeue && IsEligibleDeadRobot(assignment.Target.GetComponent<RobotStateController>()))
            QueueTarget(assignment.Target);
    }

    private bool ShouldKeepPanelsOpen() {
        if (pendingMission != null)
            return true;
        if (activeMission == null || activeMemory == null)
            return false;

        CollectorMissionFacts facts = activeMemory.Snapshot.Collector;
        if (!ReferenceEquals(facts.Assignment, activeMission))
            return false;
        if (!facts.LaunchExitReached)
            return true;
        if (facts.DockApproachReached || facts.DockAccessGranted)
            return true;
        if (waitForCollectorToClearIntake) {
            if (IsActiveCollectorInsideIntake())
                return true;
            waitForCollectorToClearIntake = false;
        }

        return false;
    }

    private bool ShouldOpenForActiveReturn() {
        if (activeMission == null || activeMemory == null)
            return false;

        CollectorMissionFacts facts = activeMemory.Snapshot.Collector;
        return ReferenceEquals(facts.Assignment, activeMission)
            && facts.LaunchExitReached
            && facts.DockApproachReached
            && (!facts.CargoLost || IsAborting(facts));
    }

    private bool IsActiveCollectorInsideIntake() {
        if (activeCollector == null)
            return false;

        Collider2D[] collectorColliders = activeCollector.GetComponentsInChildren<Collider2D>(true);
        if (intakeZone != null) {
            Bounds intakeBounds = intakeZone.bounds;
            for (int i = 0; i < collectorColliders.Length; i++) {
                Collider2D collider = collectorColliders[i];
                if (collider != null && !collider.isTrigger && collider.enabled
                    && BoundsOverlapIn2D(intakeBounds, collider.bounds)) {
                    return true;
                }
            }

            return false;
        }

        Rigidbody2D body = FindCollectorBody(activeCollector);
        Vector2 position = body != null ? body.position : (Vector2)activeCollector.transform.position;
        return Vector2.Distance(position, GetIntakePosition()) <= Mathf.Max(0.1f, fallbackIntakeRadius);
    }

    private static bool BoundsOverlapIn2D(Bounds first, Bounds second) {
        return first.min.x <= second.max.x
            && first.max.x >= second.min.x
            && first.min.y <= second.max.y
            && first.max.y >= second.min.y;
    }

    private void QueueTarget(DeadRobotCollectable target) {
        if (target == null || !target.IsCollectible)
            return;

        if (pendingMission != null && pendingMission.Target == target
            && target.IsClaimValid(pendingMission.Claim))
            return;
        if (activeMission != null && activeMission.Target == target
            && target.IsClaimValid(activeMission.Claim))
            return;

        int id = target.GetInstanceID();
        if (!queuedTargetIds.Add(id))
            return;

        queuedTargets.Add(target);
    }

    private void ReconcileQueuedTargets() {
        for (int i = queuedTargets.Count - 1; i >= 0; i--) {
            DeadRobotCollectable target = queuedTargets[i];
            RobotStateController state = target != null ? target.GetComponent<RobotStateController>() : null;
            if (target == null || !target.IsCollectible || !IsEligibleDeadRobot(state))
                queuedTargets.RemoveAt(i);
        }

        queuedTargetIds.Clear();
        for (int i = 0; i < queuedTargets.Count; i++) {
            if (queuedTargets[i] != null)
                queuedTargetIds.Add(queuedTargets[i].GetInstanceID());
        }
    }

    private void SortQueuedTargets() {
        Vector2 origin = GetDockApproachPosition();
        queuedTargets.Sort((left, right) => {
            float leftDistance = (GetCandidateCenter(left) - origin).sqrMagnitude;
            float rightDistance = (GetCandidateCenter(right) - origin).sqrMagnitude;
            int distanceOrder = leftDistance.CompareTo(rightDistance);
            if (distanceOrder != 0)
                return distanceOrder;
            int leftId = left != null ? left.GetInstanceID() : int.MaxValue;
            int rightId = right != null ? right.GetInstanceID() : int.MaxValue;
            return leftId.CompareTo(rightId);
        });
    }

    private void RemoveQueuedTargetAt(int index) {
        if (index < 0 || index >= queuedTargets.Count)
            return;

        DeadRobotCollectable target = queuedTargets[index];
        if (target != null)
            queuedTargetIds.Remove(target.GetInstanceID());
        queuedTargets.RemoveAt(index);
    }

    private static Vector2 GetCandidateCenter(DeadRobotCollectable target) {
        if (target == null)
            return Vector2.zero;

        Rigidbody2D[] bodies = target.GetComponentsInChildren<Rigidbody2D>(true);
        Vector2 sum = Vector2.zero;
        int count = 0;
        for (int i = 0; i < bodies.Length; i++) {
            Rigidbody2D body = bodies[i];
            if (body == null || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
                continue;
            sum += body.position;
            count++;
        }

        return count > 0 ? sum / count : (Vector2)target.transform.position;
    }

    private static bool IsEligibleDeadRobot(RobotStateController robot) {
        if (robot == null || robot.CurrentState != RobotState.Dead)
            return false;
        RobotHeartNew heart = robot.GetComponent<RobotHeartNew>();
        return heart == null || heart.Role != RobotRole.Collector;
    }

    private void RemoveInvalidDetectedRobots() {
        detectedDeadRobots.RemoveWhere(robot => !IsEligibleDeadRobot(robot));
    }

    private static bool IsAborting(CollectorMissionFacts facts) {
        return facts.TargetUnavailable || facts.MissionCancelled || facts.FlightFault;
    }

    private static Rigidbody2D FindCollectorBody(GameObject collector) {
        if (collector == null)
            return null;
        Rigidbody2D[] bodies = collector.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < bodies.Length; i++) {
            if (bodies[i] != null && bodies[i].name == "bone_Body")
                return bodies[i];
        }
        return bodies.Length > 0 ? bodies[0] : null;
    }

    private void ApplyPanelOpenAmount(float openAmount) {
        float clampedOpenAmount = Mathf.Clamp01(openAmount);
        ApplyPanelOpenAmount(topPanel, topClosedLocalPosition, topClosedLocalScale,
            topFixedEdgeLocalPosition, clampedOpenAmount);
        ApplyPanelOpenAmount(bottomPanel, bottomClosedLocalPosition, bottomClosedLocalScale,
            bottomFixedEdgeLocalPosition, clampedOpenAmount);
    }

    private void ApplyPanelOpenAmount(Transform panel, Vector3 closedLocalPosition,
        Vector3 closedLocalScale, float fixedEdgeLocalPosition, float openAmount) {
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
        ResolveDetectionZone();
        ResolvePanels();
        ResolveMissionReferences();

        if (initialized)
            return;
        if (!HasValidPanels()) {
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

    private void ResolveMissionReferences() {
        Transform resolvedSpawn = ResolveSpawnPoint();
        if (resolvedSpawn == null)
            return;

        if (launchExitPoint == null)
            launchExitPoint = resolvedSpawn.Find(DefaultLaunchExitPointName);
        if (dockApproachPoint == null)
            dockApproachPoint = resolvedSpawn.Find(DefaultDockApproachPointName);
        if (intakePoint == null)
            intakePoint = resolvedSpawn.Find(DefaultIntakePointName);
        if (intakeZone == null) {
            Transform zone = resolvedSpawn.Find(DefaultIntakeZoneName);
            if (zone != null)
                intakeZone = zone.GetComponent<Collider2D>();
        }
    }

    private Transform ResolveSpawnPoint() {
        if (spawnPoint == null)
            spawnPoint = transform.Find(DefaultSpawnPointName);
        return spawnPoint != null ? spawnPoint : transform;
    }

    private bool HasValidPanels() {
        return topPanel != null && bottomPanel != null;
    }

    private static void ResetPanel(Transform panel, Vector3 closedLocalPosition, Vector3 closedLocalScale) {
        if (panel == null)
            return;
        panel.localPosition = closedLocalPosition;
        panel.localScale = closedLocalScale;
    }

    private float GetAxisValue(Vector3 value) {
        switch (retractAxis) {
            case SpawnRobotCollectorPanelAxis.LocalY:
                return value.y;
            case SpawnRobotCollectorPanelAxis.LocalZ:
                return value.z;
            default:
                return value.x;
        }
    }

    private void SetAxisValue(ref Vector3 value, float axisValue) {
        switch (retractAxis) {
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

    private void ClearActiveReferences() {
        activeMission = null;
        activeCollector = null;
        activeMemory = null;
        activeBrain = null;
        activeState = null;
        activeLifecycle = null;
        abortDeadline = 0f;
        lastIntakeState = null;
    }
}
