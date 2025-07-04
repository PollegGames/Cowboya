using UnityEngine;
using UnityEngine.SceneManagement;

public class EndVictoryLoad : MonoBehaviour
{
    [SerializeField] private DoorController doorNext;
    [SerializeField] private VictorySetup victorySetup;

    private bool isExternalDoor = false;

    private void Awake()
    {
        if (doorNext != null)
        {
            isExternalDoor = doorNext.isWall;
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (victorySetup != null)
        {
            bool isVictory = victorySetup.currentKilled >= victorySetup.robotsKilledTarget;
            if (isExternalDoor && isVictory && collision.CompareTag("Player"))
            {
                SceneController.instance.LoadScene("RunSetupScene");
            }

        }
    }
}
