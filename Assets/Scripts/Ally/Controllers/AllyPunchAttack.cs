using System.Collections;
using UnityEngine;

public class AllyPunchAttack : MonoBehaviour
{
    [SerializeField] private ArcTargetFollower arcTargetFollower; // assign in inspector

    [Header("Punch Targets")]
    public Transform leftArmTarget;
    public Transform rightArmTarget;
    public Transform leftRestPosition;
    public Transform rightRestPosition;

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

    private bool isPunching = false;
    private bool targetInRange = false;
    private float cooldownTimer = 0f;
    private Vector3 targetPosition;

    private RobotStateController robotBehaviour;

    private void Awake()
    {
        robotBehaviour = GetComponent<RobotStateController>();
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (targetInRange && !isPunching && cooldownTimer <= 0f)
        {
            TryAttack();
        }
    }

    private void TryAttack()
    {
        bool facingRight = arcTargetFollower != null && arcTargetFollower.IsFacingRight;
        Transform armTarget = facingRight ? rightArmTarget : leftArmTarget;
        AttackHitbox hitbox = facingRight ? rightArmHitbox : leftArmHitbox;
        float damageCost = hitbox.DamageCost;

        if (robotBehaviour == null || !robotBehaviour.CanPerformEnergy(damageCost))
        {
            return;
        }

        robotBehaviour.PerformAttackbyEnergy(damageCost);
        cooldownTimer = attackCooldown;
        StartCoroutine(PunchSequence(armTarget, hitbox, facingRight));
    }

    /// <summary>
    /// Updates the position of the current attack target and enables attacking.
    /// </summary>
    /// <param name="position">World position of the target.</param>
    public void SetTargetPosition(Vector3 position)
    {
        targetPosition = position;
        targetInRange = true;
    }

    /// <summary>
    /// Clears the current target and stops automatic attacks.
    /// </summary>
    public void ClearTarget()
    {
        targetInRange = false;
    }

    private IEnumerator PunchSequence(Transform armTarget, AttackHitbox hitbox, bool facingRight)
    {
        isPunching = true;

        Vector3 start = armTarget.position;
        Vector3 end = targetPosition;

        Transform controlPoint = facingRight ? arcControlRight : arcControlLeft;
        Vector3 control = controlPoint != null ? controlPoint.position : (start + end) / 2 + Vector3.up * 0.5f;

        Transform restPosition = facingRight ? rightRestPosition : leftRestPosition;

        hitbox?.Activate();

        float timer = 0f;
        while (timer < punchDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / punchDuration);
            armTarget.position = GetQuadraticBezierPoint(start, control, end, t);
            yield return null;
        }

        yield return new WaitForSeconds(0.05f);

        hitbox?.Deactivate();

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
