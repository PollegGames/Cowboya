using System;
using System.Collections;
using UnityEngine;
public class RobotStateController : MonoBehaviour, IPooledObject
{
    public event Action<RobotState> OnStateChanged;
    public RobotState CurrentState { get; private set; } = RobotState.Alive;

    [SerializeField] private EnergyBot energyBot;
    [SerializeField] private HealthBot healthBot;
    public HealthBot Health => healthBot;

    [SerializeField] public RobotStats Stats;
    private bool isGrounded = true;

    private void Awake()
    {
        if (energyBot == null) energyBot = GetComponent<EnergyBot>();
        if (healthBot == null) healthBot = GetComponent<HealthBot>();

        energyBot.OnEnergyNeeded += HandleEnergyConsumption;
        healthBot.OnHealthChanged += HandleHealthChange;
    }

    public bool CanJump()
    {
        return isGrounded && CurrentState == RobotState.Alive;
    }

    public bool CanPerformAttack()
    {
        return Stats.CurrentEnergy > Stats.AttackEnergyCost && CurrentState == RobotState.Alive;
    }


    public bool CanPerformEnergy(float energyCost)
    {
        return Stats.CurrentEnergy > energyCost && CurrentState == RobotState.Alive;
    }

    private IEnumerator ResetGrounded()
    {
        yield return new WaitForSeconds(2f); // Adjust based on jump duration
        isGrounded = true;
    }
    public void ConsumeEnergy(float amount)
    {
        energyBot.RechargingEnergy(-amount); // Logical recharge handled in EnergyBot
    }

    public void PerformAttack(AttackType attackType)
    {
        if (CurrentState != RobotState.Alive || !Stats.AbleToAttack) return;

        energyBot.RechargingEnergy(-Stats.AttackEnergyCost);
    }

    public bool PerformAttackByEnergy(float energyCost)
    {
        if (CurrentState != RobotState.Alive || !Stats.AbleToAttack)
        {
            return false;
        }

        if (Stats == null || energyBot == null)
        {
            return false;
        }

        if (energyCost <= 0f)
        {
            return true;
        }

        if (Stats.CurrentEnergy < energyCost)
        {
            return false;
        }

        energyBot.RechargingEnergy(-energyCost);
        return true;
    }

    [Obsolete("Use PerformAttackByEnergy instead.")]
    public void PerformAttackbyEnergy(float energycost)
    {
        PerformAttackByEnergy(energycost);
    }
    private void HandleEnergyConsumption(float energyChange)
    {
        // Prevent state changes if dead
        if (CurrentState == RobotState.Dead)
            return;

        Stats.UpdateEnergy(energyChange);
        if (Stats.CurrentEnergy == 0)
        {
            UpdateState(RobotState.Faint);
        }
        else if (Stats.CurrentEnergy >= Stats.AttackEnergyCost)
        {
            UpdateState(RobotState.Alive);
        }
    }

    private void HandleHealthChange(float healthChange)
    {
        Stats.UpdateHealth(healthChange);
        if (Stats.CurrentHealth <= 0)
        {
            UpdateState(RobotState.Dead);
        }
    }

    public void UpdateState(RobotState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);
    }

    /// <summary>
    /// Resets the robot when it is acquired from the pool.
    /// </summary>
    public void OnAcquireFromPool()
    {
        bool stateChanged = CurrentState != RobotState.Alive;
        CurrentState = RobotState.Alive;
        Stats.CurrentHealth = Stats.MaxHealth;
        Stats.CurrentEnergy = Stats.MaxEnergy;
        if (stateChanged)
        {
            OnStateChanged?.Invoke(RobotState.Alive);
        }
    }

    /// <summary>
    /// Performs cleanup when the robot is released to the pool.
    /// </summary>
    public void OnReleaseToPool()
    {
        OnStateChanged = null;
    }
}
