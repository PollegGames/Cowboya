# Generated Map Room Selection Design - 2026-10-08

## Status

Implementation-ready design. This document defines the intended generated-room selection behavior but does not change runtime code or prefab configuration.

## Goal

Preserve the current compact composition of small generated maps while allowing larger maps, which have more POI slots, to include the wider set of authored room prefabs.

Small maps must remain focused on Resting, Security, and Reception. The Garage and Conveyor are a fixed functional pair at POI slots 6 and 7. Every other eligible generated-room type becomes available randomly only from POI slot 8 onward.

The number of POIs continues to come from `RunMapConfigSO.poiCount`. Room-selection logic must not silently raise a small map's configured POI count.

## Terminology

- **Grid cell:** One position in the generated rectangular map.
- **Work cell:** The default purpose assigned when the grid is created.
- **POI:** A cell selected as a point of interest and connected to Start by the path solver.
- **POI slot:** The one-based selection position within the configured POIs. For example, POI 1 is the first selected POI.
- **Room type:** The authored purpose/prefab rendered in a POI cell, such as Reception, Security, or Furnace.
- **Unused room type:** An eligible room type that is not yet present among the selected POIs in the current map.

## Existing Generation Order to Preserve

The high-level grid-generation order remains:

1. Create every grid cell as `UsageType.Work`.
2. Place Start and End in opposite corners.
3. Select up to the configured number of eligible Work cells as POIs.
4. Assign a room type to every selected POI according to the rules in this document.
5. Generate an independent path from Start to every POI and to End.
6. Convert remaining eligible Work cells into blocked cells up to `blockedCount`.
7. Run the existing room processors and render the selected prefabs.

Start, End, path, blocked, and remaining Work cells are not part of the POI room-type randomization.

## POI Room Selection Rules

Room selection uses the POI's one-based slot:

| POI slot | Selection rule |
|---|---|
| 1 | Always Resting |
| 2 | Always Security |
| 3-5 | Randomly select Resting, Security, or Reception |
| 6 | Always Garage |
| 7 | Always Conveyor |
| 8+ | Randomly select from the complete eligible generated-room pool except Laboratory |

These rules imply:

- A map with at least one POI always contains Resting.
- A map with at least two POIs always contains Security.
- POI 2 is the Security guarantee; a second special Security insertion is not required.
- Reception can appear randomly in slots 3-5 and is not guaranteed on maps with fewer than three POIs.
- A map with at least seven effective POIs always contains both Garage and Conveyor.
- Garage and Conveyor cannot appear in slots 1-5 and cannot be split across the 6/7 threshold.
- Slots 8 and later may contain any eligible generated room except Laboratory, including additional Garage or Conveyor rooms.

## Eligible Generated-Room Pool

For POI 6 and later, the complete pool is:

- Reception
- Security
- Resting
- Spawning
- Garage
- Conveyor
- Furnace
- Junks
- Deads
- Cube Collector

The corresponding authored prefabs currently are:

- `ROOM_Reception.prefab`
- `ROOM_security.prefab`
- `ROOM_resting.prefab`
- `ROOM_Spawning.prefab`
- `ROOM_Garage.prefab`
- `ROOM_Conveyor.prefab`
- `ROOM_Furnace.prefab`
- `ROOM_Junks.prefab`
- `ROOM_Deads.prefab`
- `ROOM_CubeCollector.prefab`

Laboratory is explicitly excluded from procedural room selection. Neither `ROOM_Laboratory_1.prefab` nor `ROOM_Laboratory_2.prefab` may be selected by this system.

The following are also not members of the randomized POI pool because they have structural roles rather than optional POI purposes:

- Start
- End
- Work
- Base
- Lift
- Laboratory

## Random Selection From Slot 8

POIs 8 and later use a true random choice from the complete eligible generated-room pool. Laboratory remains excluded. Duplicates are valid; there is no unused-type priority after slot 7.

### Example

For a map with eight POIs:

```text
POI 1: Resting
POI 2: Security
POI 3: Resting, Security, or Reception
POI 4: Resting, Security, or Reception
POI 5: Resting, Security, or Reception
POI 6: Garage
POI 7: Conveyor
POI 8: Any eligible generated type except Laboratory
```

The fixed slots guarantee the Worker Collector room dependencies as soon as the effective POI count reaches seven. Slot 8 begins unrestricted non-Laboratory random selection.

## Minimum Counts and Graceful Small-Map Behavior

There is no global minimum that automatically changes `poiCount`. The configured count determines which rules can be reached:

| Configured `poiCount` | Result |
|---:|---|
| 0 | No POIs |
| 1 | Resting only |
| 2 | Resting, then Security |
| 3 | Resting, Security, then one random Resting/Security/Reception room |
| 4 | Resting, Security, then two random Resting/Security/Reception rooms |
| 5 | Resting, Security, then three random Resting/Security/Reception rooms |
| 6 | The first five rules, then Garage; no Conveyor yet because slot 7 does not exist |
| 7 | The first five rules, then Garage and Conveyor in slots 6 and 7 |
| 8+ | The seven fixed/compact rules, followed by unrestricted random non-Laboratory rooms |

The Security guarantee applies when the requested and available POI count is at least two. A one-POI map remains valid with Resting only.

The current configuration intent is therefore preserved:

- `Setup Tutorial`: one POI, Resting only.
- `Setup Level 1`: two POIs, Resting and Security.
- `Setup Level 2`: three POIs, Resting, Security, and one random Resting/Security/Reception room.
- `Setup StressTest`: five POIs, using only Resting, Security, and Reception.

To guarantee the Garage-Conveyor pair, a configuration must request at least seven POIs and have enough eligible cells to create them. The unrestricted pool begins at eight POIs.

## Capacity and Invalid Configuration Rules

Start and End cannot also be POIs. The generator must calculate the number of eligible Work cells after assigning distinct endpoints.

```text
maximumPOICount = number of eligible Work cells after Start and End
effectivePOICount = clamp(configured poiCount, 0, maximumPOICount)
```

Required behavior:

- A negative `poiCount` is invalid and should be treated as zero with a clear warning or rejected during configuration validation.
- If `poiCount` exceeds available Work cells, create only the available number and issue a clear warning containing the requested and effective counts.
- Do not overwrite Start or End to satisfy a POI request.
- Do not increase the grid dimensions automatically.
- Do not increase `poiCount` automatically to force Reception or Security.
- The grid must have enough cells to assign distinct Start and End positions. A grid that cannot do so is an invalid map configuration and should fail clearly rather than generate ambiguous endpoints.
- `blockedCount` continues to consume only Work cells remaining after POI selection and path generation. If fewer eligible Work cells remain than requested, the existing effective blocked count may be lower than `blockedCount`.

The selection rule is based on the effective number of successfully selected POI positions. For example, if six POIs are requested but capacity allows only four, the result uses rules for POIs 1-4 and does not attempt a POI 6 room.

## Randomness and Seed Behavior

Room choice must use the same seeded Unity random sequence as the rest of map generation so the same configuration and seed reproduce the same map.

Implementation must avoid unordered collection enumeration as an input to random selection. Eligible room types should be held in a stable serialized or code-defined order before choosing a random index. This keeps results reproducible across runs and reduces accidental seed changes caused by dictionary or set ordering.

Exact generated layouts for existing seeds may change when this feature is introduced because room selection will consume additional random values. Reproducibility is required after the new algorithm is established; preserving every historical seeded layout is not a requirement unless separately requested.

## Proposed Data Model

The current `POIType` contains only `None`, `Reception`, and `Security`. It must be extended so every eligible generated room has an explicit identity.

Recommended enum direction:

```csharp
public enum POIType
{
    None,
    Reception,
    Security,
    Resting,
    Spawning,
    Garage,
    Conveyor,
    Furnace,
    Junks,
    Deads,
    CubeCollector
}
```

Append new values after existing values. Do not reorder `None`, `Reception`, or `Security`, because Unity serializes enum values numerically in scenes and prefabs.

`MapManager` needs a prefab mapping for every eligible `POIType`. The mapping must be validated before generation so a selected type cannot silently fall back to an unrelated prefab.

The first implementation can keep the selection policy in code because the slot rules are explicit. If designers later need weights, per-level pools, or unlock conditions, the room catalogue can move to serialized configuration without changing the core selection contract.

## Recommended Responsibility Split

### `GridFactory`

- Select eligible POI positions.
- Assign POI room types in slot order.
- Enforce Reception/Security thresholds.
- Prefer unused types for POIs 6+.
- Store the selected `POIType` on each cell before path solving.

### `MapManager`

- Expose prefab references for every supported POI type.
- Build and validate the complete `POIType -> GameObject` mapping.
- Pass the mapping to the renderer.

### `GridRenderer`

- Render the exact prefab mapped to the cell's `POIType`.
- Report a clear error for a missing selected-type mapping rather than substituting an incorrect room silently.

### `RunMapConfigSO`

- Continue to provide grid dimensions, `poiCount`, `blockedCount`, enemy counts, and seed.
- Does not need a new minimum-POI field for this design.

## Compatibility Considerations

Every newly generated room prefab must remain compatible with the generated-room lifecycle:

- contain the expected `RoomManager` and `RoomProperties` components;
- support grid-assigned doors and lifts;
- expose appropriate room waypoints;
- tolerate rotation/placement at any eligible grid coordinate;
- register its machines and waypoints through existing factory initialization;
- use `UsageType.POI` with the selected `POIType` after `GridManager.AssignRoomProperties`;
- have a valid minimap presentation;
- avoid dependencies that only exist in hand-authored static scenes.

Prefab compatibility must be verified separately from selection logic. Being present in the room pool does not by itself guarantee that the prefab functions correctly in a generated map.

## Implementation Sequence

1. Append the new explicit values to `POIType` without renumbering existing values.
2. Extract or add a deterministic POI room-type selector implementing the slot rules.
3. Update `GridFactory.AssignPOICells` to use that selector.
4. Add serialized prefab references and complete the mapping in `MapManager`.
5. Validate missing mappings with actionable error messages.
6. Inspect every eligible prefab for generated-room compatibility and correct any prefab-specific setup deliberately.
7. Add focused Edit Mode tests.
8. Run the complete Edit Mode suite.
9. Generate representative maps with POI counts 0, 1, 2, 5, 6, and more than the pool size, then inspect them in Unity.

## Automated Test Plan

Add focused Edit Mode tests covering:

1. **Zero POIs**
   - No cell is assigned `UsageType.POI`.

2. **One POI**
   - Exactly one POI exists.
   - Its type is Resting.

3. **Two POIs**
   - POI 1 is Resting.
   - POI 2 is Security.
   - Exactly one Security is guaranteed.

4. **POIs 3-5**
   - Every type in those slots is Resting, Security, or Reception.
   - No expanded-pool type appears in the first five slots.

5. **Fixed functional pair**
   - Slot 6 is Garage.
   - Slot 7 is Conveyor when it exists.
   - Neither fixed slot is replaced by random selection.

6. **Random pool after the pair**
   - Slots 8+ select from every eligible generated type.
   - Laboratory is never selected.
   - Duplicates are allowed without unused-type priority.

7. **Security guarantee**
   - Every generated assignment with an effective POI count of at least two contains Security.
   - An effective count of one remains Resting only.

8. **Capacity limits**
   - Start and End are never replaced by POIs.
   - A request larger than available capacity produces the effective maximum without throwing.
   - Selection rules use the effective slot count.

9. **Determinism**
   - The same valid seed and configuration produce the same POI positions and types.

10. **Prefab mapping completeness**
    - Every eligible generated `POIType` has a non-null prefab mapping.
    - Neither Laboratory prefab is in the generated mapping.

11. **Path compatibility**
    - Every selected POI remains a path target.
    - End remains a path target.
    - Room-type selection does not alter the independent Start-to-target path contract.

Run all Edit Mode tests using the repository test script:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/run-editmode-tests.ps1
```

## Manual Acceptance Test

1. Generate a map with zero POIs and confirm no POI room is present.
2. Generate maps with one and two POIs and confirm the exact Reception/Security thresholds.
3. Generate several five-POI maps with different seeds and confirm only Reception and Security appear.
4. Generate six- and seven-POI maps and confirm Garage and Conveyor occupy slots 6 and 7.
5. Generate maps with eight or more POIs and confirm slots 8+ use the full non-Laboratory pool with duplicates allowed.
6. Confirm no generated map contains either Laboratory prefab.
7. Confirm every POI and End are reachable from Start.
8. Enter every generated room type and verify doors, lifts, minimap visuals, waypoints, machines, and room initialization.
9. Regenerate twice with the same seed and configuration and confirm the POI layout and types match.

## Acceptance Criteria

The feature is complete when:

- small-map POI counts retain the documented Resting/Security/Reception composition;
- Security is guaranteed whenever at least two POIs can be created;
- Garage and Conveyor occupy fixed slots 6 and 7;
- all eligible types except Laboratory participate randomly from slot 8 onward;
- duplicates are allowed in the slot 8+ random pool;
- requested POI counts are bounded safely by grid capacity without changing Start or End;
- the same seed and configuration produce the same result;
- all selected room prefabs initialize and function in generated maps;
- automated Edit Mode tests and the manual acceptance pass succeed.
