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
    [SerializeField] private JointBreaker jointBreaker;

    [SerializeField] private RobotStats stats;
    public RobotStats Stats
    {
        get => stats;
        set
        {
            stats = value;
            if (energyBot != null)
                energyBot.SetStats(stats);
        }
    }
    private bool isGrounded = true;
    private void Awake()
    {
        if (energyBot == null)
            energyBot = GetComponent<EnergyBot>();
        if (healthBot == null)
            healthBot = GetComponent<HealthBot>();
        if (jointBreaker == null)
            jointBreaker = GetComponent<JointBreaker>();

        if (healthBot != null)
            healthBot.OnHealthChanged += HandleHealthChange;
    }

    private void OnEnable()
    {
        if (energyBot != null)
            energyBot.SetStats(Stats);
    }

    public bool CanJump()
    {
        return isGrounded && CurrentState == RobotState.Alive;
    }

    public bool CanPerformAttack()
    {
        if (CurrentState != RobotState.Alive)
            return false;

        if (Stats == null)
            return false;

        if (energyBot != null)
            return energyBot.HasEnergyForAction(EnergyAction.Attack);

        return Stats != null && Stats.CurrentEnergy > Stats.AttackEnergyCost;
    }


    public bool CanPerformEnergy(float energyCost)
    {
        if (CurrentState != RobotState.Alive)
            return false;

        if (Stats == null)
            return false;

        if (energyBot != null)
            return energyBot.HasEnergy(energyCost);

        return Stats != null && Stats.CurrentEnergy > energyCost;
    }

    public bool CanPerformEnergy(EnergyAction action, float deltaTime = 0f)
    {
        if (CurrentState != RobotState.Alive)
            return false;

        if (Stats == null && energyBot == null)
            return false;

        if (energyBot != null)
            return energyBot.HasEnergyForAction(action, deltaTime);

        return true;
    }

    private IEnumerator ResetGrounded()
    {
        yield return new WaitForSeconds(2f); // Adjust based on jump duration
        isGrounded = true;
    }

    public void ConsumeEnergy(float amount)
    {
        energyBot?.TryConsumeRaw(amount);
    }

    public void PerformAttack(AttackType attackType)
    {
        if (CurrentState != RobotState.Alive || (Stats != null && !Stats.AbleToAttack))
            return;

        energyBot?.TryConsume(EnergyAction.Attack);
    }

    public bool PerformAttackByEnergy(float energyCost)
    {
        if (CurrentState != RobotState.Alive || Stats == null || !Stats.AbleToAttack)
        {
            return false;
        }

        if (energyBot != null)
            return energyBot.TryConsume(EnergyAction.Attack, 0f, energyCost);

        if (Stats == null)
            return false;

        if (energyCost <= 0f)
            return true;

        if (Stats.CurrentEnergy < energyCost)
            return false;

        Stats.UpdateEnergy(-energyCost);
        return true;
    }

    [Obsolete("Use PerformAttackByEnergy instead.")]
    public void PerformAttackbyEnergy(float energycost)
    {
        PerformAttackByEnergy(energycost);
    }

    private void HandleHealthChange(float healthChange)
    {
        if (Stats != null)
            Stats.UpdateHealth(healthChange);

        if (Stats != null && Stats.CurrentHealth <= 0)
        {
            UpdateState(RobotState.Dead);
        }
        else if (Stats == null && healthChange < 0f)
        {
            UpdateState(RobotState.Dead);
        }
    }

    public void UpdateState(RobotState newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        OnStateChanged?.Invoke(newState);

        if (newState == RobotState.Dead)
        {
            jointBreaker?.BreakAll();
        }

        var heart = GetComponent<RobotHeart>();
        if (heart != null && heart.Role == RobotRole.SecurityGuard)
        {
            Debug.Log($"[RobotStateController] SecurityGuard '{name}' state -> {newState}");
        }
    }

    /// <summary>
    /// Resets the robot when it is acquired from the pool.
    /// </summary>
    public void OnAcquireFromPool()
    {
        bool stateChanged = CurrentState != RobotState.Alive;
        CurrentState = RobotState.Alive;
        if (Stats != null)
        {
            Stats.CurrentHealth = Stats.MaxHealth;
            Stats.CurrentEnergy = Stats.MaxEnergy;
        }
        if (energyBot != null)
            energyBot.SetStats(Stats);
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
