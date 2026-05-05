using UnityEngine;

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
    private float appliedMaxHealthBonus;
    private float appliedMaxEnergyBonus;
    private float appliedEnergyRechargeBonus;
    private int appliedAttackDamageBonus;
    private float pendingMaxHealthBonusForNextApply;
    private float pendingMaxEnergyBonusForNextApply;
    public bool HasValues => hasValues;

    /// <summary>
    /// Captures temporary stats from the source robot and related systems.
    /// </summary>
    /// <param name="source">Robot providing current values.</param>
    /// <param name="energyBot">Energy system supplying recharge rate.</param>
    /// <param name="attack">Attack providing damage values.</param>
    public void Capture(RobotStats source, EnergyBot energyBot, Attack attack)
    {
        Capture(source, energyBot, attack, null);
    }

    /// <summary>
    /// Captures temporary stats from the source robot and related systems.
    /// </summary>
    /// <param name="source">Robot providing current values.</param>
    /// <param name="energyBot">Energy system supplying recharge rate.</param>
    /// <param name="attack">Attack providing damage values.</param>
    /// <param name="attackHitboxes">Attack hitboxes providing damage values when no Attack exists.</param>
    public void Capture(RobotStats source, EnergyBot energyBot, Attack attack, AttackHitbox[] attackHitboxes)
    {
        if (source == null)
        {
            Debug.LogWarning("[PlayerRunStats] Capture skipped because source stats are missing.");
            return;
        }

        pendingMaxHealthBonusForNextApply = Mathf.Max(0f, MaxHealthBonus - appliedMaxHealthBonus);
        pendingMaxEnergyBonusForNextApply = Mathf.Max(0f, MaxEnergyBonus - appliedMaxEnergyBonus);

        MaxHealth = Mathf.Max(0f, source.MaxHealth - appliedMaxHealthBonus);
        MaxEnergy = Mathf.Max(0f, source.MaxEnergy - appliedMaxEnergyBonus);
        CurrentHealth = Mathf.Clamp(source.CurrentHealth, 0f, source.MaxHealth);
        CurrentEnergy = Mathf.Clamp(source.CurrentEnergy, 0f, source.MaxEnergy);
        Morality = source.Morality;
        if (energyBot != null)
        {
            EnergyRechargeRate = Mathf.Max(0f, energyBot.RechargeRate - appliedEnergyRechargeBonus);
        }
        else
        {
            EnergyRechargeRate = Mathf.Max(0f, source.EnergyRechargeRate - appliedEnergyRechargeBonus);
        }
        if (attack != null)
        {
            AttackDamage = Mathf.Max(0, attack.Damage - appliedAttackDamageBonus);
        }
        else if (TryGetFirstAttackHitboxDamage(attackHitboxes, out int hitboxDamage))
        {
            AttackDamage = Mathf.Max(0, hitboxDamage - appliedAttackDamageBonus);
        }

        hasValues = true;

        Debug.Log("[PlayerRunStats] Capture complete. "
            + $"baseMaxHealth={MaxHealth}, currentHealth={CurrentHealth}, "
            + $"baseMaxEnergy={MaxEnergy}, currentEnergy={CurrentEnergy}, "
            + $"baseRecharge={EnergyRechargeRate}, baseAttackDamage={AttackDamage}, "
            + $"totalBonuses=({DescribeBonuses()}), "
            + $"alreadyApplied=(health={appliedMaxHealthBonus}, energy={appliedMaxEnergyBonus}, "
            + $"recharge={appliedEnergyRechargeBonus}, attack={appliedAttackDamageBonus}), "
            + $"pendingNextApply=(health={pendingMaxHealthBonusForNextApply}, energy={pendingMaxEnergyBonusForNextApply})");
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
        Apply(target, energyBot, attack, null);
    }

    /// <summary>
    /// Applies captured stats to the target robot and related systems.
    /// </summary>
    /// <param name="target">Robot receiving stored values.</param>
    /// <param name="energyBot">Energy system to update recharge rate.</param>
    /// <param name="attack">Attack to update with stored damage.</param>
    /// <param name="attackHitboxes">Attack hitboxes to update when no Attack exists.</param>
    public void Apply(RobotStats target, EnergyBot energyBot, Attack attack, AttackHitbox[] attackHitboxes)
    {
        if (target == null || !hasValues)
        {
            if (target == null)
            {
                Debug.LogWarning("[PlayerRunStats] Apply skipped because target stats are missing.");
            }
            return;
        }

        target.MaxHealth = MaxHealth + MaxHealthBonus;
        target.MaxEnergy = MaxEnergy + MaxEnergyBonus;
        float healthTarget = Mathf.Clamp(CurrentHealth + pendingMaxHealthBonusForNextApply, 0f, target.MaxHealth);
        float energyTarget = Mathf.Clamp(CurrentEnergy + pendingMaxEnergyBonusForNextApply, 0f, target.MaxEnergy);
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

        int appliedAttackDamage = AttackDamage + AttackDamageBonus;
        bool appliedAttackDamageToHitbox = ApplyAttackHitboxDamage(attackHitboxes, appliedAttackDamage);
        if (attack == null && AttackDamageBonus != 0 && !appliedAttackDamageToHitbox)
        {
            Debug.LogWarning("[PlayerRunStats] Attack damage bonus could not be applied because no Attack or AttackHitbox is available.");
        }

        appliedMaxHealthBonus = MaxHealthBonus;
        appliedMaxEnergyBonus = MaxEnergyBonus;
        appliedEnergyRechargeBonus = EnergyRechargeBonus;
        appliedAttackDamageBonus = AttackDamageBonus;

        Debug.Log("[PlayerRunStats] Apply complete. "
            + $"maxHealth={target.MaxHealth}, currentHealth={target.CurrentHealth}, "
            + $"maxEnergy={target.MaxEnergy}, currentEnergy={target.CurrentEnergy}, "
            + $"recharge={target.EnergyRechargeRate}, "
            + $"attackDamage={(attack != null || appliedAttackDamageToHitbox ? appliedAttackDamage.ToString() : "none")}, "
            + $"totalBonuses=({DescribeBonuses()})");
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
        appliedMaxHealthBonus = 0f;
        appliedMaxEnergyBonus = 0f;
        appliedEnergyRechargeBonus = 0f;
        appliedAttackDamageBonus = 0;
        pendingMaxHealthBonusForNextApply = 0f;
        pendingMaxEnergyBonusForNextApply = 0f;

        hasValues = false;

        Debug.Log("[PlayerRunStats] Run stats reset.");
    }

    /// <summary>
    /// Adds to the energy recharge bonus accumulated during the run.
    /// </summary>
    /// <param name="value">Bonus amount to add.</param>
    public void AddEnergyRechargeBonus(float value)
    {
        EnergyRechargeBonus += value;
    }

    /// <summary>
    /// Adds a cube upgrade bonus to the current run.
    /// </summary>
    /// <param name="upgrade">Upgrade type collected.</param>
    /// <param name="store">Upgrade value source.</param>
    public void AddCubeBonus(CubeUpgradeType upgrade, CubeUpgradeSO store)
    {
        if (store == null)
        {
            Debug.LogWarning("[PlayerRunStats] Cube bonus skipped because upgrade values are missing.");
            return;
        }

        switch (upgrade)
        {
            case CubeUpgradeType.MaxHealth:
                MaxHealthBonus += store.UpgradeMaxHealthValue;
                break;
            case CubeUpgradeType.MaxEnergy:
                MaxEnergyBonus += store.UpgradeMaxEnergyValue;
                break;
            case CubeUpgradeType.EnergyRecharge:
                AddEnergyRechargeBonus(store.UpgradeEnergyRechargeValue);
                break;
            case CubeUpgradeType.AttackDamage:
                AttackDamageBonus += store.UpgradeAttackDamageValue;
                break;
        }
    }

    /// <summary>
    /// Describes current run bonuses for diagnostics.
    /// </summary>
    public string DescribeBonuses()
    {
        return $"health={MaxHealthBonus}, energy={MaxEnergyBonus}, recharge={EnergyRechargeBonus}, attack={AttackDamageBonus}";
    }

    private static bool TryGetFirstAttackHitboxDamage(AttackHitbox[] attackHitboxes, out int damage)
    {
        damage = 0;
        if (attackHitboxes == null)
            return false;

        foreach (AttackHitbox hitbox in attackHitboxes)
        {
            if (hitbox == null)
                continue;

            damage = hitbox.damage;
            return true;
        }

        return false;
    }

    private static bool ApplyAttackHitboxDamage(AttackHitbox[] attackHitboxes, int damage)
    {
        if (attackHitboxes == null)
            return false;

        bool applied = false;
        foreach (AttackHitbox hitbox in attackHitboxes)
        {
            if (hitbox == null)
                continue;

            hitbox.damage = damage;
            applied = true;
        }

        return applied;
    }
}
