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
    private bool playerInDetectZone;
    private bool playerInAttackZone;

    public bool IsFacingRight => isFacingRight;

    private Transform playerBodyReference;

    public Vector3 PlayerBodyReferencePosition => playerBodyReference != null ? playerBodyReference.position : Vector3.zero;
    public Transform PlayerTransform => playerBodyReference;

    [SerializeField] private RobotMemoryNew memory;
    [SerializeField] private RobotBrainNew brain;
    [SerializeField] private RobotMemoryNew memoryNew;
    [SerializeField] private RobotBrainNew brainNew;

    public event Action<bool> OnPlayerDetectInAttackZoneChanged;

    private void Awake()
    {
        mainCamera = Camera.main;
        if (brain == null)
            brain = GetComponent<RobotBrainNew>();
        if (memoryNew == null)
            memoryNew = GetComponent<RobotMemoryNew>();
        if (brainNew == null)
            brainNew = GetComponent<RobotBrainNew>();
    }

    private void OnDisable()
    {
        if (brain != null && playerInAttackZone && playerBodyReference != null)
            brain.OnPlayerInAttackZoneChanged(false, playerBodyReference);
        if (RobotNewPipelineRuntime.IsNewPipelineActive && brainNew != null)
            brainNew.OnPerceptionChanged(false, false, Vector3.zero, hasKnownPosition: false);

        playerInAttackZone = false;
        if (memory != null)
        {
            memory.SetPlayerInAttackZone(false);
            memory.SetCanSeePlayer(false);
        }
        if (memoryNew != null)
        {
            memoryNew.SetPlayerInAttackZone(false);
            memoryNew.SetPlayerInDetectZone(false);
            memoryNew.ClearPlayerPosition();
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
            if (RobotNewPipelineRuntime.IsNewPipelineActive)
                brainNew?.OnPerceptionChanged(true, playerInAttackZone, playerBodyReference.position, hasKnownPosition: true);
        }
        playerInDetectZone = true;

        if (ShouldUseDetectZoneForAttack() && playerBodyReference != null)
        {
            memory?.SetPlayerInAttackZone(true);
            brain?.OnPlayerInAttackZoneChanged(true, playerBodyReference);
            playerInAttackZone = true;
            brain?.Body?.StopMovement();
        }

    }

    private void OnPlayerExitDetectZone()
    {
        if (ShouldUseDetectZoneForAttack() && playerInAttackZone && playerBodyReference != null)
        {
            brain?.OnPlayerInAttackZoneChanged(false, playerBodyReference);
            memory?.SetPlayerInAttackZone(false);
            playerInAttackZone = false;
        }

        playerInDetectZone = false;
        // Reset the target position if the player leaves the zone
        playerBodyReference = null;
        memory?.ClearPlayerPosition();
        memory?.SetCanSeePlayer(false);
        if (RobotNewPipelineRuntime.IsNewPipelineActive)
            brainNew?.OnPerceptionChanged(false, false, Vector3.zero, hasKnownPosition: false);
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
            if (RobotNewPipelineRuntime.IsNewPipelineActive)
                brainNew?.OnPerceptionChanged(playerInDetectZone, true, playerBodyReference.position, hasKnownPosition: true);
        }
        OnPlayerDetectInAttackZoneChanged?.Invoke(true);

    }

    private void OnPlayerExitAttackZone()
    {
        if (ShouldUseDetectZoneForAttack() && playerInDetectZone)
        {
            OnPlayerDetectInAttackZoneChanged?.Invoke(false);
            return;
        }

        if (playerBodyReference != null)
            brain?.OnPlayerInAttackZoneChanged(false, playerBodyReference);
        memory?.SetPlayerInAttackZone(false);
        playerInAttackZone = false;
        if (RobotNewPipelineRuntime.IsNewPipelineActive)
            brainNew?.OnPerceptionChanged(playerInDetectZone, false, playerBodyReference != null ? playerBodyReference.position : Vector3.zero, hasKnownPosition: playerBodyReference != null);
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
            if (RobotNewPipelineRuntime.IsNewPipelineActive)
                brainNew?.OnPerceptionChanged(playerInDetectZone, playerInAttackZone, playerPosition, hasKnownPosition: true);

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
            if (RobotNewPipelineRuntime.IsNewPipelineActive)
                brainNew?.OnPerceptionChanged(true, playerInAttackZone, playerBodyReference.position, hasKnownPosition: true);
        }
    }

    private bool ShouldUseDetectZoneForAttack()
    {
        var heart = brain != null ? brain.Heart : null;
        if (heart == null)
            return false;

        switch (heart.Role)
        {
            case RobotRole.SecurityGuard:
            case RobotRole.Follower:
            case RobotRole.Boss:
                return true;
            default:
                return false;
        }
    }
}

