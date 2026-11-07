# Security Guard Reactivation Summary

This document summarizes the final outcome and current behavior.

## Goal
- When a machine turns off, a SecurityGuard should reach it and call `PowerOn()` directly (no slot reservation).

## Final Fix
- Use a `ReactiveZone` child with `PositionTriggerZone` + `MachineReactivationTrigger` on each machine.
- Trigger-based activation powers the machine on when a SecurityGuard enters the zone.
- Task-matching is disabled to ensure activation fires reliably.

## Current Guard Post Behavior
- When no SecurityMachine is available, guards now try Rest points first.
- If no Rest points exist, they fall back to Work/Rest, then Start as last resort.

## Notes
- `PositionTriggerZone` is core to the project and remains unchanged.
