using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public class RobotStats
{
    public string RobotName;
    public float MaxHealth = 1f;
    public float CurrentHealth = 1f;
    public float MaxEnergy = 1f;
    public float CurrentEnergy = 1f;
    public float AttackEnergyCost = 1f;
    public float EnergyRechargeRate = 1f;
    public event Action OnHealthChanged;
    public event Action OnEnergyChanged;
    public event Action OnEnergyRechargeChanged;
    public event Action OnMoralityChanged;
    public bool AbleToAttack => CurrentEnergy >= AttackEnergyCost;
    public float Morality { get; set; } = 0f;
    public List<Module> Modules { get; set; } = new List<Module>();
    public List<Attack> Attacks { get; set; } = new List<Attack>();

    public RobotStats() { }
    public RobotStats(
        float currentHealth,
        float maxHealth,
        float currentEnergy,
        float maxEnergy,
        float attackEnergyCost,
        float morality,
        List<Module> modules,
        List<Attack> attacks)
    {
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        CurrentEnergy = currentEnergy;
        MaxEnergy = maxEnergy;
        AttackEnergyCost =attackEnergyCost;
        Morality = morality;
        Modules = modules ?? new List<Module>();
        Attacks = attacks ?? new List<Attack>();
    }

    public void UpdateEnergy(float delta)
    {
        CurrentEnergy = Mathf.Clamp(CurrentEnergy + delta, 0, MaxEnergy);
        OnEnergyChanged?.Invoke();
    }

    /// <summary>
    /// Adjusts the energy recharge rate by the provided amount.
    /// </summary>
    /// <param name="delta">Amount to change recharge rate.</param>
    public void UpdateEnergyRecharge(float delta)
    {
        EnergyRechargeRate = math.max(0f, EnergyRechargeRate + delta);
        OnEnergyRechargeChanged?.Invoke();
    }

    public void UpdateHealth(float delta)
    {
        CurrentHealth = Mathf.Clamp(CurrentHealth + delta, 0, MaxHealth);
        OnHealthChanged?.Invoke();
    }

    public void UpdateMorality(float delta)
    {
        Morality += delta;
        OnMoralityChanged?.Invoke();
    }

    public void ResetMorality()
    {
        Morality = 0f;
        OnMoralityChanged?.Invoke();
    }
}
