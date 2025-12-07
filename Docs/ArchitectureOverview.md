# Architecture Overview

This document provides a high level summary of the main modules in *Cowboya* and how they interact. Referenced diagrams use simple ASCII to outline data flow.

## FactoryCore
The `FactoryCore` scripts orchestrate the factory and connect major systems.
- **FactoryManager** – central coordinator. Holds references to `MachineWorkerManager`, `MachineSecurityManager` and services like `WaypointService`.
- **RoomManager** – manages rooms, registers waypoints and machines with `WaypointService` and machine managers.
- **LiftController** and **LiftShaftController** – handle elevator movement between floors.

```
FactoryManager
    | initializes
    v
MapManager ---> WaypointService
    |                   |
    v                   v
RoomManager ----> Machines
```

## Map / Navigation
Components under `Navigation` and `Map` build the layout and provide pathfinding.
- **MapManager** – builds the grid from `RunMapConfigSO` using builders and renderers.
- **WaypointService** – stores `RoomWaypoint` data, reserves points for robots and returns paths via `WaypointPathFinder`.
- **WaypointPathFollower** – moves robots along computed paths.

## Machines
Scripts in `Machines` describe all factory devices and how robots interact with them.
- **FactoryMachine** – base workstation that workers operate on.
- **RestingMachine, SecurityMachine, SpawningMachine** – specialized machines.
- **MachineWorkerManager** – redirects workers when machines switch state. See [WorkerMachineFlow](../Assets/Doc/WorkerMachineFlow.md).
- **MachineSecurityManager** – dispatches guards to restart stopped machines. See [SecurityGuardMachineFlow](../Assets/Doc/SecurityGuardMachineFlow.md).

### Interaction Flow
```
FactoryMachine --(state change)--> MachineWorkerManager
                              \--> MachineSecurityManager
MachineWorkerManager --> workers use WaypointService to find rest points
MachineSecurityManager --> guards reactivate machines
```

## Robot AI
Robots (workers, guards, etc.) share the Brain/Heart/Body/Memory architecture (see `RobotAI_HeartBrainMemoryBody.md`).
- **Brain**: single entry point for world events (machines, alarms, managers); decides which task to push.
- **Heart**: runs the task stack/queue (e.g., `WorkAtMachine`, `GoToRest`, `ReactivateMachine`, `GuardPost`) and tells the Body what to execute.
- **Body**: moves/animates/handles interactions; reports completion back to the Heart.
- **Memory**: stores factual data to support Brain/Heart decisions.

Machines only emit state + target points; they never move robots. Managers route those events to Brains:
- **MachineWorkerManager**: listens to machine ON/OFF and forwards to the assigned worker Brain (see `WorkerMachineFlow.md`).
- **MachineSecurityManager**: listens to machine OFF and forwards reactivation requests to a chosen guard Brain (see `SecurityGuardMachineFlow.md`).

Cycles:
- Worker: `WorkAtMachine` (ON) → machine OFF → `GoToRest` → machine ON → `WorkAtMachine`.
- Guard: default `GuardPost` → machine OFF → `ReactivateMachine` → machine ON → `GuardPost`.

## Player
Player scripts manage controls, stats and interaction.
- **PlayerInputReader** and movement/attack controllers handle input.
- **PlayerStats** tracks health, energy and other upgrades.
- **GrabSystem** allows interacting with grabbable objects and levers.

## UI
UI elements live under `UI`.
- **GameUIViewModel** binds HUD components (health bar, energy bar, minimap) to the player and messages.
- **MessageSystem** displays notifications like game over or tutorial hints.

## Setup
Initialization happens in `Setup`.
- **SceneBootstrapper** instantiates managers and services based on `SceneBootstrapConfigSO`.
- **GameInitiator** ensures a camera, event system and Cinemachine setup exist.
- **SceneInitiator** ties all created instances together so gameplay can start.

### Boot Flow
```
SceneBootstrapper --> SceneInitiator
        |              |
        |         GameInitiator (camera/event system)
        |              v
        |------- FactoryManager
                    |
                    v
           PlayerSpawner & others
```

These modules work together to build each run: the scene boots services, the factory map is generated, machines register with managers, and both AI and player characters rely on the waypoint system for movement.
