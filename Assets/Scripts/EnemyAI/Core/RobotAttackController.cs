using UnityEngine;

/// <summary>
/// Bridges Brain requests to the arms controller while enforcing melee cooldowns.
/// </summary>
[DisallowMultipleComponent]
public class RobotAttackController : MonoBehaviour
{
    [SerializeField] private EnemyArmTargetController armController;
    [SerializeField] private float attackCooldown = 1f;

    private float lastAttackTime = -999f;
    private bool isAttacking;

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
            return false;

        EnsureArmController();
        if (armController == null)
            return false;

        if (isAttacking)
            return false;

        if (Time.time < lastAttackTime + attackCooldown)
            return false;

        isAttacking = true;
        lastAttackTime = Time.time;
        armController.TriggerAttackPulse();
        return true;
    }

    private void OnAttackFinished()
    {
        isAttacking = false;
    }

    private void OnDisable()
    {
        if (armController != null)
        {
            armController.AttackFinished -= OnAttackFinished;
        }
        isAttacking = false;
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
