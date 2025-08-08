using System;
using System.Collections.Generic;
using UnityEngine;

// Enemy Robot Factory
public class EnemyRobotFactory : RobotFactory
{
    public EnemyRobotFactory(int multiplierValues  =1)
    {
        health = 20 * multiplierValues;
        energy = 30 * multiplierValues;
        morality = -10;
        energyAttackCost = 5;
    }

    public override RobotStats CreateRobot()
    {
        return new RobotStats(health, health, energy, energy, energyAttackCost,morality, new List<Module>(modules), new List<Attack>(attacks));
    }
}