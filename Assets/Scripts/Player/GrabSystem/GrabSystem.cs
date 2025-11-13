using UnityEngine;

public class GrabSystem : MonoBehaviour
{
    public GrabHandAttractor leftHand;
    public GrabHandAttractor rightHand;
    public float throwStrength = 5f;

    [SerializeField] private MonoBehaviour inputSource;
    [SerializeField] private Inventory inventoryOverride;
    private IPlayerInput input;

    private IGrabbable leftHeld;
    private IGrabbable rightHeld;
    private Inventory cachedInventory;

    private void Awake()
    {
        input = ResolveInputProvider();
        if (input == null)
            Debug.LogError($"GrabSystem on {name}: no IPlayerInput found. Assign an input source in the inspector.");
    }

    private void Update()
    {
        if (input == null) return;

        // LEFT HAND
        if (input.LeftGrabDown)
        {
            if (leftHeld == null)
                TryGrab(leftHand, ref leftHeld);
        }
        else if (input.LeftGrabUp)
        {
            if (leftHeld != null)
            {
                UnityEngine.Object obj = leftHeld as UnityEngine.Object;
                if (obj != null)
                    Release(leftHand, ref leftHeld);
                else
                    leftHeld = null;
            }
        }

        if (leftHeld != null && input.LeftGrabHeld)
        {
            UnityEngine.Object obj = leftHeld as UnityEngine.Object;
            if (obj != null)
                leftHeld.OnAttract(leftHand.transform.position);
            else
                leftHeld = null;
        }

        // RIGHT HAND (same pattern)
        if (input.RightGrabDown)
        {
            if (rightHeld == null)
                TryGrab(rightHand, ref rightHeld);
        }
        else if (input.RightGrabUp)
        {
            if (rightHeld != null)
            {
                UnityEngine.Object obj = rightHeld as UnityEngine.Object;
                if (obj != null)
                    Release(rightHand, ref rightHeld);
                else
                    rightHeld = null;
            }
        }

        if (rightHeld != null && input.RightGrabHeld)
        {
            UnityEngine.Object obj = rightHeld as UnityEngine.Object;
            if (obj != null)
                rightHeld.OnAttract(rightHand.transform.position);
            else
                rightHeld = null;
        }
    }

    /// <summary>
    /// Releases any held objects without applying a throw force.
    /// </summary>
    public void ClearHands()
    {
        if (leftHeld != null)
        {
            UnityEngine.Object obj = leftHeld as UnityEngine.Object;
            if (obj != null)
            {
                Release(leftHand, ref leftHeld, 0f);
            }
            else
            {
                leftHeld = null;
            }
        }

        if (rightHeld != null)
        {
            UnityEngine.Object obj = rightHeld as UnityEngine.Object;
            if (obj != null)
            {
                Release(rightHand, ref rightHeld, 0f);
            }
            else
            {
                rightHeld = null;
            }
        }
    }

    private void TryGrab(GrabHandAttractor hand, ref IGrabbable held)
    {
        if (hand == null || held != null) return;
        IGrabbable obj = hand.DetectGrabbable();
        if (obj == null)
            return;

        Transform grabParent = ResolveGrabParent(hand.transform, obj);
        var inventory = ResolveInventory(grabParent);
        if (!obj.CanBeGrabbed(inventory))
            return;

        PickupType? slot = ResolveInventorySlot(obj);
        if (slot.HasValue && !IsInventorySlotAvailable(inventory, slot.Value, obj))
            return;

        obj.OnGrab(grabParent);

        if (inventory != null && slot.HasValue)
            inventory.SetItem(slot.Value, obj);

        held = IsInventoryOnlyPickup(obj) ? null : obj;
    }

    private void Release(GrabHandAttractor hand, ref IGrabbable held)
    {
        Release(hand, ref held, throwStrength);
    }

    private void Release(GrabHandAttractor hand, ref IGrabbable held, float strength)
    {
        UnityEngine.Object obj = held as UnityEngine.Object;
        if (hand == null || obj == null)
        {
            held = null;
            return;
        }

        var inventory = ResolveInventory(hand.transform);
        PickupType? slot = ResolveInventorySlot(held);
        if (inventory != null && slot.HasValue)
            inventory.RemoveItem(slot.Value);

        Vector2 throwForce = (Vector2)(hand.transform.right) * strength;
        held.OnRelease(throwForce);
        held = null;
    }

    private Inventory ResolveInventory(Transform contextTransform)
    {
        if (inventoryOverride != null)
            return inventoryOverride;

        if (cachedInventory != null)
            return cachedInventory;

        Inventory found = null;

        if (contextTransform != null)
        {
            found = contextTransform.GetComponentInParent<Inventory>();
            if (found == null)
                found = contextTransform.GetComponent<Inventory>();

            if (found == null && contextTransform.root != null)
                found = contextTransform.root.GetComponentInChildren<Inventory>();
        }

        if (found == null)
        {
            found = GetComponentInParent<Inventory>();
            if (found == null)
                found = GetComponent<Inventory>();
        }

        if (found == null)
        {
            var candidates = UnityEngine.Object.FindObjectsByType<Inventory>(FindObjectsSortMode.None);
            if (candidates != null && candidates.Length > 0)
                found = candidates[0];
        }

        cachedInventory = found;
        return cachedInventory;
    }

    private IPlayerInput ResolveInputProvider()
    {
        if (inputSource != null)
        {
            IPlayerInput direct = inputSource as IPlayerInput;
            if (direct != null)
                return direct;
        }

        MonoBehaviour provider = FindInputProviderOnObject(transform);
        if (provider != null)
        {
            inputSource = provider;
            return provider as IPlayerInput;
        }

        return null;
    }

    private static MonoBehaviour FindInputProviderOnObject(Transform targetTransform)
    {
        Transform current = targetTransform;
        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                if (behaviour is IPlayerInput)
                    return behaviour;
            }

            current = current.parent;
        }

        return null;
    }

    private static PickupType? ResolveInventorySlot(IGrabbable grabbable)
    {
        if (grabbable is SecurityBadgePickup)
            return PickupType.SecurityBadge;

        if (grabbable is BatteryPickup)
            return PickupType.Battery;

        return null;
    }

    private static bool IsInventoryOnlyPickup(IGrabbable grabbable)
    {
        return grabbable is SecurityBadgePickup || grabbable is BatteryPickup;
    }

    private static bool IsInventorySlotAvailable(Inventory inventory, PickupType slot, IGrabbable candidate)
    {
        if (inventory == null)
            return true;

        var existing = inventory.GetItem(slot);
        if (existing == null)
            return true;

        var existingObject = existing as UnityEngine.Object;
        if (existingObject == null)
        {
            inventory.RemoveItem(slot);
            return true;
        }

        return ReferenceEquals(existing, candidate);
    }

    private Transform ResolveGrabParent(Transform handTransform, IGrabbable candidate)
    {
        if (!IsInventoryOnlyPickup(candidate))
            return handTransform;

        PlayerMovementController player = null;
        if (handTransform != null)
            player = handTransform.GetComponentInParent<PlayerMovementController>();
        else
            player = GetComponentInParent<PlayerMovementController>();

        if (player != null && player.BodyReference != null)
            return player.BodyReference.transform;

        return handTransform != null ? handTransform.root : transform;
    }
}
