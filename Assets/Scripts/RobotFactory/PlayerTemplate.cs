using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerTemplate", menuName = "Robot/PlayerTemplate")]
public class PlayerTemplate : RobotTemplate
{
    private PlayerStateController robotBehaviour;
    public PlayerStateController InitializePlayerStateController(GameObject robotInstance)
    {
        // Get PlayerStateController component
        robotBehaviour = robotInstance.GetComponent<PlayerStateController>();
        Debug.Log("PlayerStateController initialized.");
        return robotBehaviour;
    }

    public PlayerStats InitializePlayerStats(SaveData saveData)
    {
        // Use factory to create Robot instance
        PlayerRobotFactory playerFactory =
         new PlayerRobotFactory((int)saveData.MaxHealth, (int)saveData.MaxEnergy, 0, (int)saveData.AttackEnergyCost);

        robotBehaviour.Stats = playerFactory.CreateRobot();
        Debug.Log("PlayerStats initialized with health: " + robotBehaviour.Stats.CurrentHealth
        + " and energy: " + robotBehaviour.Stats.CurrentEnergy
        + " and attack energy cost: " + robotBehaviour.Stats.AttackEnergyCost);

        return robotBehaviour.Stats;
    }

}
