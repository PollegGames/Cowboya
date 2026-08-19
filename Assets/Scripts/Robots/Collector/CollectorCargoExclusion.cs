using UnityEngine;

/// <summary>
/// Explicitly excludes a body or hierarchy branch from Collector corpse cargo.
/// </summary>
public sealed class CollectorCargoExclusion : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Detach this branch before the collected corpse root is recycled.")]
    private bool detachOnCollection;

    public bool DetachOnCollection => detachOnCollection;

    /// <summary>
    /// Configures whether this excluded payload is detached before its corpse root
    /// is recycled or destroyed.
    /// </summary>
    public void Configure(bool shouldDetachOnCollection) {
        detachOnCollection = shouldDetachOnCollection;
    }
}
