using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerTemplate", menuName = "Robot/PlayerTemplate")]
public class PlayerTemplate : RobotTemplate
{
    private RobotStateController robotBehaviour;
    private float currentHealth;
    private float currentEnergy;
    private float currentMorality;

    private const float MinMorality = -100f;
    private const float MaxMorality = 100f;
    public RobotStateController InitializePlayerStateController(GameObject robotInstance)
    {
        robotBehaviour = robotInstance.GetComponent<RobotStateController>();
        Debug.Log("PlayerStateController initialized.");
        return robotBehaviour;
    }

    /// <summary>
    /// Initializes the player's stats based on save data.
    /// </summary>
    public RobotStats InitializePlayerStats(SaveData saveData)
    {
        PlayerRobotFactory playerFactory =
            new PlayerRobotFactory((int)saveData.MaxHealth, (int)saveData.MaxEnergy, 0, (int)saveData.AttackEnergyCost);

        robotBehaviour.Stats = playerFactory.CreateRobot();
        Debug.Log("PlayerStats initialized with health: " + robotBehaviour.Stats.CurrentHealth
        + " and energy: " + robotBehaviour.Stats.CurrentEnergy
        + " and attack energy cost: " + robotBehaviour.Stats.AttackEnergyCost);

        return robotBehaviour.Stats;
    }

    /// <summary>
    /// Captures current robot stats from the active player.
    /// </summary>
    public void CaptureStats(RobotStats source)
    {
        if (source == null)
        {
            return;
        }

        currentHealth = Mathf.Clamp(source.CurrentHealth, 0f, source.MaxHealth);
        currentEnergy = Mathf.Clamp(source.CurrentEnergy, 0f, source.MaxEnergy);
        currentMorality = Mathf.Clamp(source.Morality, MinMorality, MaxMorality);
    }

    /// <summary>
    /// Applies stored stats to the target player.
    /// </summary>
    public void ApplyStats(RobotStats target)
    {
        if (target == null)
        {
            return;
        }

        float healthTarget = Mathf.Clamp(currentHealth, 0f, target.MaxHealth);
        float energyTarget = Mathf.Clamp(currentEnergy, 0f, target.MaxEnergy);
        float moralityTarget = Mathf.Clamp(currentMorality, MinMorality, MaxMorality);

        target.UpdateHealth(healthTarget - target.CurrentHealth);
        target.UpdateEnergy(energyTarget - target.CurrentEnergy);
        target.UpdateMorality(moralityTarget - target.Morality);
    }

    /// <summary>
    /// Resets the stored stats to default values.
    /// </summary>
    public void ResetStats()
    {
        currentHealth = 0f;
        currentEnergy = 0f;
        currentMorality = 0f;
    }
}
