using System.Collections.Generic;
using UnityEngine;

public enum PickupType
{
    SecurityBadge,
    Battery,
    Cube
}

/// <summary>
/// Simple inventory that tracks one pickup per slot.
/// </summary>
public class Inventory : MonoBehaviour
{
    private readonly Dictionary<PickupType, IGrabbable> items = new Dictionary<PickupType, IGrabbable>();

    /// <summary>
    /// Checks if the inventory currently holds an item of the given type.
    /// </summary>
    public bool HasItem(PickupType type)
    {
        return items.ContainsKey(type) && items[type] != null;
    }

    /// <summary>
    /// Gets the item stored in the specified slot, if any.
    /// </summary>
    public IGrabbable GetItem(PickupType type)
    {
        items.TryGetValue(type, out var item);
        return item;
    }

    /// <summary>
    /// Sets the item stored in the specified slot.
    /// </summary>
    public void SetItem(PickupType type, IGrabbable item)
    {
        items[type] = item;
    }

    /// <summary>
    /// Removes the item stored in the specified slot.
    /// </summary>
    public void RemoveItem(PickupType type)
    {
        if (items.ContainsKey(type))
            items.Remove(type);
    }

    /// <summary>
    /// Drops the item in the specified slot by releasing it and clearing the slot.
    /// </summary>
    public void DropItem(PickupType type)
    {
        if (items.TryGetValue(type, out var item) && item != null)
        {
            item.OnRelease(Vector2.zero);
        }
        items.Remove(type);
    }

    /// <summary>
    /// Drops all items currently held.
    /// </summary>
    public void DropAll()
    {
        var itemsToDrop = new List<IGrabbable>(items.Values);
        foreach (var item in itemsToDrop)
        {
            item?.OnRelease(Vector2.zero);
        }
        items.Clear();
    }
}

