using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;


/// <summary>
/// Gère la scène RunSetupScene : saisie des paramètres, validation, prévisualisation
/// et lancement du run.
/// </summary>
public class RunSetupManager : MonoBehaviour
{
    // =============== Inspector
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Preview 3D réel")]
    [SerializeField] private GameObject miniMapPreviewPrefab; // prefab (MiniMapCamera + MapManager, etc.)
    private GameObject miniMapPreviewInstance;                 // instance runtime
    [SerializeField] private RenderTexture miniMapRT;

    [Header("Config SO (persistant)")]
    [SerializeField] private RunMapConfigSO config;
    [SerializeField] private VictorySetup victorySetup;

    [Header("Services")]
    [SerializeField] private FactoryManager factoryManagerPrefab;
    private FactoryManager factoryManagerInstance;
    [SerializeField] private MapManager mapManagerPrefab;
    private MapManager mapManagerInstance;
    [SerializeField] private WaypointService waypointServicePrefab;

    [SerializeField] private IEnemiesSpawner enemiesSpawner;
    private WaypointService waypointServiceInstance;

    // =============== Références internes UI
    private VisualElement root;
    private VisualElement previewVE;      // <VisualElement name="preview">
    private IntegerField widthField,
                         heightField,
                         poiField,
                         blockedField,
                         enemyCountField,
                         bossCountField;
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

        var enemyMinusButton = root.Q<Button>("enemy-minus-button");
        var enemyPlusButton = root.Q<Button>("enemy-plus-button");

        var bossMinusButton = root.Q<Button>("boss-minus-button");
        var bossPlusButton = root.Q<Button>("boss-plus-button");

        // Récupère toutes les références
        widthField = root.Q<IntegerField>("width-field");
        heightField = root.Q<IntegerField>("height-field");
        poiField = root.Q<IntegerField>("poi-field");
        blockedField = root.Q<IntegerField>("blocked-field");
        enemyCountField = root.Q<IntegerField>("enemy-count-field");
        bossCountField = root.Q<IntegerField>("boss-count-field");
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

        enemyMinusButton.clicked += () => ChangeFieldValue(enemyCountField, -1, 0);
        enemyPlusButton.clicked += () => ChangeFieldValue(enemyCountField, +1);

        bossMinusButton.clicked += () => ChangeFieldValue(bossCountField, -1, 0);
        bossPlusButton.clicked += () => ChangeFieldValue(bossCountField, +1);
        validateButton.clicked += Validate;
        runButton.clicked += StartRun;
        randomSeedToggle.RegisterValueChangedCallback(e => seedField.SetEnabled(!e.newValue));

        runButton.SetEnabled(false);
        statusLabel.text = "Waiting for validation…";

        // Si config existante, pré-remplissage
        if (config != null)
        {
            widthField.value = config.gridWidth;
            heightField.value = config.gridHeight;
            poiField.value = config.poiCount;
            blockedField.value = config.blockedCount;
            enemyCountField.value = config.workersCount;
            bossCountField.value = config.enemiesCount;
            seedField.value = config.seed;

            // Initialiser “Cells Available” à la valeur correcte
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
    /// Recalcule “Cells Available” à chaque changement de width/height,
    /// puis plafonne automatiquement poiField et blockedField.
    /// </summary>
    private void UpdateCellsAndConstraints()
    {
        // 1) Récupérer width & height et calculer cellsAvailable = (w*h)/2 - 2
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;

        int cellsAvailable = totalCells - 4;
        if (cellsAvailable < 0) cellsAvailable = 0;

        // 2) Mettre à jour le label
        cellsAvailableLabel.text = $"Cells Available: {cellsAvailable}";

        // 3) Plafonner poi & blocked selon ce nouveau cellsAvailable
        int poi = Mathf.Max(1, poiField.value);
        int blocked = Mathf.Max(0, blockedField.value);

        // poi ≤ cellsAvailable - blocked
        int maxPoi = cellsAvailable - blocked;
        if (poi > maxPoi) poi = maxPoi;

        // blocked ≤ cellsAvailable - poi (après ajustement de poi)
        int maxBlocked = cellsAvailable - poi;
        if (blocked > maxBlocked) blocked = maxBlocked;

        // 4) Réassigner dans l’UI (pour forcer la correction visuelle)
        poiField.value = poi;
        blockedField.value = blocked;
    }

    /// <summary>
    /// Méthode appelée quand l’utilisateur modifie poiField ou blockedField à la main :
    /// on s’assure que poi ≤ cellsAvailable - blocked et blocked ≤ cellsAvailable - poi.
    /// </summary>
    private void ClampPoiAndBlocked()
    {
        // Récupérer current cellsAvailable depuis le label (ou recalculer)
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;
        int cellsAvailable = totalCells - 4;
        if (cellsAvailable < 0) cellsAvailable = 0;

        // Lecture actuelle de poi & blocked
        int poi = Mathf.Max(1, poiField.value);
        int blocked = Mathf.Max(0, blockedField.value);

        // Appliquer les mêmes contraintes
        int maxPoi = cellsAvailable - blocked;
        if (poi > maxPoi) poi = maxPoi;

        int maxBlocked = cellsAvailable - poi;
        if (blocked > maxBlocked) blocked = maxBlocked;

        // Réinjecter
        poiField.value = poi;
        blockedField.value = blocked;
    }

    // =====================================================================
    // VALIDATION & PREVIEW
    // =====================================================================
    private void Validate()
    {
        // 1) Lecture + garde-fous
        int w = Mathf.Max(2, widthField.value);
        int h = Mathf.Max(2, heightField.value);
        int totalCells = w * h;

        // Calcul “cellsAvailable” : moitié des cellules totales – 2 (start + end)
        int cellsAvailable = totalCells - 4;
        if (cellsAvailable < 0) cellsAvailable = 0;

        // Met à jour le label
        cellsAvailableLabel.text = $"Cells Available: {cellsAvailable}";

        // Lecture initiale des champs POI / Blocked
        int poi = Mathf.Max(1, poiField.value);
        int blocked = Mathf.Max(0, blockedField.value);

        // On contraint automatiquement :
        //  - poi ≤ cellsAvailable - blocked
        int maxPoi = cellsAvailable - blocked;
        if (poi > maxPoi) poi = maxPoi;
        //  - blocked ≤ cellsAvailable - poi
        int maxBlocked = cellsAvailable - poi;
        if (blocked > maxBlocked) blocked = maxBlocked;

        // On réaffecte dans l’UI (pour corriger d’éventuels dépassements)
        poiField.value = poi;
        blockedField.value = blocked;

        // Lecture et garde-fous pour le reste
        int enemies = Mathf.Max(0, enemyCountField.value);
        int boss = Mathf.Max(0, bossCountField.value);

        // Vérifications supplémentaires
        bool isValid = true;

        // 2) S’assurer que poi + blocked ne dépassent pas cellsAvailable
        if (poi + blocked > cellsAvailable) isValid = false;

        // 3) S’assurer que le nb d’ennemis ≤ (cellsAvailable - poi - blocked)
        int emptyCells = cellsAvailable - poi - blocked;

        // Highlight si erreur
        void Mark(VisualElement f, bool ok) => f.style.borderBottomColor = ok ? Color.clear : Color.red;
        Mark(poiField, poi + blocked <= cellsAvailable);
        Mark(blockedField, poi + blocked <= cellsAvailable);
        Mark(enemyCountField, enemies <= emptyCells);

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
        config.workersCount = enemies;
        config.enemiesCount = boss;
        config.seed = randomSeedToggle.value
                                ? Random.Range(0, 999999).ToString()
                                : seedField.value;

        // Ré-affecte le seed (en cas de random)
        seedField.value = config.seed;

        // 5) Prévisualisation réelle
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
    /// Met à jour le texte du Label “Cells Available” en fonction des champs width/height/poi/blocked actuels.
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
    /// Instancie le prefab de preview (MapManager + MiniMapCamera) et alimente la RT.
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
        enemiesSpawner.Initialize(mapManagerInstance, waypointServiceInstance, null, null, factoryManagerInstance.SecurityManager);
        
        miniMapPreviewInstance = Instantiate(miniMapPreviewPrefab);

        // 1) Génère la grille       
        if (mapManagerInstance != null)
            mapManagerInstance.BuildFromConfig(config);
        if (factoryManagerInstance != null)
        {
            factoryManagerInstance.Initialize(mapManagerInstance, waypointServiceInstance, victorySetup, enemiesSpawner);
        }
        float worldWidth = config.gridWidth * mapManagerInstance.cellWidth;
        float worldHeight = config.gridHeight * mapManagerInstance.cellHeight;

        // 2) Cadre la caméra
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

        // 3) Capture en Texture2D via Coroutine
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
        previewVE.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        BackgroundPosition center = new BackgroundPosition(BackgroundPositionKeyword.Center);
    }

    // =====================================================================
    // LAUNCH RUN
    // =====================================================================
    private void StartRun()
    {
        SceneManager.LoadScene("MapGeneration");
    }
}
