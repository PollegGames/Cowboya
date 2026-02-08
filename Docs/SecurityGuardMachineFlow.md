# Security Guard <-> Machine Reactivation Flow

How guards respond when a machine turns OFF.

## Responsibilities
- **FactoryMachine / RestingMachine / SecurityMachine / SpawningMachine**: own ON/OFF state and raise state-change events. The security flow listens to machine turned off signals (either direct turning-off events or state-change events).
- **MachineSecurityManager**: listens to machine events, picks a guard (nearest eligible) and calls that guard's `RobotBrain`. It does not move robots itself.
- **RobotBrain**: pushes explicit tasks like `ReactivateMachine` and later restores guard behavior (via guard post request).
- **Heart**: surfaces the current task (`GuardPost`, `ReactivateMachine`, `WaitAtMachine`, etc.).
- **Body**: moves to requested points; task handlers may trigger interactions on arrival.

External systems only call the Brain -- never Heart/Body.

## Default state: GuardPost
- Each guard has a default post (a `SecurityMachine` or point).
- Brain sets Heart's default task to `GuardPost(securityPoint)` to patrol/stay and wait for alarms.

## Machine turns OFF -> dispatch
1. A machine raises an OFF event (`OnMachineStateChanged(..., false)` and/or a turning-off event).
2. `MachineSecurityManager` selects a guard and calls `guardBrain.PushExplicitTask(RobotTaskType.ReactivateMachine, machine)`.
3. Brain pushes `ReactivateMachine(machine)` (and typically a follow-up `WaitAtMachine`).

## ReactivateMachine execution
1. Heart surfaces `ReactivateMachine`; a task handler (e.g., `MoveToPayloadHandler`) must start the reactivation routine.
2. Body moves to the machine (or its waypoint); on arrival, the routine powers the machine back on.
3. `Machine` sets state to ON (may raise `OnMachineStateChanged(..., true)`).

### Arrival Validation
- Arrival only counts if the final waypoint is the machine’s waypoint.
- If the machine has no waypoint, arrival counts when the final waypoint is within a short distance of the machine.
- This prevents reactivation if the path was rerouted (e.g., player encounter) to a different destination.

## Return to GuardPost
1. Heart marks `ReactivateMachine` complete.
2. Brain requests a guard post (or resting fallback if none are available).
3. Heart tells Body to go back and resume guarding.

Cycle: `GuardPost` -> machine OFF -> `ReactivateMachine` -> machine ON -> `GuardPost` again.

## Eligibility Rules (Intended)
- Only guards currently stationed at a security machine are eligible to react to any machine turning off.
- Guards should not be assigned to rest machines (rest machines are reserved for workers).
- Reactivation can be paused by combat, but should resume once the player leaves the detection zone.
