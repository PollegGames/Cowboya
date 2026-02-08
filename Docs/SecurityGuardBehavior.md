# Security Guard Behavior

## Overview
This document captures the intended security guard behavior logic and related systems.

## Spawn
- Security guards spawn elsewhere (via `EnemiesSpawner`).
  - Spawning logic is separate from guard behavior.

## Guarding Machines
- Guards are expected to remain assigned to a **security machine** until reassigned by the security manager.
- When any machine turns off (security, rest, work, spawner):
  - Only guards currently stationed at a security machine are eligible to react.
  - The closest eligible guard is selected and sent to reactivate the machine.
  - The dispatched guard goes only to the assigned machine.
- After reactivating a machine, the guard should look for the closest available security machine.

## Resting Guards
- Guards that are resting only search for a **free security machine** to guard.
- Resting guards do not take other tasks; they only move to guard a security machine when one is free.
- Resting guards should never be assigned to rest machines (rest machines are reserved for workers).
- Idle guards should be treated like resting guards for reassignment eligibility.

## Reservation / Selection
- There is an existing reservation system for machines.
- The security manager uses this to select the closest available station whenever possible.

## Fallback Movement
If a guard has no available security machine:
- It remains resting (or goes to the start room if no rest machine is on).

## Combat Override
- Reactivation can be temporarily paused by combat behaviors.
- Guard should resume reactivation once the player is no longer in the detection zone.

## Spawner Interaction
- Security machine OFF events should not spawn new guards.

## Reactivation Handler Requirement
- `ReactivateMachine` must be handled by a task handler (e.g., `MoveToPayloadHandler`) that calls the reactivation routine.
- Without a handler, guards may move to the target but will not power the machine back on.

## Key Components
- `Assets/Scripts/AI/EnemiesSpawner.cs`
- `Assets/Scripts/Factory/Machines/MachineSecurityManager.cs`
- `Assets/Scripts/Factory/Machines/SecurityMachine.cs`
- `Assets/Scripts/Factory/Reservations/StationReservationService.cs`
- `Assets/Scripts/Robots/RobotBrain.cs`
