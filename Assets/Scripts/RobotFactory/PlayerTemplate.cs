using System;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerTemplate", menuName = "Robot/PlayerTemplate")]
public class PlayerTemplate : RobotTemplate
{
    private RobotBehaviour robotBehaviour;
    public RobotBehaviour InitializeRobotBehaviour(GameObject robotInstance)
    {
        // Get RobotBehaviour component
        robotBehaviour = robotInstance.GetComponent<RobotBehaviour>();
        Debug.Log("RobotBehaviour initialized.");
        return robotBehaviour;
    }

    public RobotInfo InitializeRobotInfo(SaveData saveData)
    {
        // Use factory to create Robot instance
        PlayerRobotFactory playerFactory =
         new PlayerRobotFactory((int)saveData.MaxHealth, (int)saveData.MaxEnergy, 0, (int)saveData.AttackEnergyCost);

        robotBehaviour.RobotInfo = playerFactory.CreateRobot();
        Debug.Log("RobotInfo initialized with health: " + robotBehaviour.RobotInfo.CurrentHealth
        + " and energy: " + robotBehaviour.RobotInfo.CurrentEnergy 
        + " and attack energy cost: " + robotBehaviour.RobotInfo.AttackEnergyCost);

        return robotBehaviour.RobotInfo;
    }

}
