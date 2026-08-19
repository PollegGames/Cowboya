using UnityEngine;

/// <summary>
/// Creates the laboratory-only DocBot instance and owns the atomic lifetime of
/// one recurring laboratory visit.
/// </summary>
[DisallowMultipleComponent]
public sealed class LaboratoryManager : MonoBehaviour {
    [SerializeField] private DocBotController docBotPrefab;
    [SerializeField] private Transform docBotSpawnPoint;
    [SerializeField] private CubePickup whiteCubePrefab;
    [SerializeField] private LaboratoryCollectedCubeSpawner collectedCubeSpawner;

    private static LaboratoryManager activeInstance;

    private LaboratoryProgress progress;
    private DocBotController docBotInstance;
    private int visitId = -1;
    private bool visitInitialized;
    private bool visitFinalized;

    public static LaboratoryManager ActiveInstance => activeInstance;
    public LaboratoryProgress Progress => progress;
    public DocBotController DocBotInstance => docBotInstance;
    public bool VisitInitialized => visitInitialized;
    public bool VisitFinalized => visitFinalized;

    /// <summary>
    /// Assigns the laboratory assets and the room-authored centre spawn point.
    /// </summary>
    public void Configure(
        DocBotController scientistPrefab,
        Transform spawnPoint,
        CubePickup rewardPrefab) {
        docBotPrefab = scientistPrefab;
        docBotSpawnPoint = spawnPoint;
        whiteCubePrefab = rewardPrefab;
    }

    private void Awake() {
        if (activeInstance != null && activeInstance != this) {
            Debug.LogError("Only one LaboratoryManager may be active in a laboratory visit.", this);
            enabled = false;
            return;
        }

        activeInstance = this;
    }

    private void Start() {
        InitializeVisit();
    }

    private void OnDestroy() {
        if (activeInstance == this) {
            activeInstance = null;
        }
    }

    /// <summary>
    /// Initializes the visit once. This is public for deterministic setup tests and
    /// for a future SceneInitiator-owned orchestration path.
    /// </summary>
    public bool InitializeVisit() {
        if (visitInitialized) {
            return true;
        }

        if (docBotPrefab == null || docBotSpawnPoint == null) {
            Debug.LogError("LaboratoryManager requires a DocBot prefab and spawn point.", this);
            return false;
        }

        ResolveProgress();
        if (progress == null) {
            Debug.LogError("LaboratoryManager could not resolve LaboratoryProgress.", this);
            return false;
        }

        visitId = ResolveVisitId();
        if (progress.HasActiveVisit && progress.ActiveVisitId != visitId) {
            Debug.LogError(
                $"LaboratoryManager found active visit {progress.ActiveVisitId} "
                + $"while initializing visit {visitId}.",
                this);
            return false;
        }

        if (!progress.HasActiveVisit && !progress.TryBeginVisit(visitId)) {
            Debug.LogError($"LaboratoryManager could not begin visit {visitId}.", this);
            return false;
        }

        docBotInstance = Instantiate(
            docBotPrefab,
            docBotSpawnPoint.position,
            docBotSpawnPoint.rotation,
            transform);
        docBotInstance.name = "DocBot";
        docBotInstance.InitializeForVisit(progress);

        if (progress.AvailableWhiteCubeCount > 0) {
            PresentOneWhiteCube();
        }

        PresentCollectedCubes();

        visitInitialized = true;
        return true;
    }

    /// <summary>
    /// Finalizes the active manager when leaving a laboratory. Outside a laboratory
    /// run step this method intentionally succeeds without doing work.
    /// </summary>
    public static bool TryFinalizeActiveVisit(Collider2D playerCollider) {
        _ = playerCollider;
        RunProgressManager runProgress = RunProgressManager.Instance;
        if (runProgress != null
            && runProgress.CurrentStepKind != RunStepKind.Laboratory) {
            return true;
        }

        LaboratoryManager manager = activeInstance;
        if (manager == null) {
            manager = FindFirstObjectByType<LaboratoryManager>();
        }

        if (manager == null) {
            if (runProgress == null) {
                return true;
            }

            Debug.LogError("Laboratory exit was requested without an active LaboratoryManager.");
            return false;
        }

        return manager.TryFinalizeVisit();
    }

    /// <summary>
    /// Atomically commits visit-dependent scientist consequences exactly once.
    /// </summary>
    public bool TryFinalizeVisit() {
        if (visitFinalized) {
            return true;
        }

        if (!visitInitialized && !InitializeVisit()) {
            return false;
        }

        if (!progress.TryFinalizeVisit()) {
            bool alreadyFinalized = !progress.HasActiveVisit
                && progress.LastFinalizedVisitId == visitId;
            visitFinalized = alreadyFinalized;
            return alreadyFinalized;
        }

        visitFinalized = true;
        return true;
    }

    private void PresentOneWhiteCube() {
        if (whiteCubePrefab == null || docBotInstance == null || docBotInstance.ItemHolder == null) {
            Debug.LogWarning("A white cube reward is pending but its prefab or DocBot hand holder is missing.", this);
            return;
        }

        if (!docBotInstance.ItemHolder.TryPresentWhiteCube(whiteCubePrefab)) {
            Debug.LogWarning("DocBot could not present the pending white cube in either hand.", this);
            return;
        }

        CubePickup cube = docBotInstance.ItemHolder.PresentedWhiteCube;
        LaboratoryWhiteCubeReward reward = cube.GetComponent<LaboratoryWhiteCubeReward>();
        if (reward == null) {
            reward = cube.gameObject.AddComponent<LaboratoryWhiteCubeReward>();
        }
        reward.Configure(progress);
    }

    private void PresentCollectedCubes() {
        if (collectedCubeSpawner == null) {
            collectedCubeSpawner = FindFirstObjectByType<LaboratoryCollectedCubeSpawner>();
        }

        if (collectedCubeSpawner != null) {
            if (!collectedCubeSpawner.InitializeForVisit(progress, visitId)) {
                Debug.LogError(
                    "LaboratoryManager could not initialize the collected-cube presentation.",
                    collectedCubeSpawner);
            }
            return;
        }

        int[] freeCubeCounts = progress.GetLaboratoryFreeCubeSnapshot();
        for (int i = 0; i < freeCubeCounts.Length; i++) {
            if (freeCubeCounts[i] <= 0) {
                continue;
            }

            Debug.LogError(
                "LaboratoryManager has free cubes but no LaboratoryCollectedCubeSpawner "
                + "is active in the laboratory scene.",
                this);
            return;
        }
    }

    private void ResolveProgress() {
        if (RunProgressManager.Instance != null) {
            progress = RunProgressManager.Instance.LaboratoryProgress;
        }

        // Direct scene play remains useful for authoring the room and prefab.
        if (progress == null) {
            progress = new LaboratoryProgress();
        }
    }

    private int ResolveVisitId() {
        if (RunProgressManager.Instance != null
            && RunProgressManager.Instance.CurrentRunStepIndex >= 0) {
            return RunProgressManager.Instance.CurrentRunStepIndex;
        }

        return Mathf.Max(0, progress.LastFinalizedVisitId + 1);
    }
}
