using System;
using UnityEngine;

public class HealthBot : MonoBehaviour
{
    public event Action<float> OnHealthChanged;

    [SerializeField] private DamageFeedback damageFeedback;
    [SerializeField] private RobotMemory memory = null;
    [SerializeField] private RobotBrain brain = null;
    [SerializeField] private PlayerBrain playerBrain = null;

    public void TakeDamage(int damage)
    {
        Debug.Log("HealthBot: Health changed by " + damage);
        OnHealthChanged?.Invoke(-damage);
        if (damageFeedback != null)
            damageFeedback.Flash();

        CacheBrains();
        memory?.RegisterAttack();

        if (playerBrain != null)
        {
            playerBrain.OnPlayerDamaged(damage);
            return;
        }

        brain?.OnDamageTaken(damage);
    }

    public void TakePlayerDamage(int damage)
    {
        OnHealthChanged?.Invoke(-damage);
        if (damageFeedback != null)
            damageFeedback.Flash();

        CacheBrains();
        playerBrain?.OnPlayerDamaged(damage);
    }

    private void CacheBrains()
    {
        if (playerBrain == null)
            playerBrain = GetComponent<PlayerBrain>();
        if (brain == null)
            brain = GetComponent<RobotBrain>();
        if (memory == null)
            memory = GetComponent<RobotMemory>();
    }
}
