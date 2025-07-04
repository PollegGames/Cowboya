using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadRunSetupScene : MonoBehaviour
{
   private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SceneController.instance.LoadScene("RunSetupScene");
        }
    }
}
