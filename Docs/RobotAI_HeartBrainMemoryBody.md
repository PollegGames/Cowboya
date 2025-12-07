# Robot AI: Brain / Heart / Body / Memory

Shared architecture for every robot except the player.

## Components
- **Brain**: central coordinator. Receives world events (machines, alarms, managers) and decides which task to run next. Only entry point for external systems.
- **Heart**: decisional/task system. Maintains a stack/queue of `RobotTask`s (e.g., `WorkAtMachine`, `GoToRest`, `ReactivateMachine`, `GuardPost`). Chooses what to do each step and forwards movement/animation requests to the Body.
- **Body**: physical controller (movement, IK/ragdoll/animations). Executes low-level actions requested by the Heart (`go to point X`, `play punch`, `interact with machine`).
- **Memory**: factual data only (last visited point/machine/time attacked, etc.). Used by Brain/Heart to make decisions but does not decide on its own.

## Core Rules
- **Single entry point**: machines/managers/alarms talk to `RobotBrain`, never directly to Heart or Body.
- **Machines only describe state + targets**: they say “I am ON/OFF” and provide points (`WorkPoint`, `RestPoint`, `SecurityPoint`). They never move robots.
- **Managers are routers**: `MachineWorkerManager` and `MachineSecurityManager` listen to machine events, pick the right robot, and forward to that robot's Brain with the relevant points.
- **Heart owns the task stack**: Brain chooses *which* task; Heart executes and talks to Body.

## Worker loop
- Default task: `WorkAtMachine(machine, machine.WorkPoint)` while machine is ON.
- When machine turns OFF → Brain pushes `GoToRest(machine.RestPoint)`.
- When machine turns ON again → Brain pushes `WorkAtMachine` and the loop resumes.

## Guard loop
- Default task: `GuardPost(securityPoint)`.
- When a machine turns OFF → Brain raises `ReactivateMachine(machine, machine.SecurityPoint)`.
- After reactivation → return to `GuardPost`.

## Player
The player uses a separate, simpler system and is not part of this architecture.
