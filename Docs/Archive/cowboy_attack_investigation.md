# Cowboy player attack flow investigation

This note tracks where the cowboy player's arm movement and basic attack state are driven, to prepare a change where the attack motion should also end when a hit lands (instead of only when the player releases left click).

## Arm input and solver steering
- `CowboyArmTargetController` watches mouse buttons each `Update()`. Holding left click sets `attackInputHeld`/`attackHeld`, while releasing left click clears them and immediately deactivates any active attack arm via `CowboySimpleAttackController.SetArmAttackActive(...)`. The same class moves the IK solver targets in `LateUpdate()` whenever `interactHeld` (right click) or `attackHeld` (left click) is true, and returns the arms to their rest pose when both are false. Relevant logic:
  - Input capture and attack state toggling: `Update()` and `HandleAttackInput()` lines 76-245.
  - IK target driving based on `attackHeld`/`interactHeld`: `LateUpdate()` lines 249-301, plus helper methods for arm selection and rest return.

## Hitbox activation and auto-shutdown
- `CowboySimpleAttackController` only enables/disables the per-arm `AttackHitbox` instances based on the chosen active arm. It deactivates both hitboxes when disabled and when switching arms; this is currently triggered only by the left-click state changes above.
- `AttackHitbox` itself turns off once it registers a collision in `OnTriggerEnter2D`, after applying damage and pushback. This means a landed hit already deactivates the hitbox, but the arm target keeps moving until the player releases left click because `CowboyArmTargetController` does not react to hitbox events.

## Likely integration points to end motion on hit
- Introduce a notification from `AttackHitbox` when it deactivates after a hit, so the controller driving arm motion can react. Options include a UnityEvent/Action on `AttackHitbox` or having `CowboySimpleAttackController` poll `AttackHitbox.IsActive` each frame.
- In `CowboyArmTargetController`, consume that notification (or poll state) to clear `attackHeld`/`attackActiveArm` and call `SetArmAttackActive(..., false)` so the arm stops following the target and returns to rest even if the mouse button remains pressed.
- Any change should consider the existing arm-selection logic in `HandleAttackInput()` and the rest return path in `LateUpdate()` to ensure the arms reset correctly when a hit ends the attack.
