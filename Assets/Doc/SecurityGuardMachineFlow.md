# Security Guard – Machine Reactivation Flow

This document outlines how security guards react when a `FactoryMachine` is switched off.

1. **Registration**
   - `MachineSecurityManager.RegisterMachine` subscribes to each machine's `OnMachineStateChanged` event.
   - Security guards call `RegisterGuard` during initialization so the manager can dispatch them.
2. **Notification**
   - When a machine turns off, `MachineSecurityManager` chooses the nearest available guard and calls `ReactivateMachine` on it.
3. **Reactivation**
   - `SecurityGuardAI.ReactivateMachine` switches the guard to `Enemy_ReactivateMachine`.
   - After turning the machine back on, the guard enters `Enemy_ReturnToSecurityPost` to go back to its previous post.

This shared state-machine approach keeps behaviour consistent across all robot types.

