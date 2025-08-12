using UnityEngine;

[CreateAssetMenu(fileName = "PlayerRunStats", menuName = "Player/RunStats")]
public class PlayerRunStats : ScriptableObject
{
    public float currentHealth;
    public float maxHealth;
    public float maxEnergy;
    public float energyRechargeRate;
    public int attackDamage;
    public float morality;
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
        currentHealth = Mathf.Clamp(source.CurrentHealth, 0f, source.MaxHealth);
        maxHealth = source.MaxHealth;
        maxEnergy = source.MaxEnergy;
        morality = source.Morality;
        energyRechargeRate = energyBot != null ? energyBot.rechargeRate : source.EnergyRechargeRate;
        attackDamage = attack != null ? attack.Damage : (source.Attacks.Count > 0 ? source.Attacks[0].Damage : 0);

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

        target.MaxHealth = maxHealth;
        target.MaxEnergy = maxEnergy;
        float healthTarget = Mathf.Clamp(currentHealth, 0f, target.MaxHealth);
        target.UpdateHealth(healthTarget - target.CurrentHealth);
        target.UpdateMorality(morality - target.Morality);
        target.EnergyRechargeRate = energyRechargeRate;
        if (energyBot != null)
        {
            energyBot.rechargeRate = energyRechargeRate;
        }
        if (attack != null)
        {
            attack.Damage = attackDamage;
        }
    }

    /// <summary>
    /// Clears the stored run statistics.
    /// </summary>
    public void Reset()
    {
        currentHealth = 0f;
        maxHealth = 0f;
        maxEnergy = 0f;
        energyRechargeRate = 0f;
        attackDamage = 0;
        morality = 0f;
        MaxHealthBonus = 0f;
        MaxEnergyBonus = 0f;
        EnergyRechargeBonus = 0f;
        AttackDamageBonus = 0;

        hasValues = false;
    }
}
