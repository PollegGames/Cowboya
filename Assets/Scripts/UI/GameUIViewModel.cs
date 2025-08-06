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

    [Header("UI MINIMAP")]
    [SerializeField] private GameObject miniMapPreviewPrefab; // prefab (MiniMapCamera + MapManager, etc.)
    private GameObject miniMapPreviewInstance;                 // instance runtime
    [SerializeField] private RenderTexture miniMapRT;
    private VisualElement previewVE;      // <VisualElement name="preview">

    [Header("PAUSE MENU")]
    [Tooltip("Drag your PauseMenuPrefab asset here")]
    [SerializeField] private GameObject pauseMenuPrefab;

    private PauseMenuUI _pauseMenuUI;  // runtime instance
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

          // 2) Instantiate the Pause‐Menu prefab under this GameObject
        if (pauseMenuPrefab != null)
        {
            var go = Instantiate(pauseMenuPrefab, transform);
            _pauseMenuUI = go.GetComponent<PauseMenuUI>();
            if (_pauseMenuUI == null)
                Debug.LogError("PauseMenuPrefab is missing a PauseMenuUI component!");

            // ensure it starts hidden
            _pauseMenuUI.Hide();
        }
        else
        {
            Debug.LogError("GameUIViewModel: pauseMenuPrefab not assigned in inspector!");
        }

        
        // 3) Wire the pause button in your HUD to open it
        var pauseButton = ui.Q<Button>("pauseButton");
        if (pauseButton != null && _pauseMenuUI != null)
        {
            pauseButton.clicked += () => _pauseMenuUI.Show();
        }
        else if (pauseButton == null)
        {
            Debug.LogWarning("GameUIViewModel: pauseButton not found in UXML!");
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
        }
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
        if (miniMapPreviewInstance != null)
        {
            Destroy(miniMapPreviewInstance);
            miniMapPreviewInstance = null;
        }

        miniMapPreviewInstance = Instantiate(miniMapPreviewPrefab);
        float worldWidth = config.gridWidth * mapManagerInstance.cellWidth;
        float worldHeight = config.gridHeight * mapManagerInstance.cellHeight;

        var cam = miniMapPreviewInstance.GetComponentInChildren<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;

            float aspectRatio = 1f;
            if (previewVE != null && previewVE.resolvedStyle.height != 0f)
            {
                aspectRatio = previewVE.resolvedStyle.width / previewVE.resolvedStyle.height;
            }

            float halfVertSize = worldHeight / 2f;
            float halfHorzSize = (worldWidth / 2f) / aspectRatio;
            float orthoSize = Mathf.Max(halfVertSize, halfHorzSize);
            cam.orthographicSize = orthoSize;

            cam.transform.position = new Vector3(
                worldWidth / 2f,
                worldHeight / 2f,
                -10f
            );
        }

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
        if (previewVE != null)
        {
            previewVE.style.backgroundImage = new StyleBackground(tex);
            previewVE.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
            BackgroundPosition center = new BackgroundPosition(BackgroundPositionKeyword.Center);
            previewVE.style.backgroundPositionX = center;
            previewVE.style.backgroundPositionY = center;
        }
        else
        {
            Debug.LogError("GameUIViewModel: previewVE is null.");
        }
    }

}
