using System;
using System.Collections.Generic;
using UnityEngine;

// Enemy Robot Factory
public class EnemyRobotFactory : RobotFactory
{
    public EnemyRobotFactory()
    {
        health = 5;
        energy = 5;
        morality = -10;
        energyAttackCost = 1;
    }

    public override PlayerStats CreateRobot()
    {
        return new PlayerStats(health, health, energy, energy, energyAttackCost,morality, new List<Module>(modules), new List<Attack>(attacks));
    }
}