using System;
using UnityEngine;

public class HealthBot : MonoBehaviour
{
    public event Action<float> OnHealthChanged;
    [SerializeField] private DamageFeedback damageFeedback;
    [SerializeField] private RobotMemory memory = null;

    public void TakeDamage(int damage)
    {
        Debug.Log("HealthBot: Health changed by " + damage);
        OnHealthChanged?.Invoke(-damage);
        damageFeedback.Flash();
        memory.RegisterAttack();
    }


}
