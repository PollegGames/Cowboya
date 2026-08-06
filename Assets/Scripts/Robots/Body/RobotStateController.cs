using System;
using System.Collections;
using System.Collections.Generic;
using CowBoya.Robots;
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
    [SerializeField] private RobotMemoryNew memory;
    [SerializeField] private RobotBodyController bodyController;
    [SerializeField] private RobotAttackController attackController;
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
    private readonly Dictionary<Rigidbody2D, RigidbodyConstraints2D> defaultConstraints2D = new();
    private SimplePuppetBinder[] puppetBinders;
    private Rigidbody2D[] rigidbodies2D;
    private ICollectorTaskBody collectorBody;

    public WorkerCondition WorkerConditionState => workerCondition;
    [Header("Saving")]
    private static RoomWaypoint cachedStartWaypoint;
    private static bool triedCacheStart;

    /// <summary>
    /// Wires the core health, breakage, and memory references used by generated robot prefabs.
    /// </summary>
    public void ConfigureCoreReferences(HealthBot health, JointBreaker breaker, RobotMemoryNew robotMemory) {
        healthBot = health;
        jointBreaker = breaker;
        memory = robotMemory;
        collectorBody = GetComponent<ICollectorTaskBody>();
    }

    /// <summary>
    /// Restores terminal death effects after a temporary interaction resumes robot behaviours.
    /// </summary>
    public void ReapplyDeathState() {
        if (CurrentState == RobotState.Dead)
            ApplyEnemyDeathState();
    }

    private void Awake()
    {
        if (energyBot == null)
            energyBot = GetComponent<EnergyBot>();
        if (healthBot == null)
            healthBot = GetComponent<HealthBot>();
        if (jointBreaker == null)
            jointBreaker = GetComponent<JointBreaker>();
        if (memory == null)
            memory = GetComponent<RobotMemoryNew>();
        if (bodyController == null)
            bodyController = GetComponent<RobotBodyController>();
        if (attackController == null)
            attackController = GetComponent<RobotAttackController>();
        if (collectorBody == null)
            collectorBody = GetComponent<ICollectorTaskBody>();

        CacheDeathPhysicsDefaults();

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

        if (newState == RobotState.Dead)
        {
            ApplyEnemyDeathState();
            OnStateChanged?.Invoke(newState);
            jointBreaker?.BreakAll();
            ReportDeathOnce();
        }
        else
        {
            RestoreEnemyAliveState();
            OnStateChanged?.Invoke(newState);
            deathReported = false;
            savedReported = false;
        }

        var heart = GetComponent<RobotHeartNew>();
        if (heart != null && heart.Role == RobotRole.SecurityGuard)
        {
            Debug.Log($"[RobotStateController] SecurityGuard '{name}' state -> {newState}");
        }
    }

    public void SetInitialDeadState()
    {
        if (Stats != null)
        {
            Stats.CurrentHealth = 0f;
        }

        if (CurrentState != RobotState.Dead)
        {
            CurrentState = RobotState.Dead;
            deathReported = true;
            savedReported = false;
            ApplyEnemyDeathState();
            OnStateChanged?.Invoke(RobotState.Dead);
            jointBreaker?.BreakAll();
        }
    }

    public void OnAcquireFromPool()
    {
        bool stateChanged = CurrentState != RobotState.Alive;
        CurrentState = RobotState.Alive;
        deathReported = false;
        savedReported = false;
        workerCondition = WorkerCondition.Active;
        RestoreEnemyAliveState();
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

    private void ApplyEnemyDeathState()
    {
        if (IsPlayerRobot())
            return;

        memory?.SetDead(true);
        bodyController?.StopMovement();
        collectorBody?.StopAllActuators();
        attackController?.StopAttacking();
        SetPuppetBindersEnabled(false);
        ReleaseRotationConstraints();
    }

    private void RestoreEnemyAliveState()
    {
        if (IsPlayerRobot())
            return;

        memory?.SetDead(false);
        SetPuppetBindersEnabled(true);
        RestoreRotationConstraints();
    }

    private bool IsPlayerRobot()
    {
        return GetComponent<PlayerBrain>() != null;
    }

    private void CacheDeathPhysicsDefaults()
    {
        puppetBinders = GetComponentsInChildren<SimplePuppetBinder>(true);
        rigidbodies2D = GetComponentsInChildren<Rigidbody2D>(true);

        foreach (Rigidbody2D body in rigidbodies2D)
        {
            if (body != null && !defaultConstraints2D.ContainsKey(body))
                defaultConstraints2D.Add(body, body.constraints);
        }
    }

    private void SetPuppetBindersEnabled(bool enabled)
    {
        if (puppetBinders == null)
            CacheDeathPhysicsDefaults();

        foreach (SimplePuppetBinder binder in puppetBinders)
        {
            if (binder != null)
                binder.enabled = enabled;
        }
    }

    private void ReleaseRotationConstraints()
    {
        if (rigidbodies2D == null)
            CacheDeathPhysicsDefaults();

        foreach (Rigidbody2D body in rigidbodies2D)
        {
            if (body == null)
                continue;

            if (!defaultConstraints2D.ContainsKey(body))
                defaultConstraints2D.Add(body, body.constraints);

            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints &= ~RigidbodyConstraints2D.FreezeRotation;
        }
    }

    private void RestoreRotationConstraints()
    {
        foreach (var pair in defaultConstraints2D)
        {
            if (pair.Key != null)
                pair.Key.constraints = pair.Value;
        }
    }

}

