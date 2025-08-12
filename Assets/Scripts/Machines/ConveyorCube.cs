using System;
using UnityEngine;

public enum CubeUpgradeType
{
    MaxHealth,
    MaxEnergy,
    EnergyRecharge,
    AttackDamage
}

public class ConveyorCube : MonoBehaviour
{
    [SerializeField] private Sprite maxHealthSprite;
    [SerializeField] private Sprite maxEnergySprite;
    [SerializeField] private Sprite energyRechargeSprite;
    [SerializeField] private Sprite attackDamageSprite;
    [SerializeField] private SpriteRenderer spriteRenderer;

    public bool isUpgradeActive = false;
    public CubeUpgradeType upgradeType;

    private enum CubeState { Normal, Active }
    private CubeState state = CubeState.Normal;

    /// <summary>
    /// Gets the upgrade type selected when activated.
    /// </summary>
    public CubeUpgradeType SelectedUpgrade { get; private set; }

    /// <summary>
    /// Activates the cube's configured upgrade and updates the sprite.
    /// </summary>
    public void Activate()
    {
        if (state == CubeState.Active)
            return;

        isUpgradeActive = true;
        SelectedUpgrade = upgradeType;

        switch (SelectedUpgrade)
        {
            case CubeUpgradeType.MaxHealth:
                spriteRenderer.sprite = maxHealthSprite;
                break;
            case CubeUpgradeType.MaxEnergy:
                spriteRenderer.sprite = maxEnergySprite;
                break;
            case CubeUpgradeType.EnergyRecharge:
                spriteRenderer.sprite = energyRechargeSprite;
                break;
            case CubeUpgradeType.AttackDamage:
                spriteRenderer.sprite = attackDamageSprite;
                break;
        }

        state = CubeState.Active;
    }
}

