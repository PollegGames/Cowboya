using System;
using UnityEngine;

public enum GarageProcessorState
{
    Open,
    Closing,
    Processing,
    Opening
}

/// <summary>
/// Coordinates authoritative garage occupancy with the internal machine door.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GarageCubeStorage))]
public sealed class GarageCubeProcessor : MonoBehaviour
{
    [SerializeField] private GarageCubeStorage storage;
    [SerializeField] private GarageDoorController door;
    [SerializeField, Min(0f)] private float closedHoldDuration = 0.5f;

    private float closedHoldRemaining;
    private bool subscribed;
    private bool removingCompletedBatch;

    public event Action<int> OnBatchCompleted;
    public event Action OnBatchInterrupted;

    public GarageProcessorState State { get; private set; } = GarageProcessorState.Open;
    public int CompletedBatchCount { get; private set; }
    public bool IsDoorOpen => door != null && door.IsOpen;
    public GarageCubeStorage Storage => storage;
    public bool IsAccepting => isActiveAndEnabled && State == GarageProcessorState.Open
        && door != null && door.IsOpen && storage != null && !storage.IsFull;

    private void Awake() => ResolveReferences();

    private void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        SynchronizeState();
    }

    private void OnDisable() => Unsubscribe();

    private void Update() => StepProcessing(Time.deltaTime);

    public void Configure(GarageCubeStorage configuredStorage, GarageDoorController configuredDoor,
        float holdDuration = 0.5f)
    {
        Unsubscribe();
        storage = configuredStorage;
        door = configuredDoor;
        closedHoldDuration = Mathf.Max(0f, holdDuration);
        if (isActiveAndEnabled)
        {
            Subscribe();
            SynchronizeState();
        }
    }

    /// <summary>
    /// Advances the closed-door processing hold for deterministic tests.
    /// </summary>
    public void StepProcessing(float deltaTime)
    {
        if (State != GarageProcessorState.Processing || deltaTime < 0f)
            return;
        if (storage == null || !storage.IsFull)
        {
            OnBatchInterrupted?.Invoke();
            BeginOpening();
            return;
        }

        closedHoldRemaining -= deltaTime;
        if (closedHoldRemaining > 0f)
            return;
        bool processed;
        removingCompletedBatch = true;
        try {
            processed = storage.ProcessFullBatch();
        }
        finally {
            removingCompletedBatch = false;
        }
        if (!processed)
        {
            BeginOpening();
            return;
        }

        CompletedBatchCount++;
        OnBatchCompleted?.Invoke(CompletedBatchCount);
        BeginOpening();
    }

    private void HandleOccupancyChanged(int count)
    {
        if (removingCompletedBatch)
            return;
        if (State == GarageProcessorState.Processing) {
            if (count < GarageCubeStorage.Capacity) {
                OnBatchInterrupted?.Invoke();
                BeginOpening();
            }
            return;
        }
        if (count == GarageCubeStorage.Capacity && State == GarageProcessorState.Open)
        {
            State = GarageProcessorState.Closing;
            door?.CloseDoor();
        }
        else if (count < GarageCubeStorage.Capacity && State == GarageProcessorState.Closing)
        {
            OnBatchInterrupted?.Invoke();
            BeginOpening();
        }
    }

    private void HandleDoorClosed()
    {
        if (State != GarageProcessorState.Closing)
            return;
        if (storage == null || !storage.IsFull)
        {
            BeginOpening();
            return;
        }
        State = GarageProcessorState.Processing;
        closedHoldRemaining = Mathf.Max(0f, closedHoldDuration);
        if (closedHoldRemaining <= 0f)
            StepProcessing(0f);
    }

    private void HandleDoorOpened()
    {
        State = GarageProcessorState.Open;
        if (storage != null && storage.IsFull)
            HandleOccupancyChanged(storage.OccupiedCount);
    }

    private void BeginOpening()
    {
        State = GarageProcessorState.Opening;
        if (door == null)
        {
            State = GarageProcessorState.Open;
            return;
        }
        door.OpenDoor();
        if (door.IsOpen)
            HandleDoorOpened();
    }

    private void SynchronizeState()
    {
        if (storage == null || door == null)
            return;
        if (door.IsClosed)
        {
            State = storage.IsFull ? GarageProcessorState.Processing : GarageProcessorState.Opening;
            if (State == GarageProcessorState.Processing)
                closedHoldRemaining = Mathf.Max(0f, closedHoldDuration);
            else
                door.OpenDoor();
        }
        else
        {
            State = door.IsOpen ? GarageProcessorState.Open : GarageProcessorState.Opening;
            if (storage.IsFull && State == GarageProcessorState.Open)
                HandleOccupancyChanged(storage.OccupiedCount);
        }
    }

    private void ResolveReferences()
    {
        if (storage == null)
            storage = GetComponent<GarageCubeStorage>();
        if (door == null)
            door = GetComponentInChildren<GarageDoorController>(true);
    }

    private void Subscribe()
    {
        if (subscribed)
            return;
        if (storage != null)
            storage.OnOccupancyChanged += HandleOccupancyChanged;
        if (door != null)
        {
            door.OnClosed += HandleDoorClosed;
            door.OnOpened += HandleDoorOpened;
        }
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;
        if (storage != null)
            storage.OnOccupancyChanged -= HandleOccupancyChanged;
        if (door != null)
        {
            door.OnClosed -= HandleDoorClosed;
            door.OnOpened -= HandleDoorOpened;
        }
        subscribed = false;
    }
}
