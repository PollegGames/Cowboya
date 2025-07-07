
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

    public abstract RobotStats CreateRobot();

    public List<RobotStats> CreateMany(int count)
    {
        List<RobotStats> robots = new List<RobotStats>();
        for (int i = 0; i < count; i++)
        {
            robots.Add(CreateRobot());
        }
        return robots;
    }
}