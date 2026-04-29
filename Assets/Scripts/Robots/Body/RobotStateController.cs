using System;
using System.Collections;
using UnityEngine;

public class RobotStateController : MonoBehaviour, IPooledObject
{
    public event Action<RobotState> OnStateChanged;
    public static event Action<RobotStateController> OnAnyRobotKilled;
    public static event Action<RobotStateController> OnAnyRobotSaved;
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
            if (stats != null)
                stats.OnHealthChanged -= HandleStatsHealthChanged;

            stats = value;
            if (energyBot != null)
                energyBot.SetStats(stats);

            if (stats != null)
                stats.OnHealthChanged += HandleStatsHealthChanged;

            EvaluateHealthState();
        }
    }

    private bool isGrounded = true;
    private bool deathReported;
    private bool savedReported;
    private WorkerCondition workerCondition = WorkerCondition.Active;

    public WorkerCondition WorkerConditionState => workerCondition;
    [Header("Saving")]
    private static RoomWaypoint cachedStartWaypoint;
    private static bool triedCacheStart;

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

        deathReported = false;
        savedReported = false;
    }

    private void OnEnable()
    {
        if (energyBot != null)
            energyBot.SetStats(Stats);

        if (Stats != null)
        {
            Stats.OnHealthChanged -= HandleStatsHealthChanged;
            Stats.OnHealthChanged += HandleStatsHealthChanged;
        }

        EvaluateHealthState();
    }

    private void OnDisable()
    {
        if (Stats != null)
            Stats.OnHealthChanged -= HandleStatsHealthChanged;
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
        yield return new WaitForSeconds(2f);
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
            ReportDeathOnce();
        }
        else
        {
            deathReported = false;
            savedReported = false;
        }

        var heart = GetComponent<RobotHeartNew>();
        if (heart != null && heart.Role == RobotRole.SecurityGuard)
        {
            Debug.Log($"[RobotStateController] SecurityGuard '{name}' state -> {newState}");
        }
    }

    public void OnAcquireFromPool()
    {
        bool stateChanged = CurrentState != RobotState.Alive;
        CurrentState = RobotState.Alive;
        deathReported = false;
        savedReported = false;
        workerCondition = WorkerCondition.Active;
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

    public void OnReleaseToPool()
    {
        OnStateChanged = null;

        if (Stats != null)
            Stats.OnHealthChanged -= HandleStatsHealthChanged;
    }

    private void HandleStatsHealthChanged()
    {
        EvaluateHealthState();
    }

    private void EvaluateHealthState()
    {
        if (Stats != null && Stats.CurrentHealth <= 0f)
        {
            UpdateState(RobotState.Dead);
        }
    }

    private void ReportDeathOnce()
    {
        if (deathReported)
            return;

        if (GetComponent<PlayerBrain>() != null)
            return;

        deathReported = true;
        OnAnyRobotKilled?.Invoke(this);
    }

    public void MarkAsSaved()
    {
        if (savedReported)
            return;

        if (GetComponent<PlayerBrain>() != null)
            return;

        savedReported = true;
        OnAnyRobotSaved?.Invoke(this);
    }

}

