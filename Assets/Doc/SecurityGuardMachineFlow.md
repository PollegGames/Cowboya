# Security Guard – Machine Reactivation Flow

This document outlines how security guards react when a `FactoryMachine` is switched off.

1. **Registration**
   - `MachineSecurityManager.RegisterMachine` subscribes to each machine's `OnMachineStateChanged` event.
   - When a machine turns off, `MachineSecurityManager` raises `OnMachineTurnedOff`.
2. **Notification**
   - `SecurityGuardAI` components subscribe to `OnMachineTurnedOff` during initialization.
   - Upon notification, the guard's state machine switches to `Enemy_ReactivateMachine` with the target machine.
3. **Reactivation**
   - `Enemy_ReactivateMachine` moves the guard to the closest waypoint near the machine.
   - Once in range, it calls `FactoryMachine.SetState(true)` and returns the guard to `Enemy_SecurityGuardRest`.

This shared state-machine approach keeps behaviour consistent across all robot types.

