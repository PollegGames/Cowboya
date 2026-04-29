# Worker Slot Simplification Investigation (2026-04-16)

## Question
Can we remove `WorkerAttachmentDomainEventBus` and `WorkerAttachmentCoordinator` and make worker attach/release logic simpler?

Short answer: yes, this is a valid idea, not a wrong one.

## Why This Refactor Is Reasonable
The current flow is technically event-driven but operationally heavy:
- Slot triggers publish events.
- Coordinator queues and arbitrates.
- Memory/brain updates happen while attach/release churn is still in flight.
- Many transient states appear (`GoToMachine`, `Rest`, `replaced`, `slot_exit`) in a short time window.

This architecture is powerful for large multi-machine policies, but for a single-slot machine replacement rule it adds too much moving state.

Your desired rule is simple and deterministic:
1. Slot has one owner worker.
2. If a new valid worker enters, replace old owner.
3. Rest machine: keep worker for X seconds, then release.
4. Work machine: keep worker until replaced or leaves intentionally.

That can be implemented without a bus/coordinator pair.

## Important Clarification: `PositionTriggerZone` vs `OnTriggerEnter2D`
They are separate systems in the current codebase.

- `PositionTriggerZone` (`Assets/Scripts/Misc/Interaction/PositionTriggerZone.cs`) is used in room/perception style flows (`FollowEnemyTriggerHandler`, cameras, reactivation trigger).
- `WorkerSlot` / `RestingSlot` use Unity collider callbacks (`OnTriggerEnter2D`/`OnTriggerExit2D`) for machine occupancy.

So yes, your machine slot ownership currently does not run through `PositionTriggerZone`.

## Proposed Clean Architecture (No Coordinator)

### Ownership Rules
- `BaseMachine` (or each concrete machine) remains the single source of occupancy truth.
- Slot is a thin adapter that validates candidate and calls machine methods directly.
- No queue, no global event bus for slot arbitration.

### Core API (minimal)
Machine exposes explicit methods:
- `bool TryAttachWorker(RobotBrainNew worker, string reason)`
- `bool TryReplaceWorker(RobotBrainNew incoming, string reason)`
- `bool TryReleaseWorker(RobotBrainNew worker, string reason)`

### Slot Role
`WorkerSlot` does only:
- detect candidate enter/exit
- validate role + task target
- call machine API
- log local debug event

No global arbitration logic.

## Deterministic Replacement Flow (Pseudo-code)

```csharp
// WorkerSlot.OnCandidateEnter(worker)
if (!IsWorkerRole(worker)) return;
if (!IsTargetingThisMachine(worker)) return;

if (machine.CurrentWorker == null)
{
    machine.TryAttachWorker(worker, "enter_attach");
    return;
}

if (machine.CurrentWorker == worker)
{
    return; // already owner
}

machine.TryReplaceWorker(worker, "enter_replace");
```

```csharp
// Machine.TryReplaceWorker(incoming)
if (incoming == null || !IsOn) return false;

var previous = CurrentWorker;
if (previous != null)
{
    // release previous first
    ReleaseWorkerInternal(previous, reason: "replaced");
    previous.Memory.SetDesiredMachineType(OtherType(this.Type));
    previous.Memory.NotifyMachineSlotReleasedTransient();
}

AttachWorkerInternal(incoming);
incoming.Memory.NotifyMachineSlotAttached(ResolvedWaypointOrType());
return true;
```

## Rest Timing (Desired Behavior)

```csharp
// RestingMachine
OnAttach(worker): StartTimer(worker, restSeconds)
OnTimerDone(worker):
    if (CurrentWorker != worker) return;
    ReleaseWorkerInternal(worker, "rest_done");
    worker.Memory.SetDesiredMachineType(MachineType.WorkStation);
    worker.Memory.NotifyMachineSlotReleasedTransient();
```

This matches your rule exactly:
`attach rest -> stay connected -> wait X seconds -> release -> go work`.

## Critical Simplicity Guardrails
1. Only release on slot exit if exiting worker is current owner.
2. Never release current owner because a non-owner collider exited.
3. Attach must always set a concrete machine state in memory (`Work` or `Rest`) so planner cannot fall back to stale intent.
4. Keep replacement synchronous: release old then attach new in one call path.

## Migration Plan (Safe, Incremental)

### Phase A: Introduce direct machine API
- Add `TryAttachWorker/TryReplaceWorker/TryReleaseWorker` to `FactoryMachine` and `RestingMachine`.
- Keep coordinator intact temporarily, but unused by new slot path.

### Phase B: Make slots direct-call only
- `WorkerSlot` and `RestingSlot` call machine API directly.
- Remove `WorkerAttachmentDomainEventBus.PublishSlotSignal(...)` calls.

### Phase C: Remove coordinator/bus
- Delete `WorkerAttachmentCoordinator`.
- Delete `WorkerAttachmentDomainEventBus` and related event structs.

### Phase D: Validate with targeted tests
- one worker enters empty work slot -> attached
- second worker enters same slot -> replaces first
- non-owner exit does not release owner
- rest timer keeps owner until timeout, then releases exactly once

## Risks and Tradeoffs

### Pros
- Smaller call graph.
- Easier debugging.
- Clear ownership at machine level.
- Fewer transient planner flips.

### Cons
- Less global orchestration if future policies need cross-machine arbitration.
- Slot scripts must stay strict and small, or complexity can migrate back there.

## Recommendation
Proceed with this simplification.

This is not a wrong idea for your current goal. Your target behavior is deterministic and local; a direct machine-owned flow is the cleanest fit.

## Optional Variant
If you still want event logging for diagnostics, keep only a lightweight local trace event (`OnWorkerAttached/Released/Replaced`) from machine classes, without a global slot decision bus.
