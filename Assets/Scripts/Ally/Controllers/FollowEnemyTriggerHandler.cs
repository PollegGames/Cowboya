using System;
using UnityEngine;

public class FollowEnemyTriggerHandler : MonoBehaviour
{
    [Header("Zone Detection")]
    public PositionTriggerZone detectZone;
    public PositionTriggerZone attackZone;
    [Header("References")]
    public Transform circleCenter;
    public float radius = 2f;

    private Camera mainCamera;
    private bool isFacingRight = true;
    public bool IsFacingRight => isFacingRight;

    private Transform enemyBodyReference;
    public Vector3 EnemyBodyReferencePosition => enemyBodyReference != null ? enemyBodyReference.position : Vector3.zero;

    public event Action<bool> OnEnemyDetectInAttackZoneChanged;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Start()
    {
        if (detectZone != null)
        {
            detectZone.onEnter.AddListener(OnEnemyEnterDetectZone);
            detectZone.onExit.AddListener(OnEnemyExitDetectZone);
        }
        if (attackZone != null)
        {
            attackZone.onEnter.AddListener(OnEnemyEnterAttackZone);
            attackZone.onExit.AddListener(OnEnemyExitAttackZone);
        }
    }

    private void OnEnemyEnterDetectZone(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            var enemyControl = collider.transform.root.GetComponent<EnemyController>();
            if (enemyControl != null)
            {
                enemyBodyReference = enemyControl.BodyReference;
            }
        }
    }

    private void OnEnemyExitDetectZone()
    {
        enemyBodyReference = null;
    }

    private void OnEnemyEnterAttackZone(Collider2D collider)
    {
        if (collider.CompareTag("Enemy"))
        {
            OnEnemyDetectInAttackZoneChanged?.Invoke(true);
        }
    }

    private void OnEnemyExitAttackZone()
    {
        OnEnemyDetectInAttackZoneChanged?.Invoke(false);
    }

    private void Update()
    {
        if (circleCenter == null || mainCamera == null)
            return;

        if (enemyBodyReference != null)
        {
            Vector3 direction = (enemyBodyReference.position - circleCenter.position).normalized;
            Vector3 targetPos = circleCenter.position + direction * radius;
            transform.position = targetPos;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            isFacingRight = transform.position.x <= circleCenter.position.x;
        }
    }
}
