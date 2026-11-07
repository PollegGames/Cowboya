using UnityEngine;

/// <summary>
/// Marks a room of the laboratory scene and applies the run's unlock state when the scene loads.
/// A locked room is hidden and the neighbouring doors leading into it stay shut.
/// An unlocked room gets an exit trigger on each door configured as <see cref="StaticDoorState.VictoryExit"/>
/// and, for machine rooms, a cube intake on its machine.
/// </summary>
/// <remarks>
/// Runs before the scene setup so hidden rooms are never registered by the factory or the minimap.
/// </remarks>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class LaboratoryRoom : MonoBehaviour {
    private const string WhiteCubeMachineChildName = "MachineBuilder";
    private const string ColorCubeMachineChildName = "UpgradeStatsMachine";
    private const string CenterWaypointChildName = "CenterWaypoint";

    [SerializeField] private LaboratoryRoomId roomId = LaboratoryRoomId.WhiteCubes;

    [Header("Cube machine")]
    [Tooltip("Where the cube intake is created. Defaults to the room's machine child, then its centre.")]
    [SerializeField] private Transform cubeMachineAnchor;
    [SerializeField] private Vector2 cubeIntakeSize = new Vector2(2.5f, 2.5f);

    [Header("Exits")]
    [SerializeField] private Vector2 exitTriggerSize = new Vector2(0.8f, 3f);
    [Tooltip("Distance from the exit door towards the outside of the room.")]
    [SerializeField] private float exitTriggerOutwardOffset = 0.6f;
    [SerializeField] private int exitTriggerLayer = 3;

    [Header("Locked room")]
    [Tooltip("Maximum distance between this room's door and a neighbouring door that must stay shut while locked.")]
    [SerializeField] private float neighbourDoorSearchRadius = 15f;

    [Header("Debug")]
    [Tooltip("Editor only: show this room even when the run has not unlocked it.")]
    [SerializeField] private bool forceUnlockedInEditor;

    public LaboratoryRoomId RoomId => roomId;
    public bool IsUnlocked { get; private set; }

    private void Awake() {
        LaboratoryProgress progress = RunProgressManager.Instance != null
            ? RunProgressManager.Instance.LaboratoryProgress
            : null;
        IsUnlocked = ResolveUnlocked(progress);

        if (!IsUnlocked) {
            LockNeighbourDoors();
            Debug.Log($"[Laboratory] Room {roomId} is locked and hidden.", this);
            gameObject.SetActive(false);
            return;
        }

        CreateExitTriggers();
        CreateCubeMachine();
        Debug.Log($"[Laboratory] Room {roomId} is unlocked.", this);
    }

    /// <summary>
    /// Returns whether the room should be visible for the given progress. Null progress
    /// (scene played directly without a run) only shows the spawn room.
    /// </summary>
    public bool ResolveUnlocked(LaboratoryProgress progress) {
#if UNITY_EDITOR
        if (forceUnlockedInEditor) {
            return true;
        }
#endif
        if (progress == null) {
            return roomId == LaboratoryRoomId.WhiteCubes;
        }

        return progress.IsRoomUnlocked(roomId);
    }

    private void LockNeighbourDoors() {
        DoorController[] ownDoors = GetComponentsInChildren<DoorController>(true);
        DoorController[] allDoors = FindObjectsByType<DoorController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (DoorController ownDoor in ownDoors) {
            DoorController neighbour = FindFacingNeighbour(ownDoor, allDoors);
            if (neighbour == null) {
                continue;
            }

            neighbour.SetExternalLock(true);
            Debug.Log($"[Laboratory] Door '{neighbour.name}' locked: it leads to locked room {roomId}.", neighbour);
        }
    }

    private DoorController FindFacingNeighbour(DoorController ownDoor, DoorController[] candidates) {
        if (ownDoor == null) {
            return null;
        }

        float ownSide = Mathf.Sign(ownDoor.moveDirection.x);
        DoorController best = null;
        float bestDistance = neighbourDoorSearchRadius;
        foreach (DoorController candidate in candidates) {
            if (candidate == null || candidate.transform.IsChildOf(transform)) {
                continue;
            }

            if (Mathf.Sign(candidate.moveDirection.x) == ownSide) {
                continue;
            }

            float distance = Vector2.Distance(candidate.transform.position, ownDoor.transform.position);
            if (distance < bestDistance) {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private void CreateExitTriggers() {
        StaticRoomDoorConfig doorConfig = GetComponent<StaticRoomDoorConfig>();
        if (doorConfig == null) {
            return;
        }

        foreach (DoorController door in GetComponentsInChildren<DoorController>(true)) {
            bool isLeft = door.moveDirection.x < 0f;
            StaticDoorState state = isLeft ? doorConfig.LeftDoorState : doorConfig.RightDoorState;
            if (state == StaticDoorState.VictoryExit) {
                CreateExitTrigger(door, isLeft ? Vector2.left : Vector2.right);
            }
        }
    }

    private void CreateExitTrigger(DoorController door, Vector2 outward) {
        var exit = new GameObject($"LaboratoryExit_{roomId}_{(outward.x < 0f ? "Left" : "Right")}");
        exit.SetActive(false);
        exit.layer = exitTriggerLayer;
        exit.transform.SetParent(transform, true);
        exit.transform.position = door.transform.position + (Vector3)(outward * exitTriggerOutwardOffset);

        BoxCollider2D area = exit.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = exitTriggerSize;

        LevelEndVictoryTrigger trigger = exit.AddComponent<LevelEndVictoryTrigger>();
        trigger.ConfigureExitDoor(door);
        exit.SetActive(true);
    }

    private void CreateCubeMachine() {
        LaboratoryCubeMachineKind kind;
        string machineChildName;
        switch (roomId) {
            case LaboratoryRoomId.WhiteCubes:
                kind = LaboratoryCubeMachineKind.WhiteCubeRoomBuilder;
                machineChildName = WhiteCubeMachineChildName;
                break;
            case LaboratoryRoomId.ColorCubes:
                kind = LaboratoryCubeMachineKind.ColorCubeUpgrade;
                machineChildName = ColorCubeMachineChildName;
                break;
            default:
                return;
        }

        if (GetComponentInChildren<LaboratoryCubeMachine>(true) != null) {
            return; // An authored machine takes precedence.
        }

        Transform anchor = cubeMachineAnchor != null
            ? cubeMachineAnchor
            : FindDescendant(transform, machineChildName) ?? FindDescendant(transform, CenterWaypointChildName) ?? transform;

        var intake = new GameObject($"CubeIntake_{kind}");
        intake.SetActive(false);
        intake.transform.SetParent(anchor, false);

        BoxCollider2D area = intake.AddComponent<BoxCollider2D>();
        area.isTrigger = true;
        area.size = cubeIntakeSize;

        LaboratoryCubeMachine machine = intake.AddComponent<LaboratoryCubeMachine>();
        machine.Configure(kind);
        intake.SetActive(true);
    }

    private static Transform FindDescendant(Transform root, string childName) {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) {
            if (child != root && child.name == childName) {
                return child;
            }
        }

        return null;
    }
}
