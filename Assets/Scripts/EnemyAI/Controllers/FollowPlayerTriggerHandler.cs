using System;
using Unity.VisualScripting;
using UnityEngine;

public class FollowPlayerTriggerHandler : MonoBehaviour
{

    [Header("Zone Detection")]
    public PositionTriggerZone detectZone;
    public PositionTriggerZone attackZone;
    [Header("References")]
    public Transform circleCenter; // typically the player's torso
    public float radius = 2f;

    private Camera mainCamera;
    private bool isFacingRight = true;
    private bool playerInAttackZone;

    public bool IsFacingRight => isFacingRight;

    private Transform playerBodyReference;

    public Vector3 PlayerBodyReferencePosition => playerBodyReference != null ? playerBodyReference.position : Vector3.zero;
    public Transform PlayerTransform => playerBodyReference;

    [SerializeField] private RobotMemory memory;
    [SerializeField] private RobotBrain brain;

    public event Action<bool> OnPlayerDetectInAttackZoneChanged;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (brain == null)
            brain = GetComponent<RobotBrain>();
    }

    private void OnDisable()
    {
        if (brain != null && playerInAttackZone && playerBodyReference != null)
            brain.OnPlayerInAttackZoneChanged(false, playerBodyReference);

        playerInAttackZone = false;
        if (memory != null)
        {
            memory.SetPlayerInAttackZone(false);
            memory.SetCanSeePlayer(false);
        }
        playerBodyReference = null;
    }
    private void Start()
    {
        if (detectZone != null)
        {
            detectZone.onEnter.AddListener(OnPlayerEnterDetectZone);
            detectZone.onExit.AddListener(OnPlayerExitDetectZone);
        }
        if (attackZone != null)
        {
            attackZone.onEnter.AddListener(OnPlayerEnterAttackZone);
            attackZone.onExit.AddListener(OnPlayerExitAttackZone);
        }
    }


    private void OnPlayerEnterDetectZone(Collider2D collider)
    {
        CachePlayerReference(collider);
        if (playerBodyReference != null)
        {
            memory?.RememberPlayerPosition(playerBodyReference.position);
            memory?.SetCanSeePlayer(true);
        }
    }

    private void OnPlayerExitDetectZone()
    {
        // Reset the target position if the player leaves the zone
        playerBodyReference = null;
        memory?.ClearPlayerPosition();
        memory?.SetCanSeePlayer(false);
    }


    private void OnPlayerEnterAttackZone(Collider2D collider)
    {
        CachePlayerReference(collider);
        if (playerBodyReference != null)
        {
            memory?.SetPlayerInAttackZone(true);
            memory?.SetCanSeePlayer(true);
            brain?.OnPlayerInAttackZoneChanged(true, playerBodyReference);
            playerInAttackZone = true;
        }
        OnPlayerDetectInAttackZoneChanged?.Invoke(true);
    }

    private void OnPlayerExitAttackZone()
    {
        if (playerBodyReference != null)
            brain?.OnPlayerInAttackZoneChanged(false, playerBodyReference);
        memory?.SetPlayerInAttackZone(false);
        playerInAttackZone = false;
        OnPlayerDetectInAttackZoneChanged?.Invoke(false);
    }

    void Update()
    {
        if (circleCenter == null || mainCamera == null)
            return;

        if (playerBodyReference != null)
        {
            Vector3 playerPosition = playerBodyReference.position;
            memory?.RememberPlayerPosition(playerPosition);
            memory?.SetCanSeePlayer(true);

            Vector3 direction = (playerPosition - circleCenter.position).normalized;
            Vector3 targetPos = circleCenter.position + direction * radius;
            transform.position = targetPos;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            isFacingRight = transform.position.x >= circleCenter.position.x;
        }


    }

    private void CachePlayerReference(Collider2D collider)
    {
        if (collider == null)
            return;

        var playerControl = collider.transform.root.GetComponent<PlayerMovementController>();
        if (playerControl == null)
        {
            playerControl = collider.GetComponentInParent<PlayerMovementController>();
        }

        if (playerControl != null)
        {
            playerBodyReference = playerControl.BodyReference != null
                ? playerControl.BodyReference.transform
                : playerControl.transform;
            memory?.RememberPlayerPosition(playerBodyReference.position);
            memory?.SetCanSeePlayer(true);
        }
    }
}
