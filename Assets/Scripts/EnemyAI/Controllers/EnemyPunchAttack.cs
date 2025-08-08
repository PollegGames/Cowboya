using System.Collections;
using UnityEngine;

/// <summary>
/// Handles the punching animation for enemies. Uses configured arm targets and
/// hitboxes to strike the player when they enter the attack zone.
/// </summary>
public class EnemyPunchAttack : MonoBehaviour
{
    [SerializeField] private FollowPlayerTriggerHandler targetToFollow; // assigned via inspector

    [Header("Punch Targets")]
    public Transform leftArmTarget;
    public Transform rightArmTarget;
    public Transform leftRestPosition;
    public Transform rightRestPosition;
    public Transform punchTarget;

    [Header("Arc Control Points")]
    public Transform arcControlRight;
    public Transform arcControlLeft;

    [Header("Timing and Speed")]
    public float punchDuration = 0.2f;
    public float returnSpeed = 10f;
    public float attackCooldown = 1f;

    [Header("Attack Hitboxes")]
    public AttackHitbox leftArmHitbox;
    public AttackHitbox rightArmHitbox;

    [Header("Attack Settings")]
    private bool isPunching = false;
    private bool useRightArmNext = true;
    private float lastPunchTime = 0f;

    private RobotStateController robotBehaviour;

    [SerializeField] private RobotMemory memory;
    private bool playerInAttackZone = false;
    private void Awake()
    {
        robotBehaviour = GetComponent<RobotStateController>();
    }
    private void Start()
    {
        if (targetToFollow != null)
        {
            targetToFollow.OnPlayerDetectInAttackZoneChanged += HandlePlayerInAttackZoneChange;
        }
    }

    private void HandlePlayerInAttackZoneChange(bool isInside)
    {
        playerInAttackZone = isInside;
    }

    private void Update()
    {
        if (!targetToFollow || !punchTarget || isPunching) return;

        if (!playerInAttackZone || targetToFollow.PlayerBodyReferencePosition == Vector3.zero) return;

        // Update the punch target to follow the player
        punchTarget.position = targetToFollow.transform.position;

        // If target is tracked and cooldown passed
        float damageCost = rightArmHitbox.DamageCost;
        if (Time.time >= lastPunchTime + attackCooldown && robotBehaviour != null && robotBehaviour.CanPerformEnergy(damageCost))
        {
            lastPunchTime = Time.time;
            robotBehaviour.PerformAttackbyEnergy(damageCost);

            if (useRightArmNext)
                StartCoroutine(PunchSequence(rightArmTarget, rightArmHitbox));
            else
                StartCoroutine(PunchSequence(leftArmTarget, leftArmHitbox));

            useRightArmNext = !useRightArmNext;
        }
    }

    private IEnumerator PunchSequence(Transform armTarget, AttackHitbox hitbox)
    {
        isPunching = true;

        Vector3 start = armTarget.position;
        Vector3 end = punchTarget.position;

        Transform controlPoint = targetToFollow.IsFacingRight ? arcControlRight : arcControlLeft;
        Vector3 control = controlPoint != null ? controlPoint.position : (start + end) / 2 + Vector3.up * 0.5f;

        Transform restPosition = armTarget == rightArmTarget ? rightRestPosition : leftRestPosition;

        if (hitbox != null) hitbox.Activate();

        float timer = 0f;
        while (timer < punchDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / punchDuration);
            armTarget.position = GetQuadraticBezierPoint(start, control, end, t);
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);
        if (hitbox != null) hitbox.Deactivate();

        while (Vector3.Distance(armTarget.position, restPosition.position) > 0.01f)
        {
            armTarget.position = Vector3.MoveTowards(armTarget.position, restPosition.position, returnSpeed * Time.deltaTime);
            yield return null;
        }

        isPunching = false;
    }

    private Vector3 GetQuadraticBezierPoint(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        return Mathf.Pow(1 - t, 2) * a +
               2 * (1 - t) * t * b +
               Mathf.Pow(t, 2) * c;
    }
}
