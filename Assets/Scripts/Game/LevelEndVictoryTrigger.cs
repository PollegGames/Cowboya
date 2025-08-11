using UnityEngine;
public class LevelEndVictoryTrigger : MonoBehaviour
{
    [SerializeField] private DoorController doorNext;
    [SerializeField] private VictorySetup victorySetup;
    [SerializeField] private PlayerRunStats runStats;
    [SerializeField] private PlayerSaveService saveService;

    private bool isVictoryDoor = false;

    private void Awake()
    {
        if (runStats == null && RunProgressManager.Instance != null)
        {
            runStats = RunProgressManager.Instance.RunStats;
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
    }


    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (doorNext != null)
        {
            isVictoryDoor = doorNext.isVictoryDoor;
        }
        if (victorySetup != null)
        {
            bool isVictory = victorySetup.currentKilled >= victorySetup.robotsKilledTarget
                || victorySetup.currentSaved >= victorySetup.robotsSavedTarget;
            if (isVictoryDoor && isVictory && collision.CompareTag("Player"))
            {
                RobotStateController controller = collision.GetComponentInParent<RobotStateController>();
                GrabSystem grabSystem = collision.GetComponentInParent<GrabSystem>();

                if (grabSystem == null)
                {
                    Debug.LogWarning("LevelEndVictoryTrigger: GrabSystem component is missing on Player or its parent.");
                }
                else
                {
                    grabSystem.ClearHands();
                    BatteryPickup.DropPlayerBattery();
                }

                if (runStats != null && controller != null)
                {
                    runStats.Capture(controller.Stats);
                    if (saveService != null)
                    {
                        saveService.SaveGame(controller);
                    }
                }
                else if (controller == null)
                {
                    Debug.LogWarning("LevelEndVictoryTrigger: RobotStateController component is missing on Player or its parent.");
                }

                RunProgressManager.Instance.LoadNextLevel();
            }

        }
    }
}
