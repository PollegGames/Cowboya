using UnityEngine;

public class GrabSystem : MonoBehaviour
{
    public GrabHandAttractor leftHand;
    public GrabHandAttractor rightHand;
    public float throwStrength = 5f;

    [SerializeField] private MonoBehaviour inputSource;
    private IPlayerInput input;

    private IGrabbable leftHeld;
    private IGrabbable rightHeld;

    private void Awake()
    {
        input = inputSource as IPlayerInput;
        if (input == null)
        {
            Debug.LogError("GrabSystem: inputSource does not implement IPlayerInput");
        }
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
                Release(leftHand, ref leftHeld);
        }

        if (leftHeld != null && input.LeftGrabHeld)
            leftHeld.OnAttract(leftHand.transform.position);

        // RIGHT HAND (same pattern)
        if (input.RightGrabDown)
        {
            if (rightHeld == null)
                TryGrab(rightHand, ref rightHeld);
        }
        else if (input.RightGrabUp)
        {
            if (rightHeld != null)
                Release(rightHand, ref rightHeld);
        }

        if (rightHeld != null && input.RightGrabHeld)
            rightHeld.OnAttract(rightHand.transform.position);
    }

    /// <summary>
    /// Releases any held objects without applying a throw force.
    /// </summary>
    public void ClearHands()
    {
        if (leftHeld != null)
        {
            Release(leftHand, ref leftHeld, 0f);
        }

        if (rightHeld != null)
        {
            Release(rightHand, ref rightHeld, 0f);
        }

        var inventory = GetComponent<Inventory>();
        if (inventory != null)
        {
            inventory.DropAll();
        }

        leftHeld = null;
        rightHeld = null;
    }

    private void TryGrab(GrabHandAttractor hand, ref IGrabbable held)
    {
        if (hand == null || held != null) return;
        IGrabbable obj = hand.DetectGrabbable();
        var inventory = hand.GetComponentInParent<Inventory>();
        if (obj != null && obj.CanBeGrabbed(inventory))
        {
            PickupType? slot = null;

            if (obj is SecurityBadgePickup)
                slot = PickupType.SecurityBadge;
            else if (obj is BatteryPickup)
                slot = PickupType.Battery;

            if (inventory != null && slot.HasValue && inventory.HasItem(slot.Value))
                return;

            obj.OnGrab(hand.transform);

            if (inventory != null && slot.HasValue)
                inventory.SetItem(slot.Value, obj);

            // Badges or batteries attach to the player's body and should not remain in hand
            if (obj is SecurityBadgePickup || obj is BatteryPickup)
            {
                held = null;
            }
            else
            {
                held = obj;
            }
        }
    }

    private void Release(GrabHandAttractor hand, ref IGrabbable held)
    {
        Release(hand, ref held, throwStrength);
    }

    private void Release(GrabHandAttractor hand, ref IGrabbable held, float strength)
    {
        if (hand == null || held == null) return;

        var inventory = hand.GetComponentInParent<Inventory>();
        if (inventory != null)
        {
            if (held is SecurityBadgePickup)
                inventory.RemoveItem(PickupType.SecurityBadge);
            else if (held is BatteryPickup)
                inventory.RemoveItem(PickupType.Battery);
        }

        Vector2 throwForce = (Vector2)(hand.transform.right) * strength;
        held.OnRelease(throwForce);
        held = null;
    }
}
