using UnityEngine;

public class LevelEndTrigger : MonoBehaviour
{
    [SerializeField] private PlayerTemplate playerTemplate;
    [SerializeField] private PlayerSaveService saveService;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && RunProgressManager.Instance != null)
        {
            RobotStateController controller = other.GetComponent<RobotStateController>();
            GrabSystem grabSystem = other.GetComponent<GrabSystem>();

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
