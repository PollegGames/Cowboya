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

    public bool IsFacingRight => isFacingRight;

    private Vector3 playerBodyReferencePosition;

    public Vector3 PlayerBodyReferencePosition => playerBodyReferencePosition;

    [SerializeField] private RobotMemory memory;

    public event Action<bool> OnPlayerDetectInAttackZoneChanged;

    private void Awake()
    {
        mainCamera = Camera.main;
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
        // Make sure the collider is the player
        if (collider.CompareTag("Player"))
        {
            var playerControl = collider.transform.root.GetComponent<PlayerMovementController>();
            if (playerControl != null)
            {
                playerBodyReferencePosition = playerControl.BodyReference.transform.position;
                memory.RememberPlayerPosition(playerBodyReferencePosition);
            }
        }
    }

    private void OnPlayerExitDetectZone()
    {
        // Reset the target position if the player leaves the zone
        playerBodyReferencePosition = Vector3.zero;
        memory.ClearPlayerPosition();
    }


    private void OnPlayerEnterAttackZone(Collider2D collider)
    {
        // Make sure the collider is the player
        if (collider.CompareTag("Player"))
        {
            OnPlayerDetectInAttackZoneChanged?.Invoke(true);
        }
    }

    private void OnPlayerExitAttackZone()
    {
        OnPlayerDetectInAttackZoneChanged?.Invoke(false);
    }

    void Update()
    {
        if (circleCenter == null || mainCamera == null)
            return;

        if (playerBodyReferencePosition != Vector3.zero)
        {
            // 1. Update the memory with the player's position
            memory.RememberPlayerPosition(playerBodyReferencePosition);
            // 2. Calcul direction → position sur le cercle
            Vector3 direction = (playerBodyReferencePosition - circleCenter.position).normalized;
            Vector3 targetPos = circleCenter.position + direction * radius;
            transform.position = targetPos;

            // 3. Rotation de la cible vers la souris (optionnel)
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // 4. Determine if the target is to the left or right of the player
            isFacingRight = transform.position.x <= circleCenter.position.x;
        }


    }
}
