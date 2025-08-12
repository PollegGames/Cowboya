using UnityEngine;

[CreateAssetMenu(fileName = "PlayerRunStats", menuName = "Player/RunStats")]
public class PlayerRunStats : ScriptableObject
{
    public float CurrentHealth;
    public float Morality;
    public float MaxHealthBonus;
    public float MaxEnergyBonus;
    public int AttackDamageBonus;
    private bool hasValues;

    public bool HasValues => hasValues;

    /// <summary>
    /// Captures temporary stats from the source robot.
    /// </summary>
    /// <param name="source">Robot providing current values.</param>
    public void Capture(RobotStats source)
    {
        if (source == null)
        {
            return;
        }

        CurrentHealth = Mathf.Clamp(source.CurrentHealth, 0f, source.MaxHealth);
        Morality = source.Morality;
        hasValues = true;
    }

    /// <summary>
    /// Applies captured stats and accumulated upgrades to the target robot.
    /// </summary>
    /// <param name="target">Robot receiving stored values.</param>
    public void Apply(RobotStats target)
    {
        if (target == null || !hasValues)
        {
            return;
        }

        target.MaxHealth += MaxHealthBonus;
        target.MaxEnergy += MaxEnergyBonus;
        foreach (Attack attack in target.Attacks)
        {
            attack.Damage += AttackDamageBonus;
        }

        float healthTarget = Mathf.Clamp(CurrentHealth, 0f, target.MaxHealth);
        target.UpdateHealth(healthTarget - target.CurrentHealth);
        target.UpdateMorality(Morality - target.Morality);
    }

    /// <summary>
    /// Clears the stored run statistics.
    /// </summary>
    public void Reset()
    {
        CurrentHealth = 0f;
        Morality = 0f;
        MaxHealthBonus = 0f;
        MaxEnergyBonus = 0f;
        AttackDamageBonus = 0;
        hasValues = false;
    }
}
