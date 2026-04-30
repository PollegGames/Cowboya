using UnityEngine;

/// <summary>
/// Bridges Brain requests to the arms controller while enforcing melee cooldowns.
/// </summary>
[DisallowMultipleComponent]
public class RobotAttackController : MonoBehaviour
{
    [SerializeField] private EnemyArmTargetController armController;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private Vector2 attackWindupRange = new Vector2(0.5f, 1f);

    private float lastAttackTime = -999f;
    private bool isAttacking;
    private bool attackRequested;
    private Coroutine attackRoutine;
    private Transform currentTarget;

    private void OnEnable()
    {
        EnsureArmController();
    }

    /// <summary>
    /// Attempts to start a melee attack toward the provided target transform.
    /// Enforces cooldowns and avoids overlapping attacks.
    /// </summary>
    /// <param name="target">Target to attack.</param>
    /// <returns>True if an attack was started.</returns>
    public bool TryStartAttack(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning($"[{nameof(RobotAttackController)}] Attack request rejected reason=missing_target", this);
            return false;
        }

        currentTarget = target;
        attackRequested = true;

        EnsureArmController();
        if (armController == null)
        {
            Debug.LogWarning($"[{nameof(RobotAttackController)}] Attack request rejected target={target.name} reason=missing_arm_controller", this);
            return false;
        }

        if (attackRoutine == null)
            attackRoutine = StartCoroutine(AttackLoop());
        Debug.Log($"[{nameof(RobotAttackController)}] Attack request accepted target={target.name}", this);
        return true;
    }

    /// <summary>
    /// Stops any ongoing attack loop immediately.
    /// </summary>
    public void StopAttacking()
    {
        attackRequested = false;
        currentTarget = null;
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (armController != null)
            armController.SetAttackRequested(false);

        isAttacking = false;
    }

    private System.Collections.IEnumerator AttackLoop()
    {
        while (attackRequested)
        {
            if (currentTarget == null || armController == null)
                break;

            float remainingCooldown = (lastAttackTime + attackCooldown) - Time.time;
            if (remainingCooldown > 0f)
                yield return new WaitForSeconds(remainingCooldown);

            float windup = Mathf.Clamp(UnityEngine.Random.Range(attackWindupRange.x, attackWindupRange.y), 0f, Mathf.Infinity);
            if (windup > 0f)
                yield return new WaitForSeconds(windup);

            if (!attackRequested || currentTarget == null || armController == null)
                break;

            isAttacking = true;
            lastAttackTime = Time.time;
            armController.TriggerAttackPulse();

            while (attackRequested && isAttacking)
                yield return null;
        }

        attackRoutine = null;
        attackRequested = false;
    }

    private void OnAttackFinished()
    {
        isAttacking = false;
    }

    private void OnDisable()
    {
        StopAttacking();
        if (armController != null)
        {
            armController.AttackFinished -= OnAttackFinished;
        }
    }

    private void EnsureArmController()
    {
        if (armController == null)
            armController = GetComponentInChildren<EnemyArmTargetController>();

        if (armController != null)
        {
            armController.AttackFinished -= OnAttackFinished;
            armController.AttackFinished += OnAttackFinished;
        }
    }
}
