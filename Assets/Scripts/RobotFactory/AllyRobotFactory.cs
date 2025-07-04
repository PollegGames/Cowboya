using System;
using System.Collections.Generic;
using UnityEngine;

// Ally Robot Factory
public class AllyRobotFactory : RobotFactory
{
    public AllyRobotFactory()
    {
        health = 7;
        energy = 8;
        morality = 10;
        energyAttackCost = 1;
    }

    public override RobotInfo CreateRobot()
    {
        return new RobotInfo(health, health, energy, energy, energyAttackCost, morality, new List<Module>(modules), new List<Attack>(attacks));
    }
}