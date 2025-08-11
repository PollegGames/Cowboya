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
            PlayerSpawner spawner = FindObjectOfType<PlayerSpawner>();
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
            saveService = FindObjectOfType<PlayerSaveService>();
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
                RobotStateController controller = collision.GetComponent<RobotStateController>();
                GrabSystem grabSystem = collision.GetComponent<GrabSystem>();

                if (grabSystem != null)
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

                RunProgressManager.Instance.LoadNextLevel();
            }

        }
    }
}
