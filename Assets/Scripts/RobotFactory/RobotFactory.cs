
using System;
using System.Collections.Generic;
using UnityEngine;

// Factory class to create different types of robots
public abstract class RobotFactory
{
    protected int health = 1;
    protected int energy = 1;
    protected int morality = 0;
    protected int energyAttackCost = 1;
    protected List<Module> modules = new List<Module>();
    protected List<Attack> attacks = new List<Attack>();

    public abstract RobotInfo CreateRobot();

    public List<RobotInfo> CreateMany(int count)
    {
        List<RobotInfo> robots = new List<RobotInfo>();
        for (int i = 0; i < count; i++)
        {
            robots.Add(CreateRobot());
        }
        return robots;
    }
}