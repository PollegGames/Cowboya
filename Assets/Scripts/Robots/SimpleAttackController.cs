using System;
using UnityEngine;

/// <summary>
/// Simplified attack driver: exposes per-arm attack toggles.
/// </summary>
[DisallowMultipleComponent]
public class CowboySimpleAttackController : MonoBehaviour
{
    [Header("Hand Hitboxes")]
    [SerializeField] private AttackHitbox leftHandHitbox;
    [SerializeField] private AttackHitbox rightHandHitbox;
    private bool referencesLogged;

    public event Action<CowboyArmSide> AttackHit;

    private void OnEnable()
    {
        DeactivateAll();
        LogMissingReferences();
        SubscribeToHitboxes();
    }

    private void OnDisable()
    {
        DeactivateAll();
        UnsubscribeFromHitboxes();
    }

    public void SetArmAttackActive(CowboyArmSide arm, bool active)
    {
        if (!active)
        {
            DeactivateArm(arm);
            return;
        }

        ActivateArm(arm);
        DeactivateArm(arm == CowboyArmSide.Left ? CowboyArmSide.Right : CowboyArmSide.Left);
    }

    public void DeactivateAll()
    {
        DeactivateArm(CowboyArmSide.Left);
        DeactivateArm(CowboyArmSide.Right);
    }

    private void LogMissingReferences()
    {
        if (referencesLogged)
        {
            return;
        }

        if (rightHandHitbox == null)
        {
            Debug.LogWarning("[CowboySimpleAttackController] Right hand hitbox is not assigned.", this);
        }
        referencesLogged = true;
    }

    private void SubscribeToHitboxes()
    {
        UnsubscribeFromHitboxes();
        if (leftHandHitbox != null)
        {
            leftHandHitbox.Hit += OnHitboxHit;
        }

        if (rightHandHitbox != null)
        {
            rightHandHitbox.Hit += OnHitboxHit;
        }
    }

    private void UnsubscribeFromHitboxes()
    {
        if (leftHandHitbox != null)
        {
            leftHandHitbox.Hit -= OnHitboxHit;
        }

        if (rightHandHitbox != null)
        {
            rightHandHitbox.Hit -= OnHitboxHit;
        }
    }

    private void ActivateArm(CowboyArmSide arm)
    {
        AttackHitbox hitbox = GetHitbox(arm);
        hitbox?.Activate();
    }

    private void DeactivateArm(CowboyArmSide arm)
    {
        AttackHitbox hitbox = GetHitbox(arm);
        hitbox?.Deactivate();
    }

    private AttackHitbox GetHitbox(CowboyArmSide arm)
    {
        return arm == CowboyArmSide.Right ? rightHandHitbox : leftHandHitbox;
    }

    private void OnHitboxHit(AttackHitbox hitbox)
    {
        CowboyArmSide? arm = GetArmForHitbox(hitbox);
        if (!arm.HasValue)
        {
            return;
        }

        AttackHit?.Invoke(arm.Value);
    }

    private CowboyArmSide? GetArmForHitbox(AttackHitbox hitbox)
    {
        if (hitbox == null)
        {
            return null;
        }

        if (hitbox == leftHandHitbox)
        {
            return CowboyArmSide.Left;
        }

        if (hitbox == rightHandHitbox)
        {
            return CowboyArmSide.Right;
        }

        return null;
    }
}
