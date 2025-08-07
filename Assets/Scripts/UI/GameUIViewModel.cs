using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

        pauseButton.clicked += PauseGame;
        resumeButton.clicked += ResumeGame;
        restartButton.clicked += RestartGame;
        mainMenuButton.clicked += GoToMainMenu;
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
        var startMessage = GameMessages.System.Start;
        string levelPrefix = string.Empty;

        if (RunProgressManager.Instance != null)
        {
            int index = RunProgressManager.Instance.CurrentLevelIndex;
            var realLevel = index - 1;
            levelPrefix = index == 1 ? "Level Tutorial. " : $"Level {realLevel}: ";
        }

        var msg = new GameMessage(levelPrefix + startMessage.Text, startMessage.Speaker);
        MessageService.Instance?.ShowMessage(msg);
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
        string targetScene = RunProgressManager.Instance != null ? "GameOverScene" : "MenuScene";
        SceneController.instance.LoadScene(targetScene);
    }

    private void UpdateMoralityLabel()
    {
        if (robotBehaviour != null && robotBehaviour.Stats != null)
        {
            int currentMorality = Mathf.RoundToInt(robotBehaviour.Stats.Morality);
            ui.Q<Label>("moralityLabel").text = $"Morality: {currentMorality}";
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

    public void SetMiniMapTexture(MapManager mapManagerInstance)
    {
        miniMapPreviewInstance = Instantiate(miniMapPreviewPrefab);
        float worldWidth = config.gridWidth * mapManagerInstance.cellWidth;
        float worldHeight = config.gridHeight * mapManagerInstance.cellHeight;
        
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
        float spacing = 5f;
        previewVE.style.paddingRight = new StyleLength(spacing);
        previewVE.style.paddingLeft = new StyleLength(spacing);
        previewVE.style.marginRight = new StyleLength(spacing);
        previewVE.style.marginLeft = new StyleLength(spacing);
    }

}
