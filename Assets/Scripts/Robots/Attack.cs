using System;

// Base Attack class - can be extended for different types of attacks
[Serializable]
public class Attack
{
    public string AttackName;
    public int Damage;

    public Action<AttackRequest> ExecuteAction; // Delegate to hold the method to execute the attack

    public Attack(string attackName, int damage, Action<AttackRequest> executeAction)
    {
        AttackName = attackName;
        Damage = damage;
        ExecuteAction = executeAction;
    }

    // Method to execute the assigned action
    public void Execute(AttackRequest request)
    {
        ExecuteAction?.Invoke(request);
    }
}
