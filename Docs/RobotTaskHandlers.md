Robot Task Handlers
===================

What this is
------------
Data-driven mapping from `RobotTaskType` -> Scriptable handler that the **Brain** executes when the Heart’s top intent changes.

Core flow
---------
- Gameplay code talks to the **Brain**, not the Heart. Call `RobotBrain.OnMachineStateChanged(payload, isOn)` or another Brain API.
- The Brain decides which `RobotTask` to push into the Heart (e.g., ReactivateMachine when a machine turns off, WorkAtMachine when it turns on).
- When the Heart surface changes, the Brain looks up a handler in a `RobotTaskHandlers` asset and executes it. If no handler exists, it falls back to simple movement and logs a warning.

How to set it up in the Editor
------------------------------
1) Create a `RobotTaskHandlers` asset: `Create -> RobotAI -> RobotTaskHandlers`.
2) Create handler assets for the tasks you need (below). Each handler derives from `ScriptableRobotTaskHandler`.
3) Open the `RobotTaskHandlers` asset and add entries mapping `RobotTaskType` to the handler assets.
4) On each robot prefab:
   - Set `RobotHeart.Role`.
   - Assign the default `taskHandlers` asset on `RobotBrain` (or fill `roleTaskHandlers` to override by role).
5) Machines and managers should call `RobotBrain.OnMachineStateChanged` (or another Brain API) when their state changes; do **not** push directly into the Heart.

Writing a handler
-----------------
```csharp
using UnityEngine;

[CreateAssetMenu(menuName = "RobotAI/Handlers/WorkAtMachine")]
public class WorkAtMachineHandler : ScriptableRobotTaskHandler
{
    public override void Execute(RobotBrain brain, object payload)
    {
        if (brain == null || brain.Body == null) return;

        if (payload is BaseMachine machine && machine != null)
        {
            // Route to the machine’s waypoint if present; otherwise its position.
            var wp = machine.GetComponent<RoomWaypoint>();
            if (wp != null) brain.Body.SetDestination(wp);
            else brain.Body.SetDestination(machine.transform.position);
        }
    }
}
```

Existing handlers
-----------------
- `MoveToPayloadHandler`: Moves to a `RoomWaypoint` or a machine’s waypoint.
- `ChaseTargetHandler`: Moves toward a target transform or the last known player position.
- `AttackHandler`: Triggers `RobotAttackController` toward a target position/transform.
- `IdleHandler`: No-op placeholder.

Existing wiring in code
-----------------------
- Machines call `RobotBrain.OnMachineStateChanged` inside their own methods (e.g., `FactoryMachine.SendWorkerToWork`, `RestingMachine.SendWorkerToRest`, `SecurityMachine.SetGuardToSecurityPost`).
- `MachineWorkerManager` and `MachineSecurityManager` listen to machine events and forward them to the Brain via `OnMachineStateChanged`.
- `EnemiesSpawner.SpreadEnemies` seeds workers/guards at work/rest/security POIs; machines then notify the Brain when robots enter slots and are assigned.
