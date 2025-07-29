using UnityEngine;
using UnityEngine.InputSystem;

public class ArcTargetFollower : MonoBehaviour
{
    public Transform circleCenter; // usually the player's torso
    public float radius = 2f;

    private Camera mainCamera;
    private bool isFacingRight = true;

    public bool IsFacingRight => isFacingRight;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (circleCenter == null || mainCamera == null)
            return;

        // 1. Get the mouse position
        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        mouseWorld.z = 0f;

        // 2. Calcul direction → position sur le cercle
        Vector3 direction = (mouseWorld - circleCenter.position).normalized;
        Vector3 targetPos = circleCenter.position + direction * radius;
        transform.position = targetPos;

        // 3. Rotation de la cible vers la souris (optionnel)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 4. Determine if the target is to the left or right of the player
        isFacingRight = transform.position.x <= circleCenter.position.x;
    }
}
