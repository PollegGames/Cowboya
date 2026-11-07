using UnityEngine;

/// <summary>
/// Authored exit area of a laboratory room. Place it behind the exit door and size its
/// trigger collider; when the room is unlocked, <see cref="LaboratoryRoom"/> binds it to the
/// nearest exit door and turns it into a level-exit trigger.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[DisallowMultipleComponent]
public sealed class LaboratoryExitPoint : MonoBehaviour {
    private void Reset() {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnDrawGizmos() {
        Collider2D area = GetComponent<Collider2D>();
        if (area == null) {
            return;
        }

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
        Gizmos.DrawCube(area.bounds.center, area.bounds.size);
    }
}
