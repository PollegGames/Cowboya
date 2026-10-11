using UnityEngine;
public class LevelEndVictoryTrigger : MonoBehaviour
{
    [SerializeField] private DoorController doorNext;
    [SerializeField] private VictorySetup victorySetup;
    [SerializeField] private PlayerRunStats runStats;
    [SerializeField] private PlayerSaveService saveService;

    private bool isVictoryDoor = false;
    private bool hasRequestedLevelTransition;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer doorSprite;   // assign in Inspector
    [Range(0f, 1f)] public float disabledAlpha = 0.3f;
    private void Awake()
    {
        ResolveRunStats();

        if (doorNext != null)
        {
            doorNext.SetVictoryTrigger(this);
        }

        if (saveService == null)
        {
            saveService = FindFirstObjectByType<PlayerSaveService>();
        }

        if (runStats == null)
        {
            Debug.LogError("LevelEndVictoryTrigger: PlayerRunStats reference is missing.");
        }
        if (saveService == null)
        {
            Debug.LogError("LevelEndVictoryTrigger: PlayerSaveService reference is missing.");
        }

        RefreshFromRoomProperties();
    }

    private void Start()
    {
        // Generated RoomProperties are assigned after prefab Awake calls. Start
        // runs after map construction, so the correct left/right exit is now known.
        RefreshFromRoomProperties();
    }

    /// <summary>
    /// Refreshes this trigger from the left/right victory flag on its room.
    /// </summary>
    public void RefreshFromRoomProperties()
    {
        if (doorNext != null)
        {
            doorNext.RefreshFromRoomProperties();
            isVictoryDoor = doorNext.isVictoryDoor;
        }

        UpdateVisual();
    }

    /// <summary>
    /// Returns whether this trigger controls the supplied door.
    /// </summary>
    public bool ControlsDoor(DoorController door)
    {
        return doorNext == door;
    }

    /// <summary>
    /// Returns whether at least one enabled save or kill goal has been reached.
    /// </summary>
    public bool IsVictoryConditionMet()
    {
        return victorySetup != null && victorySetup.HasMetAnyGoal();
    }

    /// <summary>
    /// Returns whether the controlled exit may open for the current run step.
    /// </summary>
    public bool IsExitUnlocked()
    {
        return IsAlwaysAllowedRunStep() || IsVictoryConditionMet();
    }

    private void UpdateVisual()
    {
        if (doorSprite == null) return;

        Color c = doorSprite.color;
        c.a = isVictoryDoor ? 1f : disabledAlpha;
        doorSprite.color = c;
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        ResolveRunStats();
        RefreshFromRoomProperties();

        if (collision.CompareTag("Player") && IsAlwaysAllowedRunStep() && !hasRequestedLevelTransition)
        {
            hasRequestedLevelTransition = true;
            if (!LaboratoryManager.TryFinalizeActiveVisit(collision))
            {
                hasRequestedLevelTransition = false;
                return;
            }
            CaptureAndSaveBeforeTransition(collision);
            LoadNextRunStep();
            return;
        }

        if (victorySetup != null)
        {
            bool isVictory = IsVictoryConditionMet();
            if (isVictoryDoor && isVictory && collision.CompareTag("Player") && !hasRequestedLevelTransition)
            {
                hasRequestedLevelTransition = true;
                if (!LaboratoryManager.TryFinalizeActiveVisit(collision))
                {
                    hasRequestedLevelTransition = false;
                    return;
                }
                CaptureAndSaveBeforeTransition(collision);
                LoadNextRunStep();
            }

        }
    }

    private bool IsAlwaysAllowedRunStep()
    {
        if (RunProgressManager.Instance == null)
            return false;

        RunStepKind kind = RunProgressManager.Instance.CurrentStepKind;
        return kind == RunStepKind.StaticLevel || kind == RunStepKind.Laboratory;
    }

    private void CaptureAndSaveBeforeTransition(Collider2D collision)
    {
        RobotStateController controller = collision.GetComponentInParent<RobotStateController>();
        CowboyGrabController grabController = collision.GetComponentInParent<CowboyGrabController>();
        Inventory inventory = collision.GetComponentInParent<Inventory>();

        bool clearedHands = false;
        if (grabController != null)
        {
            grabController.ReleaseAllImmediate();
            clearedHands = true;
        }
        else
        {
            Debug.LogWarning("LevelEndVictoryTrigger: GrabSystem or CowboyGrabController component is missing on Player or its parent.");
        }

        if (clearedHands)
        {
            inventory?.DropAll();
        }

        if (runStats != null && controller != null)
        {
            EnergyBot energyBot = controller.GetComponent<EnergyBot>();
            Attack attack = controller.Stats.Attacks.Count > 0 ? controller.Stats.Attacks[0] : null;
            AttackHitbox[] attackHitboxes = controller.GetComponentsInChildren<AttackHitbox>(true);
            runStats.Capture(controller.Stats, energyBot, attack, attackHitboxes);
            Debug.Log($"[LevelEndVictoryTrigger] Captured run stats before level transition. Bonuses: {runStats.DescribeBonuses()}", this);
            if (saveService != null)
            {
                saveService.SaveGame(controller, runStats);
            }
        }
        else if (controller == null)
        {
            Debug.LogWarning("LevelEndVictoryTrigger: RobotStateController component is missing on Player or its parent.");
        }
    }

    private void LoadNextRunStep()
    {
        if (RunProgressManager.Instance != null)
        {
            Debug.Log($"[LevelEndVictoryTrigger] Loading next step from run level {RunProgressManager.Instance.CurrentLevelIndex}.", this);
            RunProgressManager.Instance.LoadNextStep();
        }
        else
        {
            Debug.LogError("LevelEndVictoryTrigger: RunProgressManager instance is missing.");
        }
    }

    private void ResolveRunStats()
    {
        if (RunProgressManager.Instance == null || RunProgressManager.Instance.RunStats == null)
            return;

        if (runStats != null && runStats != RunProgressManager.Instance.RunStats)
        {
            Debug.LogWarning("LevelEndVictoryTrigger: Replacing serialized PlayerRunStats with RunProgressManager.RunStats for run continuity.");
        }

        runStats = RunProgressManager.Instance.RunStats;
    }
}
