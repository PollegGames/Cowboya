using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelEndVictoryTrigger : MonoBehaviour
{
    [SerializeField] private DoorController doorNext;
    [SerializeField] private VictorySetup victorySetup;
    [SerializeField] private PlayerTemplate playerTemplate;

    private bool isVictoryDoor = false;


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
                }

                RunProgressManager.Instance.LoadNextLevel();
            }

        }
    }
}
