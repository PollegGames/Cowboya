using UnityEngine;

/// <summary>
/// Keeps the logical white-cube reward attached to the physical cube even if
/// DocBot's corpse is collected before the player takes it.
/// </summary>
[DisallowMultipleComponent]
public sealed class LaboratoryWhiteCubeReward : MonoBehaviour {
    private LaboratoryProgress progress;
    private CubePickup cube;
    private bool claimed;

    /// <summary>
    /// Connects this representation to the current run reward counter.
    /// </summary>
    public void Configure(LaboratoryProgress laboratoryProgress) {
        progress = laboratoryProgress;
        if (cube == null) {
            cube = GetComponent<CubePickup>();
        }

        if (cube != null) {
            cube.OnGrabbed -= HandleGrabbed;
            cube.OnGrabbed += HandleGrabbed;
        }
    }

    private void HandleGrabbed(CubePickup grabbedCube) {
        if (claimed || grabbedCube == null || grabbedCube != cube || progress == null) {
            return;
        }

        Transform holder = grabbedCube.transform.parent;
        if (holder == null || holder.GetComponentInParent<CowboyGrabController>() == null) {
            return;
        }

        if (!progress.TryClaimAvailableWhiteCube()) {
            Debug.LogError("Laboratory white cube was grabbed without an available logical reward.", this);
            return;
        }

        claimed = true;
        cube.OnGrabbed -= HandleGrabbed;
        enabled = false;
    }

    private void OnDestroy() {
        if (cube != null) {
            cube.OnGrabbed -= HandleGrabbed;
        }
    }
}
