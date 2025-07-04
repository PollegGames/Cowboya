using UnityEngine;

public class RoomTilt : MonoBehaviour
{
    public Transform player;
    public float maxTiltAngle = 15f;   // Max degrees of tilt
    public float distanceFactor = 5f;  // Higher = gentler slope
    public float tiltSpeed = 5f;       // For smooth Lerp

    void Update()
    {
        float distanceX = player.position.x - transform.position.x;

        // Scale distance so it takes more movement to reach max tilt
        float tiltInput = distanceX / distanceFactor;

        // clamp it to -1..1, then multiply by max tilt
        float targetTilt = Mathf.Clamp(tiltInput, -1f, 1f) * maxTiltAngle;

        // Optionally smooth out transitions over time
        float currentTilt = transform.localRotation.eulerAngles.y;
        // eulerAngles.y can jump from 359 to 0, so use LerpAngle:
        float smoothedTilt = Mathf.LerpAngle(currentTilt, targetTilt, Time.deltaTime * tiltSpeed);

        transform.localRotation = Quaternion.Euler(0, smoothedTilt, 0);
    }
}
