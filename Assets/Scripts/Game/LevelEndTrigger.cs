using System.Reflection;
using UnityEngine;

public class LevelEndTrigger : MonoBehaviour
{
    [SerializeField] private PlayerTemplate playerTemplate;
    [SerializeField] private PlayerSaveService saveService;

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
            Debug.LogError("LevelEndTrigger: PlayerTemplate reference is missing.");
        }
        if (saveService == null)
        {
            Debug.LogError("LevelEndTrigger: PlayerSaveService reference is missing.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && RunProgressManager.Instance != null)
        {
            RobotStateController controller = other.GetComponentInParent<RobotStateController>();
            GrabSystem grabSystem = other.GetComponentInParent<GrabSystem>();

            if (grabSystem == null)
            {
                Debug.LogWarning("LevelEndTrigger: GrabSystem component is missing on Player or its parent.");
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
                Debug.LogWarning("LevelEndTrigger: RobotStateController component is missing on Player or its parent.");
            }

            RunProgressManager.Instance.LoadNextLevel();
        }
    }
}
