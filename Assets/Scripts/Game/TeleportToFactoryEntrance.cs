using UnityEngine;
using UnityEngine.SceneManagement;

public class TeleportToFactoryEntrance : MonoBehaviour
{
   private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SceneController.instance.LoadScene("FactoryEntrance");
        }
    }
}
