using UnityEngine;


public class TeleportToFactoryEntrance : MonoBehaviour
{
   private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SceneController.instance.LoadScene("MapGeneration");
        }
    }
}
