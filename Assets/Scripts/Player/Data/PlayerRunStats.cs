using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerRunStats", menuName = "Player/RunStats")]
public class PlayerRunStats : ScriptableObject
{
    [FormerlySerializedAs("currentHealth")] public float CurrentHealth;
    [FormerlySerializedAs("maxHealth")] public float MaxHealth;
    [FormerlySerializedAs("maxEnergy")] public float MaxEnergy;
    [FormerlySerializedAs("energyRechargeRate")] public float EnergyRechargeRate;
    [FormerlySerializedAs("attackDamage")] public int AttackDamage;
    [FormerlySerializedAs("morality")] public float Morality;

    public float MaxHealthBonus;
    public float MaxEnergyBonus;
    public int AttackDamageBonus;

    private bool hasValues;

    public bool HasValues => hasValues;

    /// <summary>
    /// Captures temporary stats from the source robot and related systems.
    /// </summary>
    /// <param name="source">Robot providing current values.</param>
    /// <param name="energyBot">Energy system supplying recharge rate.</param>
    /// <param name="attack">Attack providing damage values.</param>
    public void Capture(RobotStats source, EnergyBot energyBot, Attack attack)
    {
        if (source == null)
        {
            return;
        }
        CurrentHealth = Mathf.Clamp(source.CurrentHealth, 0f, source.MaxHealth);
        MaxHealth = source.MaxHealth;
        MaxEnergy = source.MaxEnergy;
        Morality = source.Morality;
        if (energyBot != null)
        {
            EnergyRechargeRate = energyBot.rechargeRate;
        }
        if (attack != null)
        {
            AttackDamage = attack.Damage;
        }

        hasValues = true;
    }

    /// <summary>
    /// Applies captured stats to the target robot and related systems.

    /// </summary>
    /// <param name="target">Robot receiving stored values.</param>
    /// <param name="energyBot">Energy system to update recharge rate.</param>
    /// <param name="attack">Attack to update with stored damage.</param>
    public void Apply(RobotStats target, EnergyBot energyBot, Attack attack)
    {
        if (target == null || !hasValues)
        {
            return;
        }

        target.MaxHealth = MaxHealth;
        target.MaxEnergy = MaxEnergy;
        float healthTarget = Mathf.Clamp(CurrentHealth, 0f, target.MaxHealth);
        target.UpdateHealth(healthTarget - target.CurrentHealth);
        target.UpdateMorality(Morality - target.Morality);
        if (energyBot != null)
        {
            energyBot.rechargeRate = EnergyRechargeRate;
        }
        if (attack != null)
        {
            attack.Damage = AttackDamage;
        }

    }

    /// <summary>
    /// Clears the stored run statistics.
    /// </summary>
    public void Reset()
    {
        CurrentHealth = 0f;
        MaxHealth = 0f;
        MaxEnergy = 0f;
        EnergyRechargeRate = 0f;
        AttackDamage = 0;
        Morality = 0f;
        MaxHealthBonus = 0f;
        MaxEnergyBonus = 0f;
        AttackDamageBonus = 0;

        hasValues = false;
    }
}
