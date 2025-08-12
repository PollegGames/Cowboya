using System;
using System.Collections.Generic;
using UnityEngine;

// Enemy Robot Factory
public class WorkerRobotFactory : RobotFactory
{
    public WorkerRobotFactory()
    {
        health = 30;
        energy = 30;
        morality = -1;
        energyAttackCost = 1;
    }

    public override RobotStats CreateRobot()
    {
        return new RobotStats(health, health, energy, energy, energyAttackCost,morality, new List<Module>(modules), new List<Attack>(attacks));
    }
}