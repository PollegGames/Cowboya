using System.Collections;
using UnityEngine;

/// <summary>
/// Simple attack driver for robots: activates assigned hitboxes for a short duration with cooldown.
/// </summary>
[DisallowMultipleComponent]
public class RobotAttackController : MonoBehaviour
{
    [SerializeField] private AttackHitbox[] hitboxes;
    [SerializeField] private float attackDuration = 0.4f;
    [SerializeField] private float attackCooldown = 1f;

    private float lastAttackTime = -Mathf.Infinity;
    private Coroutine attackRoutine;

    private void OnEnable()
    {
        DeactivateAll();
    }

    private void OnDisable()
    {
        StopActiveAttack();
        DeactivateAll();
    }

    /// <summary>
    /// Attempts to perform an attack. Returns true if started.
    /// </summary>
    public bool TryAttack(Vector2 targetPosition)
    {
        if (Time.time < lastAttackTime + attackCooldown)
            return false;

        StopActiveAttack();
        attackRoutine = StartCoroutine(AttackWindow());
        lastAttackTime = Time.time;
        return true;
    }

    private IEnumerator AttackWindow()
    {
        ActivateAll();
        yield return new WaitForSeconds(attackDuration);
        DeactivateAll();
        attackRoutine = null;
    }

    private void StopActiveAttack()
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
    }

    private void ActivateAll()
    {
        if (hitboxes == null)
            return;
        foreach (var hitbox in hitboxes)
        {
            if (hitbox != null)
                hitbox.Activate();
        }
    }

    private void DeactivateAll()
    {
        if (hitboxes == null)
            return;
        foreach (var hitbox in hitboxes)
        {
            if (hitbox != null)
                hitbox.Deactivate();
        }
    }
}
