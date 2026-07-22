using UnityEngine;

/// <summary>
/// Moves this transform on the X-Z plane within configurable limits based on the player's position.
/// </summary>
public class MoveWithPlayerPosition : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Player transform to track. If empty, the player head is taken from Room Manager.")]
    public Transform player;

    [Tooltip("Optional reference used as the neutral player position. Defaults to this object's starting position.")]
    public Transform trackingCenter;

    [Tooltip("Optional room manager used to find the player head automatically.")]
    public RoomManager roomManager;

    [Tooltip("Optional zone that activates movement and detects the player. If empty, movement is always active.")]
    public PositionTriggerZone activationZone;

    [Header("Player Range")]
    [Min(0.01f)]
    [Tooltip("Player X distance from the center at which horizontal movement reaches its limit.")]
    public float horizontalRange = 5f;

    [Min(0.01f)]
    [Tooltip("Player Z distance from the center at which vertical movement reaches its limit.")]
    public float verticalRange = 5f;

    [Header("Movement Limits")]
    [Min(0f)] public float maxLeft = 1f;
    [Min(0f)] public float maxRight = 1f;
    [Min(0f)] public float maxDown = 1f;
    [Min(0f)] public float maxUp = 1f;

    [Header("Direction")]
    public bool invertHorizontal;
    public bool invertVertical;

    [Header("Motion")]
    [Min(0f)]
    [Tooltip("Time used to smooth movement. Set to zero for immediate movement.")]
    public float smoothTime = 0.1f;

    private Vector3 initialLocalPosition;
    private Vector3 movementVelocity;
    private bool playerIsInZone;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;

        if (roomManager == null)
            roomManager = GetComponentInParent<RoomManager>();

        ResolvePlayer();
    }

    private void OnEnable()
    {
        if (activationZone == null)
            return;

        activationZone.onEnter?.AddListener(OnPlayerEnteredZone);
        activationZone.onExit?.AddListener(OnPlayerExitedZone);
    }

    private void OnDisable()
    {
        if (activationZone == null)
            return;

        activationZone.onEnter?.RemoveListener(OnPlayerEnteredZone);
        activationZone.onExit?.RemoveListener(OnPlayerExitedZone);
    }

    private void LateUpdate()
    {
        if (activationZone != null && !playerIsInZone)
        {
            MoveTo(initialLocalPosition);
            return;
        }

        if (player == null)
            ResolvePlayer();

        if (player == null)
            return;

        Vector3 centerPosition = GetCenterPositionInParentSpace();
        Vector3 playerPosition = GetPositionInParentSpace(player.position);
        Vector3 playerOffset = playerPosition - centerPosition;

        float horizontalInput = Mathf.Clamp(playerOffset.x / horizontalRange, -1f, 1f);
        float verticalInput = Mathf.Clamp(playerOffset.z / verticalRange, -1f, 1f);

        if (invertHorizontal)
            horizontalInput = -horizontalInput;

        if (invertVertical)
            verticalInput = -verticalInput;

        float horizontalOffset = horizontalInput < 0f
            ? horizontalInput * maxLeft
            : horizontalInput * maxRight;
        float verticalOffset = verticalInput < 0f
            ? verticalInput * maxDown
            : verticalInput * maxUp;

        Vector3 targetPosition = initialLocalPosition + new Vector3(horizontalOffset, 0f, verticalOffset);

        MoveTo(targetPosition);
    }

    private void OnPlayerEnteredZone(Collider2D playerCollider)
    {
        playerIsInZone = true;

        if (playerCollider != null)
        {
            PlayerMovementController movement = playerCollider.GetComponentInParent<PlayerMovementController>();
            if (movement != null && movement.HeadTransform != null)
                player = movement.HeadTransform;
        }

        if (player == null)
            ResolvePlayer();

        if (player == null && playerCollider != null)
            player = playerCollider.transform;
    }

    private void OnPlayerExitedZone()
    {
        playerIsInZone = false;
    }

    private void MoveTo(Vector3 targetPosition)
    {
        if (smoothTime <= 0f)
        {
            transform.localPosition = targetPosition;
            movementVelocity = Vector3.zero;
            return;
        }

        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition,
            targetPosition,
            ref movementVelocity,
            smoothTime);
    }

    private void ResolvePlayer()
    {
        if (roomManager == null)
            return;

        if (roomManager.FactoryManager != null && roomManager.FactoryManager.playerHeadTransform != null)
            player = roomManager.FactoryManager.playerHeadTransform;
        else if (roomManager.PlayerHead != null)
            player = roomManager.PlayerHead;
    }

    private Vector3 GetCenterPositionInParentSpace()
    {
        if (trackingCenter != null)
            return GetPositionInParentSpace(trackingCenter.position);

        return initialLocalPosition;
    }

    private Vector3 GetPositionInParentSpace(Vector3 worldPosition)
    {
        return transform.parent != null
            ? transform.parent.InverseTransformPoint(worldPosition)
            : worldPosition;
    }

    private void OnValidate()
    {
        horizontalRange = Mathf.Max(0.01f, horizontalRange);
        verticalRange = Mathf.Max(0.01f, verticalRange);
        maxLeft = Mathf.Max(0f, maxLeft);
        maxRight = Mathf.Max(0f, maxRight);
        maxDown = Mathf.Max(0f, maxDown);
        maxUp = Mathf.Max(0f, maxUp);
        smoothTime = Mathf.Max(0f, smoothTime);
    }
}
