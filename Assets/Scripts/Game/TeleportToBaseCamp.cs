using UnityEngine;

public class TeleportToBaseCap : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            SceneController.instance.LoadScene("BaseCampScene");
        }
    }
}

