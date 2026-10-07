using UnityEngine;

/// <summary>
/// Establishes the garage machine readiness boundary before worker providers register.
/// </summary>
[DisallowMultipleComponent]
public sealed class GarageCubeCoordinator : MonoBehaviour
{
    [SerializeField] private GarageDoorController door;
    [SerializeField] private GarageCubeStorage storage;
    [SerializeField] private GarageCubeProcessor processor;

    public bool IsReady { get; private set; }
    public GarageDoorController Door => door;
    public GarageCubeStorage Storage => storage;
    public GarageCubeProcessor Processor => processor;

    private void Awake() => ResolveReferences();

    private void Start()
    {
        ResolveReferences();
        IsReady = door != null && storage != null && processor != null
            && door.isActiveAndEnabled && storage.isActiveAndEnabled && processor.isActiveAndEnabled
            && storage.Slots.Count == GarageCubeStorage.Capacity;
        if (!IsReady)
        {
            Debug.LogWarning(
                $"GarageCubeCoordinator is not ready: doorActive={door != null && door.isActiveAndEnabled}, "
                + $"storageActive={storage != null && storage.isActiveAndEnabled}, "
                + $"processorActive={processor != null && processor.isActiveAndEnabled}, "
                + $"slots={(storage != null ? storage.Slots.Count.ToString() : "null") }.", this);
        }
    }

    private void ResolveReferences()
    {
        if (door == null)
            door = GetComponent<GarageDoorController>();
        if (storage == null)
            storage = GetComponent<GarageCubeStorage>();
        if (processor == null)
            processor = GetComponent<GarageCubeProcessor>();
    }
}