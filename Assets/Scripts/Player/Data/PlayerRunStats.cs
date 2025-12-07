using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "PlayerRunStats", menuName = "Player/RunStats")]
public class PlayerRunStats : ScriptableObject
{
    public float CurrentHealth;
    public float CurrentEnergy;
    public float MaxHealth;
    public float MaxEnergy;
    public float EnergyRechargeRate;
    public int AttackDamage;
    public float Morality;
    public float MaxHealthBonus;
    public float MaxEnergyBonus;
    public float EnergyRechargeBonus;
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
        CurrentEnergy = Mathf.Clamp(source.MaxEnergy, 0f, source.MaxEnergy);
        MaxHealth = source.MaxHealth;
        MaxEnergy = source.MaxEnergy;
        Morality = source.Morality;
        if (energyBot != null)
        {
            EnergyRechargeRate = energyBot.RechargeRate;
        }
        else
        {
            EnergyRechargeRate = source.EnergyRechargeRate;
        }
        if (attack != null)
        {
            AttackDamage = attack.Damage;
        }


        hasValues = true;
    }

    public void Capture(RobotStats source)
    {
        Capture(source, null, null);
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

        target.MaxHealth = MaxHealth + MaxHealthBonus;
        target.MaxEnergy = MaxEnergy + MaxEnergyBonus;
        float healthTarget = Mathf.Clamp(CurrentHealth + MaxHealthBonus, 0f, target.MaxHealth);
        float energyTarget = Mathf.Clamp(MaxEnergy + MaxEnergyBonus, 0f, target.MaxEnergy);
        target.UpdateHealth(healthTarget - target.CurrentHealth);
        target.UpdateEnergy(energyTarget - target.CurrentEnergy);
        target.UpdateMorality(Morality - target.Morality);
        target.EnergyRechargeRate = EnergyRechargeRate + EnergyRechargeBonus;

        if (energyBot != null)
        {
            energyBot.RechargeRate = EnergyRechargeRate + EnergyRechargeBonus;
        }

        if (attack != null)
        {
            attack.Damage = AttackDamage + AttackDamageBonus;
        }
    }

    /// <summary>
    /// Clears the stored run statistics.
    /// </summary>
    public void Reset()
    {
        CurrentHealth = 0f;
        CurrentEnergy = 0f;
        MaxHealth = 0f;
        MaxEnergy = 0f;
        EnergyRechargeRate = 0f;
        AttackDamage = 5;
        Morality = 0f;
        MaxHealthBonus = 0f;
        MaxEnergyBonus = 0f;
        EnergyRechargeBonus = 0f;
        AttackDamageBonus = 0;

        hasValues = false;
    }

    /// <summary>
    /// Adds to the energy recharge bonus accumulated during the run.
    /// </summary>
    /// <param name="value">Bonus amount to add.</param>
    public void AddEnergyRechargeBonus(float value)
    {
        EnergyRechargeBonus += value;
    }
}
