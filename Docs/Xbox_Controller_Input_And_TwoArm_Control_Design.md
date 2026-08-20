# Xbox Controller Input And Two-Arm Control Design

## Purpose

Document the implemented controller-first input behavior. Keyboard and mouse support remains available as a fallback, but the primary target is an Xbox controller.

## Design Principles

- Walking, jumping, crouching, arm aiming, grabbing, and attacking are separate player intents.
- Each arm has its own mode and can operate at the same time as the other arm.
- The controller selects the arm action explicitly; there is no tap-versus-hold timing decision between attack and grab.
- Existing radial hand movement, hand rest poses, speed-based attacks, energy costs, and grab lifecycle remain authoritative.
- Facing is visual state. It must not be used as a substitute for the selected movement or aim direction.
- A grab-mode release releases the object held by that arm. Stopping arm input and releasing a held object are the same operation for this controller design.
- The two arms are fully independent: changing or releasing one arm's mode must never release, reset, or otherwise affect an object held by the other arm.

## Final Xbox Layout

| Control | Action |
| --- | --- |
| Left stick horizontal | Walk left/right |
| Left stick down | Hold to crouch; horizontal walking is suppressed |
| `A` | Directional jump |
| Right stick | Move the active hand around the existing radial system |
| `LB` | Left-arm grab mode |
| `RB` | Right-arm grab mode |
| `LT` | Left-arm attack mode |
| `RT` | Right-arm attack mode |

`LB`, `RB`, `LT`, and `RT` are independent. This allows both hands to grab, both hands to attack, or one hand to grab while the other attacks.

The right stick is reserved for arm aiming. Neither right-stick direction nor right-stick click controls crouching. `B` is currently unassigned by this design.

## Locomotion

### Walking

- Only the horizontal component of the left stick controls walking.
- Left stick left walks left.
- Left stick right walks right.
- The current facing/flip logic may continue to update visual orientation, but it must not change the meaning of the movement input.
- While crouching, horizontal movement is disabled.

### Jumping

- `A` requests a jump.
- A jump is valid only when the left stick has a meaningful horizontal direction.
- Left stick left produces a left jump.
- Left stick right produces a right jump.
- Neutral horizontal input blocks the jump; there is intentionally no straight jump for this control design.
- The existing jump animation and energy rules remain authoritative.

Jump is driven by the dedicated `Jump` action and the current horizontal direction; positive vertical movement does not trigger it.

### Crouching

- Pulling the left stick down is a held crouch action.
- Crouch engages when the processed vertical value reaches `-0.60` and remains engaged until it rises above `-0.45`.
- The separate press and release thresholds provide hysteresis, preventing controller noise around the boundary from rapidly toggling crouch.
- Only gamepad left-stick input uses this analog crouch rule. Keyboard `S` and the down arrow do not crouch; keyboard crouch remains `C`.
- Horizontal movement remains disabled for the entire crouch state.
- The existing crouch animation and energy rules remain authoritative.

## Arm Aiming

The right stick controls the hand position through the existing radial system around the player body.

- The right stick direction is converted to the radial hand target.
- The stick direction determines the angle only. Once the stick is outside its deadzone, stick magnitude does not determine arm radius; the existing `ArcTargetFollower` radius determines the distance.
- The direction is world/screen-relative and is never inverted or remapped by the character's facing direction.
- `ArcTargetFollower` is the only component responsible for converting the abstract aim vector into the radial world target. Do not duplicate radial calculations in the input reader or arm controller.
- When the right stick returns to neutral, retain the last valid radial target while the arm is still in an active mode.
- When the arm mode is released, use the existing default/rest-position behavior. Do not invent a new hand return animation.
- A deadzone is required so controller noise does not move the hand.
- Both active arms share the last valid right-stick target. Per-arm independent aim axes are outside this design.

### One Right Stick And Two Arms

The current controller implementation uses one right stick, so simultaneous active arms share the same aim direction.

Examples:

- `LB` + right stick: move the left hand.
- `RB` + right stick: move the right hand.
- `LB` + `RB` + right stick: move both hands toward the same radial direction.
- `LT` + `RT` + right stick: both hands can attack toward the same radial direction.

Independent simultaneous hand positions would require two aim axes or another hand-selection mechanism and are outside this first design.

## Per-Arm Modes

Each arm has one active intent at a time:

```text
Rest
  | LB/RB
  v
Grab

Rest
  | LT/RT
  v
Attack
```

The actual mode is selected independently for the left and right arm.

### Input Precedence

For a given arm, the most recently pressed mode input wins:

- Left arm: `LB` versus `LT`.
- Right arm: `RB` versus `RT`.
- Pressing the other mode replaces the current mode immediately.
- The implementation tracks input transitions, not only the current combined button state, so "last input received" is deterministic.
- Releasing the winning input should end that mode. If the other input is still held, the arm immediately resumes the other mode.
- A mode released with no remaining active mode returns the hand through the existing rest-position logic.

The required transition example is:

```text
LB held -> LT pressed -> Attack -> LT released while LB still held -> immediately resume Grab
```

This means there is no attack delay and no grab delay. The trigger or bumper explicitly declares the intended mode.

## Grab Mode

While `LB` or `RB` is the current mode for an arm:

- The selected arm follows the right-stick radial target.
- The arm continuously uses the existing grab detection while empty.
- A held object remains held according to the existing grab controller rules.
- Releasing the bumper releases the object held by that arm and ends grab-mode movement.
- If the other mode for that arm is still held, the arm resumes that mode immediately instead of returning to rest.
- Left and right arm grab state remains independent.
- Attacking with the opposite arm does not release or interrupt this arm's held object. The other arm may attack while this arm continues grabbing.
- Both arms may grab separate objects simultaneously.
- Both arms may grab the same object if the existing two-hand support rules permit it.

## Attack Mode

While `LT` or `RT` is the current mode for an arm:

- The selected arm follows the right-stick radial target.
- The trigger being held enables attack intent; it does not itself cause damage.
- The existing hand movement-speed threshold remains the real attack activation condition.
- When the hand reaches the configured speed threshold, the existing attack system activates and damage can occur.
- When the hand is below the threshold, the hand is still in attack mode but does not deal speed-based damage.
- Releasing the trigger immediately ends attack intent and deactivates the attack output, subject to the existing controller cleanup behavior.
- Existing attack grace-period, cooldown, and energy rules should be preserved unless they conflict with this explicit mode ownership.
- Attack and grab modes must not both be active for the same arm at the same time.
- The existing attack-hit protection must remain in place: after an attack hit, the same attack cannot apply damage again on the immediately following frame or during the existing short protection interval.
- A trigger hold supports repeated attacks. After a fast movement produces a hit, the hitbox deactivates for that attack window while the arm remains under attack control.
- While the trigger remains held, another qualifying fast movement may start a new attack window and reactivate the hitbox after the existing hit protection/cooldown permits it.
- The player does not need to release and repress the trigger between separate swings.

The intended behavior is therefore:

```text
Trigger held -> arm may move -> hand speed reaches threshold -> attack hitbox activates
Attack hit -> hitbox deactivates for that attack window -> arm remains in attack control
Another qualifying fast movement -> a new attack window -> hitbox reactivates
Trigger released -> attack control stops -> arm returns if no other mode is held
```

The repeated-attack sequence is therefore:

```text
RT held -> swing fast -> HIT -> hitbox deactivates -> arm remains under attack control
  -> swing fast again -> HIT -> hitbox deactivates -> repeat while RT remains held
```

## Energy Integration

Input code never owns or duplicates gameplay energy values. Jump, grab, and attack are routed through the existing `PlayerBrain` / `RobotStateController` / `EnergyBot` pipeline using their `EnergyAction` values.

| Action | Consumption point | Authoritative cost source |
| --- | --- | --- |
| Jump | Once when an accepted directional jump begins | `EnergyBot.jumpEnergyCost` serialized on the player prefab |
| Grab | Once when an arm enters grab mode, then once per `ArmTargetController.holdEnergyInterval` while held | `EnergyBot.grabEnergyCost` serialized on the player prefab |
| Attack | Once when a qualifying speed-based attack window begins | `RobotStats.AttackEnergyCost`, initialized from `SaveData.AttackEnergyCost` and persisted by `PlayerSaveService` |

The current `Cowboy_Player` prefab defaults are `1.5` for jump, `1` for grab, and a `1`-second grab hold interval. The attack default is `5`, but saved player data is authoritative for attack cost. These values must be changed through their existing prefab/save configuration, never by adding constants to the input layer.

If the energy system rejects a cost, the requested action does not begin. Running out of energy while grab mode is held disables that arm's grab intent according to the existing state and faint behavior.

## Mouse And Keyboard Compatibility

Keyboard and mouse are not the primary design target, but they must remain functional through one fixed compatibility mapping. They must produce the same abstract intents as the gamepad path.

| Keyboard/mouse input | Action |
| --- | --- |
| `A` / `D` or left/right arrows | Walk left/right |
| `Space` | Directional jump using the current `A`/`D` direction; no direction means no jump |
| `C` held | Crouch; horizontal movement is disabled |
| Mouse position | World/screen-relative radial aim direction |
| Left mouse button held | Left-arm grab mode |
| Right mouse button held | Right-arm grab mode |
| `Q` held | Left-arm attack mode |
| `E` held | Right-arm attack mode |

For mouse aiming, the cursor supplies the abstract aim vector. The same `ArcTargetFollower` then applies the configured radius. Mouse buttons and keyboard attack keys use the same per-arm last-input-wins and release rules as the controller.

The important requirement is that both input sources feed the same abstract player and arm intents after the input-reading layer. Gameplay controllers should not need separate attack physics for mouse and gamepad.

## Implemented Code Responsibilities

| Area | Final responsibility |
| --- | --- |
| `InputSystem_Actions.inputactions` | Defines the gamepad and keyboard/mouse bindings; the generated C# wrapper mirrors this asset |
| `PlayerInputReader` | Exposes movement, aim, jump transitions, left-stick crouch state, and independent per-arm button states/sequences |
| `IPlayerInput` | Provides the device-independent player input contract and last-input-wins arm-mode resolver |
| `PlayerMovementController` | Applies directional jump and crouch locomotion rules, including blocking horizontal movement while crouched |
| `ArmTargetController` | Resolves independent arm modes, drives the shared radial target, and requests grab/attack energy through the existing energy system |
| `ArcTargetFollower` | Converts the raw aim vector into the existing fixed-radius world target |
| `SimpleAttackController` | Owns per-arm hitbox activation and hit reporting |
| `CowboyGrabController` | Owns independent left/right held objects and release behavior |
| `EnergyBot` / `RobotStateController` / `PlayerBrain` | Authoritatively validate and consume action energy |
| `SaveData` / `PlayerSaveService` / `PlayerTemplate` | Persist and restore the player's attack energy cost |

The generated `InputSystem_Actions.cs` file should not be edited manually. It must be regenerated from the `.inputactions` asset after binding changes.

## Input Ownership Pipeline

The runtime responsibilities are deliberately separated:

```text
Device input
   -> PlayerInputReader
     raw Movement, raw Vector2 Aim, button states, button transitions
   -> ArmTargetController
     LeftArmMode/RightArmMode, last-input-wins, release/resume rules
   -> ArcTargetFollower
     Aim -> radial world target using the existing fixed radius
   -> ArmTargetController
     active arm(s) follow the radial target
   -> SimpleAttackController / CowboyGrabController
     attack hitboxes, speed threshold, grab, release, and held-object consequences
```

`PlayerInputReader` must not decide which arm is active. `ArcTargetFollower` must not decide whether an arm is grabbing or attacking. `ArmTargetController` must not reimplement radial geometry.

## Implementation Status

The controller layout, two-arm mode resolution, radial aim path, locomotion rules, energy routing, keyboard/mouse compatibility, and crouch hysteresis described here are implemented. The generated `InputSystem_Actions.cs` wrapper is synchronized with the `.inputactions` asset. Edit Mode coverage includes per-arm mode precedence and left-stick crouch hysteresis.

## Acceptance Criteria

- Left stick walks left/right and cannot move the player horizontally while crouching.
- `A` jumps left or right only when the left stick supplies that direction.
- Pulling the left stick down past `-0.60` crouches; returning above `-0.45` releases crouch.
- The right stick remains dedicated to arm aiming and never changes crouch state.
- Right stick moves active hands around the existing radial system.
- `LB` and `RB` independently select grab mode for the left and right arms.
- `LT` and `RT` independently select attack mode for the left and right arms.
- The last mode input pressed wins for each arm.
- `LB held -> LT pressed -> LT released while LB remains held` immediately returns the left arm to grab mode; the equivalent rule applies to the right arm.
- Releasing a bumper releases that arm's held object unless another arm-specific mode remains active and the existing grab lifecycle explicitly retains it.
- Trigger-held attack still requires hand speed to reach the existing threshold before damage begins.
- Releasing a trigger stops that arm's attack mode.
- An attack hit cannot damage again on the immediately following frame or during the existing short hit-protection interval.
- One continuous trigger hold can produce multiple attack windows and hits; each new hit requires another qualifying fast movement.
- Both arms can be active simultaneously.
- Attacking with one arm never releases the object held by the other arm.
- Right-stick direction is world/screen-relative, never facing-relative; magnitude affects only deadzone validity, not radial distance.
- Neutral right-stick input holds the last valid radial target during an active mode.
- Releasing all modes restores the existing arm rest behavior.
- Jump, grab, and attack consume energy through the existing action-energy system and use its configured/persisted values.
- Existing grab, hitbox, and faint/release rules are not regressed.
- Keyboard and mouse remain usable through the fixed compatibility mapping specified above.
