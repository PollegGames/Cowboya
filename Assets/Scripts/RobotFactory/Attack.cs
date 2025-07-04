using System;
using System.Collections.Generic;
using UnityEngine;

// Base Attack class - can be extended for different types of attacks
[Serializable]
public class Attack
{
    public string AttackName;
    public int Damage;

    public Action ExecuteAction; // Delegate to hold the method to execute the attack

    public Attack(string attackName, int damage, Action executeAction)
    {
        AttackName = attackName;
        Damage = damage;
        ExecuteAction = executeAction;
    }

    // Method to execute the assigned action
    public void Execute()
    {
        ExecuteAction?.Invoke();
    }
}
