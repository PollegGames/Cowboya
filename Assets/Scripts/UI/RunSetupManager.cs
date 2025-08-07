using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;


/// <summary>
/// Manages the RunSetupScene: collects parameters, validates them, shows a
/// preview and launches the run.
/// </summary>
public class RunSetupManager : MonoBehaviour
{
    // =============== Inspector
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Actual 3D Preview")]
    [SerializeField] private GameObject miniMapPreviewPrefab; // prefab (MiniMapCamera + MapManager, etc.)
    private GameObject miniMapPreviewInstance;                 // instance runtime
    [SerializeField] private RenderTexture miniMapRT;

    [Header("Config SO (persistent)")]
    [SerializeField] private RunMapConfigSO config;
    [SerializeField] private VictorySetup victorySetup;

    [Header("Services")]
    [SerializeField] private FactoryManager factoryManagerPrefab;
    private FactoryManager factoryManagerInstance;
    [SerializeField] private MapManager mapManagerPrefab;
    private MapManager mapManagerInstance;
    [SerializeField] private WaypointService waypointServicePrefab;

    [SerializeField] private EnemiesSpawner enemiesSpawnerPrefab;
    private IEnemiesSpawner enemiesSpawnerInstance;
    private WaypointService waypointServiceInstance;

    // =============== Internal UI references
    private VisualElement root;
    private VisualElement previewVE;      // <VisualElement name="preview">
    private IntegerField widthField,
                         heightField,
                         poiField,
                         blockedField,
                         workersCountField,
                         enemyCountField;
    private TextField seedField;
    private Toggle randomSeedToggle;
    private Button validateButton, runButton;
    private Label statusLabel, cellsAvailableLabel;

    // =====================================================================
    // LIFECYCLE
    // =====================================================================
    private void Awake()
    {
        root = uiDocument.rootVisualElement;
        previewVE = root.Q<VisualElement>("preview");
        BindUI();
    }

    // =====================================================================
    // UI BINDING
    // =====================================================================
    private void BindUI()
    {
        // Buttons for width
        var widthMinusButton = root.Q<Button>("width-minus-button");
        var widthPlusButton = root.Q<Button>("width-plus-button");
        // Same for other fields...
        var heightMinusButton = root.Q<Button>("height-minus-button");
        var heightPlusButton = root.Q<Button>("height-plus-button");

        var poiMinusButton = root.Q<Button>("poi-minus-button");
        var poiPlusButton = root.Q<Button>("poi-plus-button");

        var blockedMinusButton = root.Q<Button>("blocked-minus-button");
        var blockedPlusButton = root.Q<Button>("blocked-plus-button");

        var workerMinusButton = root.Q<Button>("worker-minus-button");
        var workerPlusButton = root.Q<Button>("worker-plus-button");

        var enemyMinusButton = root.Q<Button>("enemy-minus-button");
        var enemyPlusButton = root.Q<Button>("enemy-plus-button");

        // Grab all UI references
        widthField = root.Q<IntegerField>("width-field");
        heightField = root.Q<IntegerField>("height-field");
        poiField = root.Q<IntegerField>("poi-field");
        blockedField = root.Q<IntegerField>("blocked-field");
        workersCountField = root.Q<IntegerField>("worker-count-field");
        enemyCountField = root.Q<IntegerField>("enemy-count-field");
        seedField = root.Q<TextField>("seed-field");
        randomSeedToggle = root.Q<Toggle>("random-seed-toggle");
        validateButton = root.Q<Button>("validate-button");
        runButton = root.Q<Button>("run-button");
        statusLabel = root.Q<Label>("status-label");
        cellsAvailableLabel = root.Q<Label>("cells-available-label");
        previewVE = root.Q<VisualElement>("preview");

        widthField.RegisterValueChangedCallback(evt => UpdateCellsAndConstraints());
        heightField.RegisterValueChangedCallback(evt => UpdateCellsAndConstraints());

        poiField.RegisterValueChangedCallback(evt => ClampPoiAndBlocked());
        blockedField.RegisterValueChangedCallback(evt => ClampPoiAndBlocked());

        // Callbacks
        widthMinusButton.clicked += () => ChangeFieldValue(widthField, -1, 2);
        widthPlusButton.clicked += () => ChangeFieldValue(widthField, +1);

        heightMinusButton.clicked += () => ChangeFieldValue(heightField, -1, 2);
        heightPlusButton.clicked += () => ChangeFieldValue(heightField, +1);

        poiMinusButton.clicked += () => ChangeFieldValue(poiField, -1, 1, ClampPoiAndBlocked);
        poiPlusButton.clicked += () => ChangeFieldValue(poiField, +1, 1, ClampPoiAndBlocked);

        blockedMinusButton.clicked += () => ChangeFieldValue(blockedField, -1, 0, ClampPoiAndBlocked);
        blockedPlusButton.clicked += () => ChangeFieldValue(blockedField, +1, 0, ClampPoiAndBlocked);

        workerMinusButton.clicked += () => ChangeFieldValue(workersCountField, -1, 0);
        workerPlusButton.clicked += () => ChangeFieldValue(workersCountField, +1);

        enemyMinusButton.clicked += () => ChangeFieldValue(enemyCountField, -1, 0);
        enemyPlusButton.clicked += () => ChangeFieldValue(enemyCountField, +1);
        validateButton.clicked += Validate;
        runButton.clicked += StartRun;
        randomSeedToggle.RegisterValueChangedCallback(e => seedField.SetEnabled(!e.newValue));

        runButton.SetEnabled(false);
        statusLabel.text = "Waiting for validation…";

        // Pre-fill if a configuration already exists
        if (config != null)
        {
            widthField.value = config.gridWidth;
            heightField.value = config.gridHeight;
            poiField.value = config.poiCount;
            blockedField.value = config.blockedCount;
            workersCountField.value = config.workersCount;
            enemyCountField.value = config.enemiesCount;
            seedField.value = config.seed;

            // Initialize "Cells Available" with the correct value
            UpdateCellsAndConstraints();
        }
    }

    private void ChangeFieldValue(IntegerField field, int delta, int min = int.MinValue, System.Action onChanged = null)
    {
        int newValue = Mathf.Max(min, field.value + delta);
        field.value = newValue;
        onChanged?.Invoke();
    }

    /// <summary>
    /// Recomputes "Cells Available" whenever width or height changes,
    /// then automatically clamps poiField and blockedField.
    /// </summary>
    private void UpdateCellsAndConstraints()
    {
        // 1) Read width & height and compute cellsAvailable = (w*h)/2 - 2
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;

        int cellsAvailable = totalCells - 4;
        if (cellsAvailable < 0) cellsAvailable = 0;

        // 2) Update the label
        cellsAvailableLabel.text = $"Cells Available: {cellsAvailable}";

        // 3) Clamp poi & blocked according to the new cellsAvailable
        int poi = Mathf.Max(1, poiField.value);
        int blocked = Mathf.Max(0, blockedField.value);

        // poi ≤ cellsAvailable - blocked
        int maxPoi = cellsAvailable - blocked;
        if (poi > maxPoi) poi = maxPoi;

        // blocked ≤ cellsAvailable - poi (after adjusting poi)
        int maxBlocked = cellsAvailable - poi;
        if (blocked > maxBlocked) blocked = maxBlocked;

        // 4) Reassign in the UI (forces visual correction)
        poiField.value = poi;
        blockedField.value = blocked;
    }

    /// <summary>
    /// Called when the user manually edits poiField or blockedField.
    /// Ensures poi ≤ cellsAvailable - blocked and blocked ≤ cellsAvailable - poi.
    /// </summary>
    private void ClampPoiAndBlocked()
    {
        // Retrieve current cellsAvailable from the label (or recompute)
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;
        int cellsAvailable = totalCells - 4;
        if (cellsAvailable < 0) cellsAvailable = 0;

        // Current values of poi and blocked
        int poi = Mathf.Max(1, poiField.value);
        int blocked = Mathf.Max(0, blockedField.value);

        // Apply the same constraints
        int maxPoi = cellsAvailable - blocked;
        if (poi > maxPoi) poi = maxPoi;

        int maxBlocked = cellsAvailable - poi;
        if (blocked > maxBlocked) blocked = maxBlocked;

        // Write back
        poiField.value = poi;
        blockedField.value = blocked;
    }

    // =====================================================================
    // VALIDATION & PREVIEW
    // =====================================================================
    private void Validate()
    {
        // 1) Read values with safety checks
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;

        // Compute "cellsAvailable": total cells minus 4 (start + end)
        int cellsAvailable = totalCells - 4;
        if (cellsAvailable < 0) cellsAvailable = 0;

        // Update the label
        cellsAvailableLabel.text = $"Cells Available: {cellsAvailable}";

        // Initial read of the POI / Blocked fields
        int poi = Mathf.Max(1, poiField.value);
        int blocked = Mathf.Max(0, blockedField.value);

        // Apply automatic constraints:
        //  - poi ≤ cellsAvailable - blocked
        int maxPoi = cellsAvailable - blocked;
        if (poi > maxPoi) poi = maxPoi;
        //  - blocked ≤ cellsAvailable - poi
        int maxBlocked = cellsAvailable - poi;
        if (blocked > maxBlocked) blocked = maxBlocked;

        // Reassign in the UI (fix possible overflow)
        poiField.value = poi;
        blockedField.value = blocked;

        // Read and clamp the remaining fields
        int workers = Mathf.Max(0, workersCountField.value);
        int enemies = Mathf.Max(0, enemyCountField.value);

        // Additional validation
        bool isValid = true;

        // 2) Ensure poi + blocked do not exceed cellsAvailable
        if (poi + blocked > cellsAvailable) isValid = false;

        // 3) Ensure enemy count ≤ (cellsAvailable - poi - blocked)
        int emptyCells = cellsAvailable - poi - blocked;

        // Highlight si erreur
        void Mark(VisualElement f, bool ok) => f.style.borderBottomColor = ok ? Color.clear : Color.red;
        Mark(poiField, poi + blocked <= cellsAvailable);
        Mark(blockedField, poi + blocked <= cellsAvailable);
        Mark(workersCountField, workers <= emptyCells);

        if (!isValid)
        {
            statusLabel.text = "Invalid inputs";
            runButton.SetEnabled(false);
            return;
        }

        // 4) Si tout est OK → persiste la config
        config.gridWidth = w;
        config.gridHeight = h;
        config.poiCount = poi;
        config.blockedCount = blocked;
        config.workersCount = workers;
        config.enemiesCount = enemies;
        config.seed = randomSeedToggle.value
                                ? Random.Range(0, 999999).ToString()
                                : seedField.value;

        // Write back the seed (when using random)
        seedField.value = config.seed;

        // 5) Create the real preview
        ShowRealPreview();

        if (miniMapPreviewInstance == null)
        {
            statusLabel.text = "No minimap";
            runButton.SetEnabled(false);
        }
        else
        {
            statusLabel.text = "Validated";
            runButton.SetEnabled(true);
        }
    }

    /// <summary>
    /// Updates the "Cells Available" label based on the current width,
    /// height, poi and blocked values.
    /// </summary>
    private void UpdateCellsAvailableLabel()
    {
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;
        int cellsAvailable = totalCells - 4;
        if (cellsAvailable < 0) cellsAvailable = 0;
        cellsAvailableLabel.text = $"Cells Available: {cellsAvailable}";
    }

    /// <summary>
    /// Instantiates the preview prefab (MapManager + MiniMapCamera) and feeds the render texture.
    /// </summary>
    private void ShowRealPreview()
    {
        DestroyOldInstances();
        factoryManagerInstance = Instantiate(factoryManagerPrefab);
        mapManagerInstance = Instantiate(mapManagerPrefab);
        var gridBuilder = mapManagerInstance.gameObject.AddComponent<GridBuilder>();
        var roomRenderer = mapManagerInstance.gameObject.AddComponent<RoomRenderer>();
        var roomProcessor = mapManagerInstance.gameObject.AddComponent<RoomProcessor>();
        mapManagerInstance.Construct(gridBuilder, roomRenderer, roomProcessor);
        waypointServiceInstance = Instantiate(waypointServicePrefab);
        enemiesSpawnerInstance = Instantiate(enemiesSpawnerPrefab);
        enemiesSpawnerInstance.SetDropContainer(factoryManagerInstance.transform);
        enemiesSpawnerInstance.Initialize(mapManagerInstance, waypointServiceInstance, null, null, factoryManagerInstance.SecurityManager, null);

        miniMapPreviewInstance = Instantiate(miniMapPreviewPrefab);

        // 1) Build the grid
        if (mapManagerInstance != null)
            mapManagerInstance.BuildFromConfig(config);
        if (factoryManagerInstance != null)
        {
            factoryManagerInstance.Initialize(mapManagerInstance, waypointServiceInstance, victorySetup, enemiesSpawnerInstance);
        }
        float worldWidth = config.gridWidth * mapManagerInstance.cellWidth;
        float worldHeight = config.gridHeight * mapManagerInstance.cellHeight;

        // 2) Frame the camera
        var cam = miniMapPreviewInstance.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;
            float aspectRatio = (float)miniMapRT.width / miniMapRT.height;
            float halfVertSize = worldHeight / 2f;
            float halfHorzSize = (worldWidth / 2f) / aspectRatio;
            float orthoSize = Mathf.Max(halfVertSize, halfHorzSize);
            cam.orthographicSize = orthoSize;

            cam.transform.position = new Vector3(
                worldWidth / 2f,
                worldHeight / 2f,
                -10f
            );

            // cam.targetTexture = miniMapRT;
        }

        // 3) Capture to Texture2D via coroutine
        StartCoroutine(CaptureRTToUI());
    }

    private void DestroyOldInstances()
    {
        if (factoryManagerInstance != null)
        {
            Destroy(factoryManagerInstance.gameObject);
            factoryManagerInstance = null;
        }
        if (mapManagerInstance != null)
        {
            Destroy(mapManagerInstance.gameObject);
            mapManagerInstance = null;
        }
        if (waypointServiceInstance != null)
        {
            Destroy(waypointServiceInstance.gameObject);
            waypointServiceInstance = null;
        }
        if (miniMapPreviewInstance != null)
        {
            Destroy(miniMapPreviewInstance);
            miniMapPreviewInstance = null;
        }
        if (enemiesSpawnerInstance != null)
        {
            enemiesSpawnerInstance = null;
        }
    }

    private IEnumerator CaptureRTToUI()
    {
        yield return new WaitForEndOfFrame();
        var tex = new Texture2D(miniMapRT.width, miniMapRT.height, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point };

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = miniMapRT;
        tex.ReadPixels(new Rect(0, 0, miniMapRT.width, miniMapRT.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        previewVE.style.backgroundImage = new StyleBackground(tex);
        previewVE.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
        BackgroundPosition center = new BackgroundPosition(BackgroundPositionKeyword.Center);
        previewVE.style.backgroundPositionX = center;
        previewVE.style.backgroundPositionY = center;
        float spacing = 5f;
        previewVE.style.paddingRight = new StyleLength(spacing);
        previewVE.style.paddingLeft = new StyleLength(spacing);
        previewVE.style.marginRight = new StyleLength(spacing);
        previewVE.style.marginLeft = new StyleLength(spacing);
    }

    // =====================================================================
    // LAUNCH RUN
    // =====================================================================

    [SerializeField] private RunProgressManager runProgressManagerPrefab;
    private void StartRun()
    {
        if (RunProgressManager.Instance == null && runProgressManagerPrefab != null)
        {
            Instantiate(runProgressManagerPrefab);
        }
        RunProgressManager.Instance.LoadStressTestLevel();
    }
}
