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

        var workerMinusButton = root.Q<Button>("worker-minus-button");
        var workerPlusButton = root.Q<Button>("worker-plus-button");

        var enemyMinusButton = root.Q<Button>("enemy-minus-button");
        var enemyPlusButton = root.Q<Button>("enemy-plus-button");

        // Grab all UI references
        widthField = root.Q<IntegerField>("width-field");
        heightField = root.Q<IntegerField>("height-field");
        poiField = root.Q<IntegerField>("poi-field");
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

        poiField.RegisterValueChangedCallback(evt => ClampPoi());

        // Callbacks
        widthMinusButton.clicked += () => ChangeFieldValue(widthField, -1, 2);
        widthPlusButton.clicked += () => ChangeFieldValue(widthField, +1);

        heightMinusButton.clicked += () => ChangeFieldValue(heightField, -1, 2);
        heightPlusButton.clicked += () => ChangeFieldValue(heightField, +1);

        poiMinusButton.clicked += () => ChangeFieldValue(poiField, -1, 1, ClampPoi);
        poiPlusButton.clicked += () => ChangeFieldValue(poiField, +1, 1, ClampPoi);

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
    /// then automatically clamps poiField.
    /// </summary>
    private void UpdateCellsAndConstraints()
    {
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;

        int cellsAvailable = totalCells - 2;
        if (cellsAvailable < 0) cellsAvailable = 0;

        // 2) Update the label
        cellsAvailableLabel.text = $"Cells Available: {cellsAvailable}";

        int poi = Mathf.Max(1, poiField.value);
        if (poi > cellsAvailable) poi = cellsAvailable;

        // 4) Reassign in the UI (forces visual correction)
        poiField.value = poi;
    }

    /// <summary>
    /// Called when the user manually edits poiField.
    /// </summary>
    private void ClampPoi()
    {
        // Retrieve current cellsAvailable from the label (or recompute)
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;
        int cellsAvailable = totalCells - 2;
        if (cellsAvailable < 0) cellsAvailable = 0;

        int poi = Mathf.Max(1, poiField.value);
        if (poi > cellsAvailable) poi = cellsAvailable;

        // Write back
        poiField.value = poi;
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

        int cellsAvailable = totalCells - 2;
        if (cellsAvailable < 0) cellsAvailable = 0;

        // Update the label
        cellsAvailableLabel.text = $"Cells Available: {cellsAvailable}";

        int poi = Mathf.Max(1, poiField.value);
        if (poi > cellsAvailable) poi = cellsAvailable;

        // Reassign in the UI (fix possible overflow)
        poiField.value = poi;

        // Read and clamp the remaining fields
        int workers = Mathf.Max(0, workersCountField.value);
        int enemies = Mathf.Max(0, enemyCountField.value);

        bool isValid = poi <= cellsAvailable;
        int workCells = cellsAvailable - poi;

        // Highlight si erreur
        void Mark(VisualElement f, bool ok) => f.style.borderBottomColor = ok ? Color.clear : Color.red;
        Mark(poiField, poi <= cellsAvailable);
        Mark(workersCountField, workers <= workCells);

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
    /// height and POI values.
    /// </summary>
    private void UpdateCellsAvailableLabel()
    {
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;
        int cellsAvailable = totalCells - 2;
        if (cellsAvailable < 0) cellsAvailable = 0;
        cellsAvailableLabel.text = $"Cells Available: {cellsAvailable}";
    }

    /// <summary>
    /// Instantiates the preview prefab (MapManager + MiniMapCamera) and feeds the render texture.
    /// </summary>
    private void ShowRealPreview()
    {
        DestroyOldInstances();
        RobotDomainEventAdapter.EnsureInScene();
        factoryManagerInstance = Instantiate(factoryManagerPrefab);
        mapManagerInstance = Instantiate(mapManagerPrefab);
        var gridBuilder = mapManagerInstance.gameObject.AddComponent<GridBuilder>();
        var roomRenderer = mapManagerInstance.gameObject.AddComponent<RoomRenderer>();
        var roomProcessor = mapManagerInstance.gameObject.AddComponent<RoomProcessor>();
        mapManagerInstance.Construct(gridBuilder, roomRenderer, roomProcessor);
        waypointServiceInstance = Instantiate(waypointServicePrefab);
        enemiesSpawnerInstance = Instantiate(enemiesSpawnerPrefab);
        enemiesSpawnerInstance.SetDropContainer(factoryManagerInstance.transform);
        enemiesSpawnerInstance.Initialize(mapManagerInstance, waypointServiceInstance, null, null, factoryManagerInstance, factoryManagerInstance.SecurityManager, null, null);

        miniMapPreviewInstance = Instantiate(miniMapPreviewPrefab);

        // 1) Build the grid
        if (mapManagerInstance != null)
            mapManagerInstance.BuildFromConfig(config);
        if (factoryManagerInstance != null)
        {
            factoryManagerInstance.Initialize(mapManagerInstance, waypointServiceInstance, victorySetup, enemiesSpawnerInstance);
        }
        Bounds bounds = mapManagerInstance.GetGridWorldBounds();

        // 2) Frame the camera
        var cam = miniMapPreviewInstance.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;
            float aspect = (float)miniMapRT.width / miniMapRT.height;
            float halfH = bounds.size.y * 0.5f;
            float halfWAsH = (bounds.size.x * 0.5f) / aspect;
            float padding = 0.05f;
            cam.orthographicSize = Mathf.Max(halfH, halfWAsH) * (1f + padding);

            Vector3 pos = bounds.center;
            pos.z = -10f;
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.identity;

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
