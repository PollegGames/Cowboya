using UnityEngine;

/// <summary>
/// Placed on a connecting room of the laboratory (a lift). Names the laboratory room behind each
/// of its doors and keeps a door shut while that room is locked for the run.
/// </summary>
[DefaultExecutionOrder(-900)]
[DisallowMultipleComponent]
public sealed class LaboratoryPassageGate : MonoBehaviour {
    [SerializeField] private bool gateLeftDoor = true;
    [SerializeField] private LaboratoryRoomId leftRoom = LaboratoryRoomId.WhiteCubes;
    [SerializeField] private bool gateRightDoor = true;
    [SerializeField] private LaboratoryRoomId rightRoom = LaboratoryRoomId.Stats;

    private void Awake() {
        LaboratoryProgress progress = RunProgressManager.Instance != null
            ? RunProgressManager.Instance.LaboratoryProgress
            : null;
        Apply(progress);
    }

    /// <summary>
    /// Locks or unlocks each gated door according to the run's unlocked rooms.
    /// Without a run (scene played directly) only the spawn room counts as unlocked.
    /// </summary>
    public void Apply(LaboratoryProgress progress) {
        foreach (DoorController door in GetComponentsInChildren<DoorController>(true)) {
            bool isLeft = door.moveDirection.x < 0f;
            if (isLeft ? !gateLeftDoor : !gateRightDoor) {
                continue;
            }

            LaboratoryRoomId room = isLeft ? leftRoom : rightRoom;
            bool unlocked = progress != null
                ? progress.IsRoomUnlocked(room)
                : room == LaboratoryRoomId.WhiteCubes;
            door.SetExternalLock(!unlocked);
            if (!unlocked) {
                Debug.Log($"[Laboratory] {name}: {(isLeft ? "left" : "right")} door locked, room {room} is locked.", door);
            }
        }
    }
}
