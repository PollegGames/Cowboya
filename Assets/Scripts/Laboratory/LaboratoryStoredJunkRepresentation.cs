using UnityEngine;

/// <summary>
/// Keeps the physical representation of Junk returned after DocBot's death
/// non-grabbable until the laboratory scene unloads it into persistent storage.
/// </summary>
[DisallowMultipleComponent]
public sealed class LaboratoryStoredJunkRepresentation : MonoBehaviour {
    private JunkPickup junk;

    /// <summary>
    /// Atomically takes the Junk grab lock from its previous owner.
    /// </summary>
    public bool TryReserve(JunkPickup pickup, UnityEngine.Object previousOwner) {
        if (pickup == null || previousOwner == null) {
            return false;
        }

        if (pickup.GrabLockOwner == this) {
            junk = pickup;
            return true;
        }

        if (pickup.GrabLockOwner != previousOwner || !pickup.UnlockGrab(previousOwner)) {
            return false;
        }

        if (pickup.TryLockGrab(this)) {
            junk = pickup;
            return true;
        }

        pickup.TryLockGrab(previousOwner);
        return false;
    }

    private void OnDestroy() {
        if (junk != null && junk.GrabLockOwner == this) {
            junk.UnlockGrab(this);
        }
    }
}
