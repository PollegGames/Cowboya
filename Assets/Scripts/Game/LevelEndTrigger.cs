using UnityEngine;

public class LevelEndTrigger : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && RunProgressManager.Instance != null)
        {
            RunProgressManager.Instance.LoadNextLevel();
        }
    }
}
