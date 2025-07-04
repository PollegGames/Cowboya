using UnityEngine;

public class HeadTargetController : MonoBehaviour
{
    public Transform hips;
    public Transform head;
    public RobotLocomotionController locomotion;

    [Header("Position Settings")]
    public float verticalOffset = 2.5f;
    public float maxMirrorOffset = 3f;

    [Header("Bend Settings")]
    public float maxBendOffset = 0.5f;
    public float bendSpeed = 5f;

    private float currentBend = 0f;

    private void LateUpdate()
    {
        if (!hips || !head || !locomotion) return;

        // 1. Miroir par rapport à la position de la tête
        float headOffsetX = head.position.x - hips.position.x;
        float mirroredOffsetX = -headOffsetX;
        mirroredOffsetX = Mathf.Clamp(mirroredOffsetX, -maxMirrorOffset, maxMirrorOffset);

        // 2. Direction du mouvement → offset de torsion (vers où il se penche)
        float input = Input.GetAxisRaw("Horizontal");
        float targetBend = Mathf.Clamp(input, -1f, 1f) * maxBendOffset;

        // Interpolation fluide pour éviter les à-coups
        currentBend = Mathf.Lerp(currentBend, targetBend, bendSpeed * Time.deltaTime);

        // 3. Position finale
        float totalXOffset = mirroredOffsetX + currentBend;
        transform.position = new Vector3(
            hips.position.x + totalXOffset,
            hips.position.y + verticalOffset,
            transform.position.z
        );
    }
}
