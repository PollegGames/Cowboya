using UnityEngine;

public class SquareMover : MonoBehaviour
{
    public float moveSpeed = 5f; // Base speed for movement

    private void Update()
    {
        // Get horizontal input
        float horizontalInput = Input.GetAxis("Horizontal");

        // Calculate movement vector
        Vector3 movement = new Vector3(horizontalInput, 0f, 0f);

        // Move the player
        transform.position += movement * moveSpeed * Time.deltaTime;

        // Update the current speed in the ScriptableObject
        float currentSpeed = Mathf.Abs(horizontalInput * moveSpeed);

    }
}
