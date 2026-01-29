using System;
using System.Collections.Generic;
using UnityEngine;

// Player Robot Factory
/// <summary>
/// Creates player robot instances.
/// </summary>
public class PlayerRobotFactory : RobotFactory
{
    private int currentHealth;
    private int currentEnergy;

    public PlayerRobotFactory()
    {
        health = 100;
        currentHealth = health;
        energy = 100;
        currentEnergy = energy;
        energyAttackCost = 5;
        morality = 0;
    }

    public PlayerRobotFactory(
        int currentHealthFromSave,
        int maxHealthFromSave,
        int currentEnergyFromSave,
        int maxEnergyFromSave,
        int moralityFromSave,
        int energyAttackCostFromSave)
    {
        currentHealth = currentHealthFromSave;
        health = maxHealthFromSave;
        currentEnergy = currentEnergyFromSave;
        energy = maxEnergyFromSave;
        morality = moralityFromSave;
        energyAttackCost = energyAttackCostFromSave;
    }

    public override RobotStats CreateRobot()
    {
        return new RobotStats(
            currentHealth,
            health,
            currentEnergy,
            energy,
            energyAttackCost,
            morality,
            new List<Module>(modules),
            new List<Attack>(attacks));
    }
}
