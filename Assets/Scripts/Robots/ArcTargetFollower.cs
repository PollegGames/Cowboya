using UnityEngine;

public class ArcTargetFollower : MonoBehaviour
{
    public Transform circleCenter; // généralement le torse du joueur
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

        // 1. Récupère la position de la souris
        Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mouseWorld.z = 0f;

        // 2. Calcul direction → position sur le cercle
        Vector3 direction = (mouseWorld - circleCenter.position).normalized;
        Vector3 targetPos = circleCenter.position + direction * radius;
        transform.position = targetPos;

        // 3. Rotation de la cible vers la souris (optionnel)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 4. Détermine si la cible est à gauche ou à droite du joueur
        isFacingRight = transform.position.x <= circleCenter.position.x;
    }
}
