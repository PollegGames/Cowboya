# Security Guard ↔ Machine Reactivation Flow

How guards respond when a `FactoryMachine` turns OFF.

## Responsibilities
- **FactoryMachine**: owns ON/OFF state; exposes `SecurityPoint`; raises `OnMachineTurnedOff(machine, securityPoint)` and optionally `OnMachineTurnedOn`.
- **MachineSecurityManager**: listens to machine events, picks a guard (nearest/available) and calls that guard's `RobotBrain`. It does not move robots itself.
- **RobotBrain**: raises task priority for `ReactivateMachine` and later restores the default guard task.
- **Heart**: runs the current task (`GuardPost`, `ReactivateMachine`) and instructs the Body.
- **Body**: moves to requested points and reports arrival so the Heart can trigger interactions.

External systems only call the Brain—never Heart/Body.

## Default state: GuardPost
- Each guard has a default post (a `SecurityMachine` or point).
- Brain sets Heart's default task to `GuardPost(securityPoint)` to patrol/stay and wait for alarms.

## Machine turns OFF → dispatch
1. `FactoryMachine` raises `OnMachineTurnedOff(machine, machine.SecurityPoint)`.
2. `MachineSecurityManager` selects a guard and calls `guardBrain.RequestReactivateMachine(machine, securityPoint)`.
3. Brain elevates `ReactivateMachine(machine, securityPoint)` to the top of the Heart's stack/queue.

## ReactivateMachine execution
1. Heart tells Body: go to `securityPoint` for that machine.
2. Body moves there, then notifies Heart on arrival.
3. Heart triggers the interaction to switch the machine back ON.
4. `FactoryMachine` sets state to ON (may raise `OnMachineTurnedOn`).

## Return to GuardPost
1. Heart marks `ReactivateMachine` complete.
2. Brain restores the default `GuardPost(securityPointOfThisGuard)`.
3. Heart tells Body to go back and resume guarding.

Cycle: `GuardPost` → machine OFF → `ReactivateMachine` → machine ON → `GuardPost` again.
