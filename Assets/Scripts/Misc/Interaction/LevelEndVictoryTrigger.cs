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

        if (doorNext != null)
        {
            isVictoryDoor = doorNext.isVictoryDoor;
        }
        UpdateVisual();
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

        if (doorNext != null)
        {
            isVictoryDoor = doorNext.isVictoryDoor;
        }
        if (victorySetup != null)
        {
            bool isVictory = victorySetup.currentKilled >= victorySetup.robotsKilledTarget
                || victorySetup.currentSaved >= victorySetup.robotsSavedTarget;
            if (isVictoryDoor && isVictory && collision.CompareTag("Player") && !hasRequestedLevelTransition)
            {
                hasRequestedLevelTransition = true;
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

                if (RunProgressManager.Instance != null)
                {
                    Debug.Log($"[LevelEndVictoryTrigger] Loading next level from run level {RunProgressManager.Instance.CurrentLevelIndex}.", this);
                    RunProgressManager.Instance.LoadNextLevel();
                }
                else
                {
                    Debug.LogError("LevelEndVictoryTrigger: RunProgressManager instance is missing.");
                }
            }

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
