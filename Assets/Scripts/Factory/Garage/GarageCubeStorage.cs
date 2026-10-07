using System;
using System.Collections.Generic;
using UnityEngine;

public readonly struct GarageSlotReservation : IEquatable<GarageSlotReservation>
{
    public GarageSlotReservation(int storageInstanceId, int ownerInstanceId, int slotIndex, int version)
    {
        StorageInstanceId = storageInstanceId;
        OwnerInstanceId = ownerInstanceId;
        SlotIndex = slotIndex;
        Version = version;
    }

    public int StorageInstanceId { get; }
    public int OwnerInstanceId { get; }
    public int SlotIndex { get; }
    public int Version { get; }
    public bool IsValid => StorageInstanceId != 0 && OwnerInstanceId != 0 && SlotIndex >= 0 && Version > 0;

    public bool Equals(GarageSlotReservation other) => StorageInstanceId == other.StorageInstanceId
        && OwnerInstanceId == other.OwnerInstanceId && SlotIndex == other.SlotIndex && Version == other.Version;
    public override bool Equals(object obj) => obj is GarageSlotReservation other && Equals(other);
    public override int GetHashCode() => (StorageInstanceId, OwnerInstanceId, SlotIndex, Version).GetHashCode();
    public static bool operator ==(GarageSlotReservation left, GarageSlotReservation right) => left.Equals(right);
    public static bool operator !=(GarageSlotReservation left, GarageSlotReservation right) => !left.Equals(right);
}

/// <summary>
/// Owns the nine physical cube positions and all delivery reservations for a garage.
/// </summary>
[DisallowMultipleComponent]
public sealed class GarageCubeStorage : MonoBehaviour
{
    public const int Capacity = 9;

    [SerializeField] private Transform[] slots = new Transform[Capacity];

    private readonly WhiteCubeCargo[] contents = new WhiteCubeCargo[Capacity];
    private readonly Dictionary<int, GarageSlotReservation> reservations = new Dictionary<int, GarageSlotReservation>();
    private int reservationVersion;
    private bool shuttingDown;

    public event Action<int> OnOccupancyChanged;
    public event Action<bool> OnFullStateChanged;

    public int OccupiedCount
    {
        get
        {
            RemoveDestroyedContents();
            int count = 0;
            for (int i = 0; i < contents.Length; i++)
            {
                if (contents[i] != null)
                    count++;
            }
            return count;
        }
    }
    public bool IsFull => OccupiedCount == Capacity;
    public IReadOnlyList<Transform> Slots => slots;

    private void Awake() => ResolveSlots();

    private void OnEnable()
    {
        shuttingDown = false;
        ResolveSlots();
        for (int i = 0; i < contents.Length; i++)
            Subscribe(contents[i]);
    }

    private void OnDisable()
    {
        shuttingDown = true;
        reservations.Clear();
        for (int i = 0; i < contents.Length; i++)
            Unsubscribe(contents[i]);
    }

    /// <summary>
    /// Configures the explicit bottom-to-top fill order used by this storage.
    /// </summary>
    public void ConfigureSlots(Transform[] configuredSlots)
    {
        if (configuredSlots == null || configuredSlots.Length != Capacity)
            throw new ArgumentException($"Garage storage requires exactly {Capacity} slots.", nameof(configuredSlots));
        slots = configuredSlots;
    }

    /// <summary>
    /// Reserves the first empty, unreserved slot for one approaching worker.
    /// </summary>
    public bool TryReserve(UnityEngine.Object owner, out GarageSlotReservation reservation)
    {
        reservation = default;
        if (owner == null || !isActiveAndEnabled)
            return false;

        int ownerId = owner.GetInstanceID();
        if (reservations.TryGetValue(ownerId, out reservation))
            return IsReservationValid(reservation);

        for (int i = 0; i < Capacity; i++)
        {
            if (contents[i] != null || IsSlotReserved(i) || slots[i] == null)
                continue;
            reservation = new GarageSlotReservation(GetInstanceID(), ownerId, i, ++reservationVersion);
            reservations.Add(ownerId, reservation);
            return true;
        }

        string slotState = string.Empty;
        for (int i = 0; i < Capacity; i++)
        {
            if (i > 0)
                slotState += ", ";
            slotState += $"{i}:occupied={contents[i] != null}/reserved={IsSlotReserved(i)}/assigned={slots[i] != null}";
        }
        Debug.LogWarning(
            $"GarageCubeStorage could not reserve a slot. active={isActiveAndEnabled}, "
            + $"occupied={OccupiedCount}, reservations={reservations.Count}, slots=[{slotState}].", this);
        return false;
    }

    public bool IsReservationValid(GarageSlotReservation reservation)
    {
        return isActiveAndEnabled && reservation.IsValid && reservation.StorageInstanceId == GetInstanceID()
            && reservation.SlotIndex < Capacity && contents[reservation.SlotIndex] == null
            && reservations.TryGetValue(reservation.OwnerInstanceId, out GarageSlotReservation current)
            && current == reservation;
    }

    public bool ReleaseReservation(GarageSlotReservation reservation)
    {
        if (!reservation.IsValid || !reservations.TryGetValue(reservation.OwnerInstanceId, out GarageSlotReservation current)
            || current != reservation)
            return false;
        reservations.Remove(reservation.OwnerInstanceId);
        return true;
    }

    /// <summary>
    /// Moves the claimed physical cube into its reserved slot without cloning it.
    /// </summary>
    public bool TryAccept(WhiteCubeCargo cargo, WhiteCubeClaim claim, GarageSlotReservation reservation)
    {
        if (cargo == null || !IsReservationValid(reservation) || !cargo.IsClaimValid(claim))
            return false;

        int before = OccupiedCount;
        Transform slot = slots[reservation.SlotIndex];
        if (!cargo.TryStore(claim, slot))
            return false;

        reservations.Remove(reservation.OwnerInstanceId);
        contents[reservation.SlotIndex] = cargo;
        Subscribe(cargo);
        RaiseOccupancy(before);
        return true;
    }

    public WhiteCubeCargo GetStoredCube(int slotIndex) => slotIndex >= 0 && slotIndex < Capacity
        ? contents[slotIndex] : null;

    /// <summary>
    /// Removes only the currently registered full batch and returns whether all nine were processed.
    /// </summary>
    public bool ProcessFullBatch()
    {
        if (!IsFull)
            return false;

        WhiteCubeCargo[] batch = (WhiteCubeCargo[])contents.Clone();
        for (int i = 0; i < batch.Length; i++)
        {
            if (batch[i] == null)
                return false;
        }

        int before = Capacity;
        for (int i = 0; i < batch.Length; i++)
        {
            WhiteCubeCargo cargo = batch[i];
            Unsubscribe(cargo);
            contents[i] = null;
            if (cargo != null)
            {
                if (Application.isPlaying)
                    Destroy(cargo.gameObject);
                else
                    DestroyImmediate(cargo.gameObject);
            }
        }
        RaiseOccupancy(before);
        return true;
    }

    private void HandleStoredCubeGrabbed(CubePickup pickup)
    {
        if (pickup == null)
            return;
        WhiteCubeCargo cargo = pickup.GetComponent<WhiteCubeCargo>();
        RemoveStoredCargo(cargo, restorePhysics: true);
    }

    private void HandleStoredCubeDestroyed(WhiteCubeCargo cargo)
    {
        RemoveStoredCargo(cargo, restorePhysics: false);
    }

    private void RemoveStoredCargo(WhiteCubeCargo cargo, bool restorePhysics)
    {
        if (cargo == null || shuttingDown)
            return;
        int slotIndex = Array.IndexOf(contents, cargo);
        if (slotIndex < 0)
            return;

        int before = OccupiedCount;
        Unsubscribe(cargo);
        contents[slotIndex] = null;
        if (restorePhysics)
            cargo.RestoreLoosePhysics();
        RaiseOccupancy(before);
    }

    private void Subscribe(WhiteCubeCargo cargo)
    {
        if (cargo == null || cargo.Pickup == null)
            return;
        cargo.Pickup.OnGrabbed -= HandleStoredCubeGrabbed;
        cargo.Pickup.OnGrabbed += HandleStoredCubeGrabbed;
        cargo.OnDestroyed -= HandleStoredCubeDestroyed;
        cargo.OnDestroyed += HandleStoredCubeDestroyed;
    }

    private void Unsubscribe(WhiteCubeCargo cargo)
    {
        if (cargo == null)
            return;
        if (cargo.Pickup != null)
            cargo.Pickup.OnGrabbed -= HandleStoredCubeGrabbed;
        cargo.OnDestroyed -= HandleStoredCubeDestroyed;
    }

    private bool IsSlotReserved(int slotIndex)
    {
        foreach (GarageSlotReservation reservation in reservations.Values)
        {
            if (reservation.SlotIndex == slotIndex)
                return true;
        }
        return false;
    }

    private void RemoveDestroyedContents()
    {
        for (int i = 0; i < contents.Length; i++)
        {
            if (contents[i] == null)
                contents[i] = null;
        }
    }

    private void RaiseOccupancy(int previousCount)
    {
        int current = OccupiedCount;
        if (current == previousCount)
            return;
        OnOccupancyChanged?.Invoke(current);
        if ((previousCount == Capacity) != (current == Capacity))
            OnFullStateChanged?.Invoke(current == Capacity);
    }

    private void ResolveSlots()
    {
        if (slots == null || slots.Length != Capacity)
            slots = new Transform[Capacity];
        Transform root = transform.Find("Slots");
        if (root == null)
            return;
        for (int i = 0; i < Capacity; i++)
        {
            if (slots[i] == null)
                slots[i] = root.Find($"Slot_{i:00}");
        }
    }
}
