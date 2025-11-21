using UnityEngine;

[DisallowMultipleComponent]
public class CowboyGrabController : MonoBehaviour
{
    [Header("Grab Settings")]
    [SerializeField] private float grabRadius = 0.4f;
    [SerializeField] private LayerMask grabbableLayers = ~0;
    [SerializeField] private float throwStrength = 5f;
    [SerializeField] private Transform leftHandGrabAnchor;
    [SerializeField] private Transform rightHandGrabAnchor;
    [SerializeField] private Transform leftHandHoldParent;
    [SerializeField] private Transform rightHandHoldParent;
    [SerializeField] private Inventory inventory;
    [SerializeField] private GrabHandAttractor leftHandAttractor;
    [SerializeField] private GrabHandAttractor rightHandAttractor;
    [SerializeField] private ToggleBox leftHandToggleBox;
    [SerializeField] private ToggleBox rightHandToggleBox;

    private IGrabbable heldObject;
    private CowboyArmSide holdingArm;

    private void Awake()
    {
        CacheGrabAnchors();
        CacheInventory();
        CacheHandAttractors();
        CacheToggleBoxes();
    }

    private void OnEnable()
    {
        CacheGrabAnchors();
        CacheInventory();
        CacheHandAttractors();
        CacheToggleBoxes();
    }

    public bool TryGrab(CowboyArmSide arm)
    {
        Transform anchor = GetGrabAnchor(arm);
        if (anchor == null)
        {
            return false;
        }

        IGrabbable candidate = DetectGrabbable(anchor.position);
        if (candidate == null)
        {
            return false;
        }

        Inventory currentInventory = GetInventory();
        if (!candidate.CanBeGrabbed(currentInventory))
        {
            return false;
        }

        PickupType? slot = ResolvePickupSlot(candidate);
        if (slot.HasValue && currentInventory != null && currentInventory.HasItem(slot.Value))
        {
            return false;
        }

        Transform parent = GetHoldParent(arm) ?? anchor;
        candidate.OnGrab(parent);

        if (currentInventory != null && slot.HasValue)
        {
            currentInventory.SetItem(slot.Value, candidate);
        }

        if (IsInventoryOnlyPickup(candidate))
        {
            return true;
        }

        heldObject = candidate;
        holdingArm = arm;
        SetHandAttractorState(arm, true);

        return true;
    }

    public void MaintainHold(CowboyArmSide arm)
    {
        if (!HasHeldObject(arm))
        {
            return;
        }

        Transform anchor = GetGrabAnchor(arm);
        if (anchor == null)
        {
            return;
        }

        heldObject.OnAttract(anchor.position);
    }

    public void Release(CowboyArmSide arm)
    {
        ReleaseInternal(arm, throwStrength);
    }

    public void Release(CowboyArmSide arm, float overrideStrength)
    {
        ReleaseInternal(arm, overrideStrength);
    }

    public void ReleaseAllImmediate()
    {
        if (!HasHeldObject())
        {
            return;
        }

        ReleaseInternal(holdingArm, 0f);
    }

    public bool HasHeldObject()
    {
        if (heldObject == null)
        {
            return false;
        }

        UnityEngine.Object unityObject = heldObject as UnityEngine.Object;
        if (unityObject == null)
        {
            heldObject = null;
            return false;
        }

        return true;
    }

    public bool HasHeldObject(CowboyArmSide arm)
    {
        return HasHeldObject() && holdingArm == arm;
    }

    public CowboyArmSide? GetHoldingArm()
    {
        return HasHeldObject() ? holdingArm : (CowboyArmSide?)null;
    }

    private void ReleaseInternal(CowboyArmSide arm, float strength)
    {
        if (!HasHeldObject(arm))
        {
            return;
        }

        Transform reference = GetHoldParent(arm) ?? GetGrabAnchor(arm);
        Vector2 throwForce = reference != null ? (Vector2)reference.right * strength : Vector2.zero;

        RemoveInventoryEntry(heldObject);
        heldObject.OnRelease(throwForce);
        heldObject = null;
        SetHandAttractorState(arm, false);
    }

    private IGrabbable DetectGrabbable(Vector3 origin)
    {
        int mask = grabbableLayers.value;
        if (mask == 0)
        {
            mask = ~0;
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin, grabRadius, mask);
        IGrabbable closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider2D collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            IGrabbable grabbable = collider.GetComponent<IGrabbable>();
            if (grabbable == null)
            {
                grabbable = collider.GetComponentInParent<IGrabbable>();
            }

            if (grabbable == null)
            {
                continue;
            }

            MonoBehaviour behaviour = grabbable as MonoBehaviour;
            if (behaviour == null)
            {
                continue;
            }

            float distance = Vector2.Distance(origin, behaviour.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = grabbable;
            }
        }

        return closest;
    }

    private static PickupType? ResolvePickupSlot(IGrabbable grabbable)
    {
        if (grabbable is SecurityBadgePickup)
        {
            return PickupType.SecurityBadge;
        }

        if (grabbable is BatteryPickup)
        {
            return PickupType.Battery;
        }

        return null;
    }

    private static bool IsInventoryOnlyPickup(IGrabbable grabbable)
    {
        return grabbable is SecurityBadgePickup || grabbable is BatteryPickup;
    }

    private Transform GetGrabAnchor(CowboyArmSide arm)
    {
        return arm == CowboyArmSide.Right
            ? (rightHandGrabAnchor != null ? rightHandGrabAnchor : rightHandHoldParent)
            : (leftHandGrabAnchor != null ? leftHandGrabAnchor : leftHandHoldParent);
    }

    private Transform GetHoldParent(CowboyArmSide arm)
    {
        return arm == CowboyArmSide.Right
            ? (rightHandHoldParent != null ? rightHandHoldParent : rightHandGrabAnchor)
            : (leftHandHoldParent != null ? leftHandHoldParent : leftHandGrabAnchor);
    }

    private void CacheGrabAnchors()
    {
        if (leftHandGrabAnchor == null)
        {
            leftHandGrabAnchor = leftHandHoldParent;
        }

        if (rightHandGrabAnchor == null)
        {
            rightHandGrabAnchor = rightHandHoldParent;
        }

        if (leftHandHoldParent == null)
        {
            leftHandHoldParent = leftHandGrabAnchor;
        }

        if (rightHandHoldParent == null)
        {
            rightHandHoldParent = rightHandGrabAnchor;
        }
    }

    public void SetHandAttractorState(CowboyArmSide arm, bool active)
    {
        GrabHandAttractor attractor = GetHandAttractor(arm);
        if (attractor == null)
        {
            SetToggleBoxState(arm, false);
            return;
        }

        if (active)
        {
            attractor.Activate();
        }
        else
        {
            attractor.Deactivate();
        }

        if (HasHeldObject(arm))
        {
            SetToggleBoxState(arm, false);
            return;
        }

        SetToggleBoxState(arm, active);
    }

    public void SetAllHandAttractorsInactive()
    {
        leftHandAttractor?.Deactivate();
        rightHandAttractor?.Deactivate();
        SetAllToggleBoxesInactive();
    }

    private GrabHandAttractor GetHandAttractor(CowboyArmSide arm)
    {
        CacheHandAttractors();
        return arm == CowboyArmSide.Right ? rightHandAttractor : leftHandAttractor;
    }

    private void CacheHandAttractors()
    {
        if (leftHandAttractor == null)
        {
            leftHandAttractor = ResolveHandAttractor(leftHandGrabAnchor) ?? ResolveHandAttractor(leftHandHoldParent);
        }

        if (rightHandAttractor == null)
        {
            rightHandAttractor = ResolveHandAttractor(rightHandGrabAnchor) ?? ResolveHandAttractor(rightHandHoldParent);
        }
    }

    private static GrabHandAttractor ResolveHandAttractor(Transform reference)
    {
        if (reference == null)
        {
            return null;
        }

        GrabHandAttractor attractor = reference.GetComponent<GrabHandAttractor>();
        if (attractor != null)
        {
            return attractor;
        }

        return reference.GetComponentInChildren<GrabHandAttractor>(true);
    }

    private void CacheToggleBoxes()
    {
        if (leftHandToggleBox == null)
        {
            leftHandToggleBox = ResolveToggleBox(leftHandAttractor, leftHandGrabAnchor, leftHandHoldParent);
        }

        if (rightHandToggleBox == null)
        {
            rightHandToggleBox = ResolveToggleBox(rightHandAttractor, rightHandGrabAnchor, rightHandHoldParent);
        }
    }

    private ToggleBox GetToggleBox(CowboyArmSide arm)
    {
        CacheToggleBoxes();
        return arm == CowboyArmSide.Right ? rightHandToggleBox : leftHandToggleBox;
    }

    private static ToggleBox ResolveToggleBox(GrabHandAttractor attractor, Transform grabAnchor, Transform holdParent)
    {
        if (attractor != null)
        {
            ToggleBox attractorToggle = attractor.GetToggleBox();
            if (attractorToggle != null)
            {
                return attractorToggle;
            }
        }

        ToggleBox anchorToggle = FindToggleBox(grabAnchor);
        if (anchorToggle != null)
        {
            return anchorToggle;
        }

        return FindToggleBox(holdParent);
    }

    private static ToggleBox FindToggleBox(Transform reference)
    {
        if (reference == null)
        {
            return null;
        }

        ToggleBox direct = reference.GetComponent<ToggleBox>();
        if (direct != null)
        {
            return direct;
        }

        return reference.GetComponentInChildren<ToggleBox>(true);
    }

    private void SetToggleBoxState(CowboyArmSide arm, bool active)
    {
        ToggleBox toggleBox = GetToggleBox(arm);
        if (toggleBox == null)
        {
            return;
        }

        if (active)
        {
            toggleBox.Activate();
        }
        else
        {
            toggleBox.Deactivate();
        }
    }

    private void SetAllToggleBoxesInactive()
    {
        SetToggleBoxState(CowboyArmSide.Left, false);
        SetToggleBoxState(CowboyArmSide.Right, false);
    }

    private void CacheInventory()
    {
        if (inventory != null)
        {
            return;
        }

        inventory = GetComponentInParent<Inventory>();
        if (inventory == null)
        {
            inventory = GetComponent<Inventory>();
        }
    }

    private Inventory GetInventory()
    {
        if (inventory == null)
        {
            CacheInventory();
        }

        return inventory;
    }

    private void RemoveInventoryEntry(IGrabbable grabbable)
    {
        Inventory currentInventory = GetInventory();
        if (currentInventory == null)
        {
            return;
        }

        PickupType? slot = ResolvePickupSlot(grabbable);
        if (slot.HasValue)
        {
            currentInventory.RemoveItem(slot.Value);
        }
    }
}
