using System;
using System.Collections.Generic;
using UnityEngine;

// Player Robot Factory
public class PlayerRobotFactory : RobotFactory
{
    public PlayerRobotFactory()
    {
        health = 100;
        energy = 100;
        energyAttackCost=5;
        morality = 0;
    }

    public PlayerRobotFactory(int healtFromSave, int energyFromSave, int moralityFromSave, int energyAttackCostFromSave)
    {
        health = healtFromSave;
        energy = energyFromSave;
        morality = moralityFromSave;
        energyAttackCost = energyAttackCostFromSave;
    }

    public override RobotStats CreateRobot()
    {
        return new RobotStats(health, health, energy, energy,energyAttackCost, morality, new List<Module>(modules), new List<Attack>(attacks));
    }
}