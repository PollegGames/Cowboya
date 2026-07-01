using System;
using System.Collections;
using UnityEngine;

public class EnergyBot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RobotStateController stateController;
    [SerializeField] private RobotStats stats;
    [SerializeField] private PlayerBrain playerBrain;

    [Header("Energy Settings")]
    [SerializeField] private bool consumeEnergy = true;
    [SerializeField] private bool autoRecharge = true;
    [SerializeField] private float rechargeRate = 2f;
    [SerializeField] private float tickDelay = 1f;
    [SerializeField] private float rechargeDelayAfterUse = 0.5f;
    [SerializeField] private float faintRecoveryEnergy = 20f;

    [Header("Action Costs")]
    [SerializeField] private float walkEnergyCostPerSecond = 0.25f;
    [SerializeField] private float jumpEnergyCost = 3f;
    [SerializeField] private float crouchEnergyCostPerSecond = 0.2f;
    [SerializeField] private float grabEnergyCost = 1f;

    private Coroutine rechargeCoroutine;
    private bool playerControlled;
    private float nextRechargeStartTime;

    public event Action<float, float> OnEnergyChanged;

    public float RechargeRate
    {
        get => rechargeRate;
        set => rechargeRate = Mathf.Max(0f, value);
    }

    public float FaintRecoveryThreshold => GetFaintRecoveryThreshold();

    public bool AutoRechargeEnabled => autoRecharge;

    private void Awake()
    {
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();
        if (playerBrain == null)
            playerBrain = GetComponent<PlayerBrain>();
        playerControlled = playerBrain != null;
        if (stats == null && stateController != null)
            stats = stateController.Stats;
        SyncRechargeRateFromStats();
        nextRechargeStartTime = Time.time;
    }

    private void OnEnable()
    {
        stats ??= stateController != null ? stateController.Stats : null;
        SyncRechargeRateFromStats();
        StartRechargeIfNeeded();
    }

    private void OnDisable()
    {
        StopRecharge();
    }

    public void SetStats(RobotStats robotStats)
    {
        stats = robotStats;
        SyncRechargeRateFromStats();
        StartRechargeIfNeeded();
    }

    public void SetPlayerMode(bool isPlayerControlled)
    {
        playerControlled = isPlayerControlled;
    }

    public void SetAutoRecharge(bool enabled)
    {
        autoRecharge = enabled;
        if (!autoRecharge)
        {
            StopRecharge();
            return;
        }

        nextRechargeStartTime = Time.time;
        StartRechargeIfNeeded();
    }

    public void SetCurrentEnergy(float value)
    {
        if (stats == null)
            return;

        float target = Mathf.Clamp(value, 0f, stats.MaxEnergy);
        ApplyEnergyChange(target - stats.CurrentEnergy);
    }

    public void SetActionCost(EnergyAction action, float cost)
    {
        switch (action)
        {
            case EnergyAction.Walk:
                walkEnergyCostPerSecond = Mathf.Max(0f, cost);
                break;
            case EnergyAction.Jump:
                jumpEnergyCost = Mathf.Max(0f, cost);
                break;
            case EnergyAction.Crouch:
                crouchEnergyCostPerSecond = Mathf.Max(0f, cost);
                break;
            case EnergyAction.Attack:
                // attackEnergyCost = Mathf.Max(0f, cost);
                break;
            case EnergyAction.Grab:
                grabEnergyCost = Mathf.Max(0f, cost);
                break;
        }
    }

    public bool HasEnergy(float energyCost)
    {
        if (!consumeEnergy || stats == null)
            return true;

        if (stateController != null && stateController.CurrentState == RobotState.Dead)
            return false;

        return stats.CurrentEnergy >= energyCost;
    }

    public bool HasEnergyForAction(EnergyAction action, float deltaTime = 0f, float? overrideCost = null)
    {
        float cost = ResolveActionCost(action, deltaTime, overrideCost);
        return HasEnergy(cost);
    }

    public bool TryConsume(EnergyAction action, float deltaTime = 0f, float? overrideCost = null)
    {
        float cost = ResolveActionCost(action, deltaTime, overrideCost);
        return TryConsumeRaw(cost);
    }

    public bool TryConsumeRaw(float energyCost)
    {
        if (!consumeEnergy || energyCost <= 0f)
            return true;

        if (!HasEnergy(energyCost))
        {
            HandleEnergyDepleted();
            return false;
        }

        RegisterEnergySpend();
        ApplyEnergyChange(-energyCost);
        return true;
    }

    private float ResolveActionCost(EnergyAction action, float deltaTime, float? overrideCost)
    {
        if (overrideCost.HasValue)
            return Mathf.Max(0f, overrideCost.Value);

        float timeFactor = deltaTime > 0f ? deltaTime : (Time.deltaTime > 0f ? Time.deltaTime : 1f);

        switch (action)
        {
            case EnergyAction.Walk:
                return walkEnergyCostPerSecond * timeFactor;
            case EnergyAction.Jump:
                return jumpEnergyCost;
            case EnergyAction.Crouch:
                return crouchEnergyCostPerSecond * timeFactor;
            case EnergyAction.Attack:
                return stats != null
                        ? stats.AttackEnergyCost
                        : 0f;
            case EnergyAction.Grab:
                return grabEnergyCost;
            default:
                return 0f;
        }
    }

    private void ApplyEnergyChange(float delta)
    {
        if (stats == null)
            return;

        stats.UpdateEnergy(delta);
        OnEnergyChanged?.Invoke(stats.CurrentEnergy, stats.MaxEnergy);

        if (stats.CurrentEnergy <= 0f)
        {
            HandleEnergyDepleted();
            return;
        }

        if (stateController != null
            && stateController.CurrentState == RobotState.Faint
            && stats.CurrentEnergy >= GetFaintRecoveryThreshold())
        {
            stateController.UpdateState(RobotState.Alive);
        }

        StartRechargeIfNeeded();
    }

    private void HandleEnergyDepleted()
    {
        if (stateController != null && stateController.CurrentState != RobotState.Dead)
            stateController.UpdateState(RobotState.Faint);
    }

    private void StartRechargeIfNeeded()
    {
        if (!autoRecharge || stats == null)
            return;

        if (stats.CurrentEnergy >= stats.MaxEnergy)
            return;

        if (rechargeCoroutine == null)
            rechargeCoroutine = StartCoroutine(RechargeCoroutine());
    }

    private void StopRecharge()
    {
        if (rechargeCoroutine != null)
        {
            StopCoroutine(rechargeCoroutine);
            rechargeCoroutine = null;
        }
    }

    private IEnumerator RechargeCoroutine()
    {
        while (autoRecharge && stats != null && (stateController == null || stateController.CurrentState != RobotState.Dead))
        {
            if (Time.time < nextRechargeStartTime)
            {
                yield return new WaitForSeconds(Mathf.Max(0.01f, nextRechargeStartTime - Time.time));
                continue;
            }

            float missing = stats.MaxEnergy - stats.CurrentEnergy;
            if (missing <= 0.001f)
                break;

            float amountThisTick = Mathf.Min(rechargeRate * tickDelay, missing);
            ApplyEnergyChange(amountThisTick);

            if (stats.MaxEnergy - stats.CurrentEnergy <= 0.001f)
                break;

            float delay = tickDelay > 0f ? tickDelay : 0.01f;
            yield return new WaitForSeconds(delay);
        }

        rechargeCoroutine = null;
    }

    private void RegisterEnergySpend()
    {
        nextRechargeStartTime = Time.time + rechargeDelayAfterUse;
        StopRecharge();
        StartRechargeIfNeeded();
    }

    private void SyncRechargeRateFromStats()
    {
        if (stats == null)
            return;

        if (stats.EnergyRechargeRate > 0f)
            rechargeRate = stats.EnergyRechargeRate;
    }

    private float GetFaintRecoveryThreshold()
    {
        float attackCost = stats != null ? stats.AttackEnergyCost : 0f;
        return Mathf.Max(faintRecoveryEnergy, attackCost);
    }
}
