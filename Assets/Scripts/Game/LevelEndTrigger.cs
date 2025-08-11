using UnityEngine;

public class LevelEndTrigger : MonoBehaviour
{
    [SerializeField] private PlayerRunStats runStats;
    [SerializeField] private PlayerSaveService saveService;

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
            Debug.LogError("LevelEndTrigger: PlayerRunStats reference is missing.");
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
                Debug.LogWarning("LevelEndTrigger: RobotStateController component is missing on Player or its parent.");
            }

            RunProgressManager.Instance.LoadNextLevel();
        }
    }
}
