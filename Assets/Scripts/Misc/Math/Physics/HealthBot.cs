using System;
using UnityEngine;

public class HealthBot : MonoBehaviour
{
    public event Action<float> OnHealthChanged;

    [SerializeField] private DamageFeedback damageFeedback;
    [SerializeField] private RobotMemoryNew memory = null;
    [SerializeField] private RobotBrainNew brain = null;
    [SerializeField] private RobotMemoryNew memoryNew = null;
    [SerializeField] private RobotBrainNew brainNew = null;
    [SerializeField] private PlayerBrain playerBrain = null;

    public void TakeDamage(int damage)
    {
        Debug.Log("HealthBot: Health changed by " + damage);
        OnHealthChanged?.Invoke(-damage);
        if (damageFeedback != null)
            damageFeedback.Flash();

        CacheBrains();
        memory?.RegisterAttack();
        if (RobotNewPipelineRuntime.IsNewPipelineActive)
        {
            memoryNew?.RegisterAttack();
            brainNew?.OnDamageTaken(damage);
        }

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
            brain = GetComponent<RobotBrainNew>();
        if (memory == null)
            memory = GetComponent<RobotMemoryNew>();
        if (brainNew == null)
            brainNew = GetComponent<RobotBrainNew>();
        if (memoryNew == null)
            memoryNew = GetComponent<RobotMemoryNew>();
    }
}

