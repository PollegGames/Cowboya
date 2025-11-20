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

    private void OnEnable()
    {
        DeactivateAll();
        LogMissingReferences();
    }

    private void OnDisable()
    {
        DeactivateAll();
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
}
