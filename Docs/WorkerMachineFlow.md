# Worker ↔ Machine Flow

How workers react to machine state changes in the Brain/Heart/Body/Memory architecture.

## Responsibilities
- **FactoryMachine**: owns ON/OFF state and exposes `WorkPoint` / `RestPoint` / `SecurityPoint`; raises `OnMachineTurnedOn/Off(machine, point)`.
- **MachineWorkerManager**: listens to machine events, finds the assigned worker, and forwards to that worker's `RobotBrain`. It never manipulates Heart/Body directly.
- **RobotBrain**: selects which task to run in the Heart when machine events arrive.
- **Heart**: runs the task stack/queue (e.g., `WorkAtMachine`, `GoToRest`) and asks the Body to execute movement/animations.
- **Body**: moves to requested points and reports completion back to the Heart.

External systems (machines/managers) never talk to Heart/Body; they only call the Brain.

## Normal Loop (machine ON)
1. Worker is assigned to a `FactoryMachine`.
2. `RobotBrain` sets the default Heart task to `WorkAtMachine(machine, machine.WorkPoint)`.
3. Heart tells Body to move to `WorkPoint` and hold position while the machine is ON (work animation).

## Machine turns OFF
1. `FactoryMachine` flips state to Off and raises `OnMachineTurnedOff(machine, machine.RestPoint)`.
2. `MachineWorkerManager` receives the event and calls `workerBrain.OnMachineTurnedOff(machine, restPoint)`.
3. Brain clears/lowers `WorkAtMachine` and pushes `GoToRest(machine.RestPoint)` into the Heart.
4. Heart tells Body to move to `RestPoint` and stay there (rest/sleep animation).

## Machine back ON → return from rest
1. When Body reaches `RestPoint`, it notifies Heart; Heart marks `GoToRest` complete.
2. Brain waits for `OnMachineTurnedOn(machine, machine.WorkPoint)` (event or poll).
3. On ON event, `MachineWorkerManager` forwards to `workerBrain.OnMachineTurnedOn(machine, workPoint)`.
4. Brain pushes `WorkAtMachine(machine, workPoint)`; Heart sends Body from rest back to work and resumes the loop.

Resulting cycle: `WorkAtMachine` (ON) → machine OFF → `GoToRest` → machine ON → `WorkAtMachine` again.
