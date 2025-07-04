using System;
using System.Collections;
using UnityEngine;
public class RobotBehaviour : MonoBehaviour
{
    public event Action<RobotState> OnStateChanged;
    public RobotState CurrentState { get; private set; } = RobotState.Alive;

    [SerializeField] private EnergyBot energyBot;
    [SerializeField] private HealthBot healthBot;
    public HealthBot Health => healthBot;

    [SerializeField] public RobotInfo RobotInfo;
    private bool isGrounded = true;

    [Header("Hips Rigidbody")]
    [SerializeField] private Rigidbody2D hips;
    private void Awake()
    {
        if (energyBot == null) energyBot = GetComponent<EnergyBot>();
        if (healthBot == null) healthBot = GetComponent<HealthBot>();

        energyBot.OnEnergyNeeded += HandleEnergyConsumption;
        healthBot.OnHealthChanged += HandleHealthChange;
        if (hips == null) hips = GetComponentInChildren<Rigidbody2D>();
    }

    public bool CanJump()
    {
        return isGrounded && CurrentState == RobotState.Alive;
    }

    public bool CanPerformAttack()
    {
        return RobotInfo.CurrentEnergy > RobotInfo.AttackEnergyCost && CurrentState == RobotState.Alive;
    }


    public bool CanPerformEnergy(float energyCost)
    {
        return RobotInfo.CurrentEnergy > energyCost && CurrentState == RobotState.Alive;
    }

    public void HandleJump(float jumpForce)
    {
        if (!CanJump()) return;

        Debug.Log("RobotBehaviour: Jumping with force " + jumpForce);
        isGrounded = false;
        hips.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        StartCoroutine(ResetGrounded());
    }

    private IEnumerator ResetGrounded()
    {
        yield return new WaitForSeconds(2f); // Adjust based on jump duration
        isGrounded = true;
    }
    public void ConsumeEnergy(float amount)
    {
        energyBot.RechargingEnergy(-amount); // Recharge logique gérée dans EnergyBot
    }

    public void PerformAttack(AttackType attackType)
    {
        if (CurrentState != RobotState.Alive || !RobotInfo.AbleToAttack) return;

        energyBot.RechargingEnergy(-RobotInfo.AttackEnergyCost);
    }

    public void PerformAttackbyEnergy(float energycost)
    {
        if (CurrentState != RobotState.Alive || !RobotInfo.AbleToAttack) return;

        energyBot.RechargingEnergy(-energycost);
    }
    private void HandleEnergyConsumption(float energyChange)
    {
        // Prevent state changes if dead
        if (CurrentState == RobotState.Dead)
            return;

        RobotInfo.UpdateEnergy(energyChange);
        if (RobotInfo.CurrentEnergy == 0)
        {
            UpdateState(RobotState.Faint);
        }
        else if (RobotInfo.CurrentEnergy >= RobotInfo.AttackEnergyCost)
        {
            UpdateState(RobotState.Alive);
        }
    }

    private void HandleHealthChange(float healthChange)
    {
        RobotInfo.UpdateHealth(healthChange);
        if (RobotInfo.CurrentHealth <= 0)
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

}