using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelEndVictoryTrigger : MonoBehaviour
{
    [SerializeField] private DoorController doorNext;
    [SerializeField] private VictorySetup victorySetup;
    [SerializeField] private PlayerTemplate playerTemplate;
    [SerializeField] private PlayerSaveService saveService;

    private bool isVictoryDoor = false;

    private void Awake()
    {
        if (playerTemplate == null && RunProgressManager.Instance != null)
        {
            FieldInfo field = typeof(RunProgressManager).GetField("playerTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                playerTemplate = field.GetValue(RunProgressManager.Instance) as PlayerTemplate;
            }
        }

        if (playerTemplate == null)
        {
            PlayerSpawner spawner = FindFirstObjectByType<PlayerSpawner>();
            if (spawner != null)
            {
                FieldInfo spawnerField = typeof(PlayerSpawner).GetField("playerTemplate", BindingFlags.NonPublic | BindingFlags.Instance);
                if (spawnerField != null)
                {
                    playerTemplate = spawnerField.GetValue(spawner) as PlayerTemplate;
                }
            }
        }

        if (saveService == null)
        {
            saveService = FindFirstObjectByType<PlayerSaveService>();
        }

        if (playerTemplate == null)
        {
            Debug.LogError("LevelEndVictoryTrigger: PlayerTemplate reference is missing.");
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
                }

                if (playerTemplate != null && controller != null)
                {
                    playerTemplate.CaptureStats(controller.Stats);
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
