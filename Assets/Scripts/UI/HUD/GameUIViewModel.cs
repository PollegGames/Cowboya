using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class GameUIViewModel : MonoBehaviour
{
    public VisualElement ui;
    private RobotStateController robotBehaviour;
    [SerializeField] private RunMapConfigSO config;
    private VisualElement gameHUDContainer;

    [Header("UI MINIMAP")]
    [SerializeField] private GameObject miniMapPreviewPrefab; // prefab (MiniMapCamera + MapManager, etc.)
    private GameObject miniMapPreviewInstance;                 // instance runtime
    [SerializeField] private RenderTexture miniMapRT;
    private VisualElement previewVE;      // <VisualElement name="preview">
    private bool minimapCaptured;
    private bool minimapConfigured;

    [Header("PAUSE MENU")]

    private VisualElement pauseMenuContainer;
    private Button pauseButton;
    private Button resumeButton;
    private Button restartButton;
    private Button mainMenuButton;

    private void Awake()
    {
        ui = GetComponent<UIDocument>().rootVisualElement;

        previewVE = ui.Q<VisualElement>("miniMapPreview");

        var service = MessageService.Instance;
        if (service == null)
        {
            service = gameObject.AddComponent<MessageService>();
        }
        service.Initialize(ui);

        pauseMenuContainer = ui.Q<VisualElement>("PauseMenu");
        gameHUDContainer = ui.Q<VisualElement>("GameHUD");
        pauseButton = ui.Q<Button>("pauseButton");
        resumeButton = ui.Q<Button>("resumeButton");
        restartButton = ui.Q<Button>("restartButton");
        mainMenuButton = ui.Q<Button>("mainMenuButton");

        pauseButton?.AddToClassList("hud-button");
        resumeButton?.AddToClassList("hud-button");
        restartButton?.AddToClassList("hud-button");
        mainMenuButton?.AddToClassList("hud-button");

        pauseButton.clicked += PauseGame;
        resumeButton.clicked += ResumeGame;
        restartButton.clicked += RestartGame;
        mainMenuButton.clicked += GoToMainMenu;

        minimapCaptured = false;
        minimapConfigured = false;
    }

    void PauseGame()
    {
        Time.timeScale = 0;
        SetPauseMenuVisible(true);
    }

    void ResumeGame()
    {
        Debug.Log("Resume clicked");
        Time.timeScale = 1;
        SetPauseMenuVisible(false);
    }

    void RestartGame()
    {
        Debug.Log("Restart clicked");
        Time.timeScale = 1;
        RunProgressManager.Instance?.RestartRun();
    }
    void GoToMainMenu()
    {
        Debug.Log("MainMenu clicked");
        Time.timeScale = 1;
        SceneController.instance?.LoadScene("MenuScene");
    }

    void SetPauseMenuVisible(bool visible)
    {
        if (pauseMenuContainer != null)
        {
            Debug.Log($"Setting pause menu visibility to {visible}");
            pauseMenuContainer.style.display = visible
            ? DisplayStyle.Flex
            : DisplayStyle.None;

            gameHUDContainer.style.display = visible
            ? DisplayStyle.None
            : DisplayStyle.Flex;
        }
        else
        {
            Debug.LogError("Pause menu container is null!");
        }
    }
    private void Start()
    {
        var hintManager = FindFirstObjectByType<HintManager>();

        if (hintManager != null)
        {
            hintManager.QueueHint(new GameMessage("Move with [A][D]...", MessageSpeaker.Narrator));
            hintManager.QueueHint(new GameMessage("Energy powers your actions.", MessageSpeaker.Narrator));
        }
    }

    public void SetPlayer(RobotStateController robot)
    {
        if (robot != null && robot.Stats != null)
        {
            robotBehaviour = robot; // Store the instance reference
            RobotStats robotInfo = robot.Stats;

            // Subscribe to PlayerStats events
            robotInfo.OnEnergyChanged += UpdateEnergyBar;
            robotInfo.OnHealthChanged += UpdateHealthBar;
            robotInfo.OnMoralityChanged += UpdateMoralityLabel;

            // Listen for player state changes
            robotBehaviour.OnStateChanged += HandleRobotStateChange;

            // Initial UI update
            UpdateEnergyBar();
            UpdateHealthBar();
            UpdateMoralityLabel();

            Debug.Log("Health and energy bars bound to PlayerStateController.");
            RefreshMinimapTexture();
        }
        else
        {
            Debug.LogError("PlayerStateController or PlayerStats is null!");
        }
    }

    private void HandleRobotStateChange(RobotState newState)
    {
        if (newState == RobotState.Dead)
        {
            MessageService.Instance?.ShowMessage(GameMessages.System.GameOver);
            StartCoroutine(LoadSceneAfterDelay());
        }
    }

    private IEnumerator LoadSceneAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        string targetScene = RunProgressManager.Instance != null ? "MenuScene" : "MenuScene";
        SceneController.instance.LoadScene(targetScene);
    }

    private void UpdateMoralityLabel()
    {
        if (robotBehaviour != null && robotBehaviour.Stats != null)
        {
            int currentMorality = Mathf.RoundToInt(robotBehaviour.Stats.Morality);
            var moralityLabel = ui.Q<Label>("moralityLabel");
            moralityLabel.text = $"Morality: {currentMorality}";

            // Remove color classes before setting the new one
            moralityLabel.RemoveFromClassList("morality-positive");
            moralityLabel.RemoveFromClassList("morality-negative");

            if (currentMorality > 0)
                moralityLabel.AddToClassList("morality-positive");
            else if (currentMorality < 0)
                moralityLabel.AddToClassList("morality-negative");
            // No class for zero; default color from USS
        }
    }

    private void UpdateEnergyBar()
    {
        if (robotBehaviour != null && robotBehaviour.Stats != null)
        {
            float currentEnergy = robotBehaviour.Stats.CurrentEnergy;
            ui.Q<EnergyBar>().currentEnergy = currentEnergy; // Assuming EnergyBar is a VisualElement
            ui.Q<EnergyBar>().MarkDirtyRepaint();
            ui.Q<Label>("energyValueLabel").text = Mathf.RoundToInt(currentEnergy).ToString();
        }
    }

    private void UpdateHealthBar()
    {
        if (robotBehaviour != null && robotBehaviour.Stats != null)
        {
            float currentHealth = robotBehaviour.Stats.CurrentHealth;
            ui.Q<HealthBar>().currentHealth = currentHealth; // Assuming HealthBar is a VisualElement
            ui.Q<HealthBar>().MarkDirtyRepaint();
            ui.Q<Label>("healthValueLabel").text = Mathf.RoundToInt(currentHealth).ToString();
        }
    }

    /// <summary>
    /// Configures the minimap camera to frame the generated map.
    /// </summary>
    public void SetMiniMapTexture(MapManager mapManagerInstance)
    {
        if (mapManagerInstance == null)
            return;

        Bounds bounds = mapManagerInstance.GetGridWorldBounds();
        SetMiniMapTexture(bounds);
    }

    public void SetMiniMapTextureFromScene()
    {
        if (!TryGetActiveSceneBounds(out Bounds bounds))
            return;

        SetMiniMapTexture(bounds);
    }

    private void SetMiniMapTexture(Bounds bounds)
    {
        minimapConfigured = true;
        miniMapPreviewInstance = Instantiate(miniMapPreviewPrefab);

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

            cam.targetTexture = miniMapRT;

        }
        CaptureMinimapOnce();
    }

    private bool TryGetActiveSceneBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;
        int mapPreviewLayer = LayerMask.NameToLayer("MapPreview");
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            if (root == null)
                continue;

            if (root.GetComponentInChildren<GameUIViewModel>() != null)
                continue;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: false);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || renderer.gameObject.name.Contains("Camera"))
                    continue;

                if (mapPreviewLayer >= 0 && renderer.gameObject.layer != mapPreviewLayer)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            Debug.LogWarning("GameUIViewModel: static minimap skipped because no MapPreview renderers were found.");
        }

        return hasBounds;
    }

    public void RefreshMinimapTexture()
    {
        CaptureMinimapOnce();
    }

    private void OnDestroy()
    {
        if (robotBehaviour != null)
        {
            robotBehaviour.OnStateChanged -= HandleRobotStateChange;
            if (robotBehaviour.Stats != null)
            {
                robotBehaviour.Stats.OnMoralityChanged -= UpdateMoralityLabel;
            }
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
    }

    private void CaptureMinimapOnce()
    {
        if (!minimapConfigured)
            return;

        if (minimapCaptured)
            return;
        minimapCaptured = true;
        StartCoroutine(CaptureRTToUI());
    }

}
