using UnityEngine;

/// <summary>
/// Converts the player's abstract aim into a fixed-radius world target.
/// </summary>
public class ArcTargetFollower : MonoBehaviour
{
    public Transform circleCenter;
    public float radius = 2f;
    [SerializeField] private MonoBehaviour inputSource;
    [SerializeField, Range(0f, 1f)] private float aimDeadzone = 0.2f;

    private Camera mainCamera;
    private IPlayerInput input;
    private Vector2 lastAimDirection = Vector2.right;
    private bool hasAimDirection;
    private bool isFacingRight = true;

    public bool IsFacingRight => isFacingRight;

    private void Awake()
    {
        mainCamera = Camera.main;
        CacheInput();
    }

    private void Update()
    {
        if (circleCenter == null)
            return;

        CacheInput();
        if (input == null)
            return;

        Vector2 direction = ResolveAimDirection();
        if (direction.sqrMagnitude >= aimDeadzone * aimDeadzone)
        {
            lastAimDirection = direction.normalized;
            hasAimDirection = true;
        }

        if (!hasAimDirection)
            return;

        transform.position = circleCenter.position + (Vector3)(lastAimDirection * radius);
        float angle = Mathf.Atan2(lastAimDirection.y, lastAimDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        isFacingRight = lastAimDirection.x >= 0f;
    }

    private Vector2 ResolveAimDirection()
    {
        Vector2 aim = input.Aim;
        if (!input.AimIsScreenPosition)
            return aim;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return Vector2.zero;

        Vector3 aimWorld = mainCamera.ScreenToWorldPoint(aim);
        return (Vector2)(aimWorld - circleCenter.position);
    }

    private void CacheInput()
    {
        if (input != null)
            return;

        input = inputSource as IPlayerInput;
        if (input == null)
            input = GetComponentInParent<IPlayerInput>();
    }
}
