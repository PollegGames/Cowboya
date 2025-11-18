using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simplified attack driver: holds left mouse to enable hand hitboxes.
/// </summary>
[DisallowMultipleComponent]
public class CowboySimpleAttackController : MonoBehaviour
{
    [Header("Hand Hitboxes")]
    [SerializeField] private AttackHitbox leftHandHitbox;
    [SerializeField] private AttackHitbox rightHandHitbox;

    [Tooltip("If true, both hands attack together; otherwise only the right hand activates.")]
    [SerializeField] private bool useBothHands;

    private bool hitboxesActive;
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

    private void Update()
    {
        bool attackHeld = IsLeftMouseHeld();
        if (attackHeld != hitboxesActive)
        {
            SetHitboxesActive(attackHeld);
        }
    }

    private bool IsLeftMouseHeld()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }

        return Input.GetMouseButton(0);
    }

    private void SetHitboxesActive(bool active)
    {
        hitboxesActive = active;
        if (!active)
        {
            DeactivateAll();
            return;
        }

        if (useBothHands)
        {
            leftHandHitbox?.Activate();
        }
        else
        {
            leftHandHitbox?.Deactivate();
        }

        rightHandHitbox?.Activate();
    }

    private void DeactivateAll()
    {
        hitboxesActive = false;
        leftHandHitbox?.Deactivate();
        rightHandHitbox?.Deactivate();
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

        if (useBothHands && leftHandHitbox == null)
        {
            Debug.LogWarning("[CowboySimpleAttackController] Left hand hitbox is not assigned but both hands are enabled.", this);
        }

        referencesLogged = true;
    }
}

