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
        ResolveRobotReferences();
    }

    private void OnDisable()
    {
        bool hadPlayerPerceptionState = playerInDetectZone || playerInAttackZone || playerBodyReference != null;

        if (brain != null && playerInAttackZone && playerBodyReference != null)
            brain.OnPlayerInAttackZoneChanged(false, playerBodyReference);
        if (hadPlayerPerceptionState)
            PublishNewPipelinePerception(false, false, Vector3.zero, false, null, "disable");

        playerInDetectZone = false;
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
        playerInDetectZone = true;
        if (playerBodyReference != null)
        {
            memory?.RememberPlayerPosition(playerBodyReference.position);
            memory?.SetCanSeePlayer(true);
            PublishNewPipelinePerception(true, playerInAttackZone, playerBodyReference.position, true, playerBodyReference, "enter_detect");
        }

        if (ShouldUseDetectZoneForAttack() && playerBodyReference != null)
        {
            memory?.SetPlayerInAttackZone(true);
            brain?.OnPlayerInAttackZoneChanged(true, playerBodyReference);
            playerInAttackZone = true;
            PublishNewPipelinePerception(true, true, playerBodyReference.position, true, playerBodyReference, "enter_detect_as_attack");
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
        PublishNewPipelinePerception(false, false, Vector3.zero, false, null, "exit_detect");
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
            PublishNewPipelinePerception(playerInDetectZone, true, playerBodyReference.position, true, playerBodyReference, "enter_attack");
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
        PublishNewPipelinePerception(
            playerInDetectZone,
            false,
            playerBodyReference != null ? playerBodyReference.position : Vector3.zero,
            playerBodyReference != null,
            playerBodyReference,
            "exit_attack");
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
            PublishNewPipelinePerception(playerInDetectZone, playerInAttackZone, playerPosition, true, playerBodyReference, "update");

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
            PublishNewPipelinePerception(true, playerInAttackZone, playerBodyReference.position, true, playerBodyReference, "cache_player");
        }
    }

    private void ResolveRobotReferences()
    {
        if (brain == null)
            brain = ResolveRobotComponent<RobotBrainNew>();
        if (memory == null)
            memory = ResolveRobotComponent<RobotMemoryNew>();
        if (memoryNew == null)
            memoryNew = ResolveRobotComponent<RobotMemoryNew>();
        if (brainNew == null)
            brainNew = ResolveRobotComponent<RobotBrainNew>();
    }

    private T ResolveRobotComponent<T>() where T : Component
    {
        T component = GetComponent<T>();
        if (component != null)
            return component;

        component = GetComponentInParent<T>();
        if (component != null)
            return component;

        Transform root = transform.root;
        return root != null ? root.GetComponentInChildren<T>() : null;
    }

    private void PublishNewPipelinePerception(
        bool detect,
        bool attack,
        Vector3 position,
        bool hasKnownPosition,
        Transform playerTransform,
        string source)
    {
        if (!RobotNewPipelineRuntime.IsNewPipelineActive)
            return;

        if (brainNew == null)
            ResolveRobotReferences();

        if (brainNew == null)
        {
            Debug.LogWarning(
                $"[{nameof(FollowPlayerTriggerHandler)}] New pipeline perception skipped source={source} detect={detect} attack={attack} reason=missing_brain",
                this);
            return;
        }

        if (source != "update")
        {
            Debug.Log(
                $"[{nameof(FollowPlayerTriggerHandler)}] New pipeline perception source={source} detect={detect} attack={attack} hasPlayerRef={playerTransform != null}",
                this);
        }

        brainNew.OnPerceptionChanged(detect, attack, position, hasKnownPosition, playerTransform);
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

