using System;
using System.Collections.Generic;
using UnityEngine;

// Ally Robot Factory
/// <summary>
/// Creates ally robot instances.
/// </summary>
public class AllyRobotFactory : RobotFactory
{
    public AllyRobotFactory()
    {
        health = 7;
        energy = 8;
        morality = 10;
        energyAttackCost = 1;
    }

    public override RobotStats CreateRobot()
    {
        return new RobotStats(health, health, energy, energy, energyAttackCost, morality, new List<Module>(modules), new List<Attack>(attacks));
    }
}
