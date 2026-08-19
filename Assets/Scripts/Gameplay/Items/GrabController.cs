using System;
using UnityEngine;

[DisallowMultipleComponent]
public class CowboyGrabController : MonoBehaviour
{
    private static readonly PickupType[] InventoryPickupTypes =
        (PickupType[])Enum.GetValues(typeof(PickupType));

    private sealed class ArmGrabState
    {
        public IGrabbable HeldObject;
    }

    private struct GrabDetection
    {
        public IGrabbable Grabbable;
        public Collider2D SourceCollider;
    }

    [Header("Grab Settings")]
    [SerializeField] private float grabRadius = 0.4f;
    [SerializeField, Min(0f), Tooltip("Maximum distance for stealing a badge that is attached to a living enemy.")]
    private float livingEnemyBadgeGrabRadius = 0.25f;
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
    [SerializeField] private RobotStateController robotBehaviour;
    [SerializeField] private EnergyBot energyBot;
    [SerializeField] private PlayerBrain playerBrain;

    private readonly ArmGrabState leftGrab = new ArmGrabState();
    private readonly ArmGrabState rightGrab = new ArmGrabState();

    private void Awake()
    {
        CacheRobotSystems();
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
        CacheRobotSystems();
    }

    public bool TryGrab(CowboyArmSide arm)
    {
        if (HasHeldObject(arm))
        {
            return false;
        }

        Transform anchor = GetGrabAnchor(arm);
        if (anchor == null)
        {
            return false;
        }

        Inventory currentInventory = GetInventory();
        GrabDetection detection = DetectGrabbable(anchor.position, currentInventory);
        IGrabbable candidate = detection.Grabbable;
        if (candidate == null)
        {
            return false;
        }

        if (candidate is IGrabContextReceiver contextReceiver)
        {
            contextReceiver.SetGrabContext(detection.SourceCollider, anchor.position);
        }

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

        GetGrabState(arm).HeldObject = candidate;
        SetHandAttractorState(arm, true);
        MaintainHold(arm);

        return true;
    }

    public void MaintainHold(CowboyArmSide arm)
    {
        if (!HasHeldObject(arm))
        {
            return;
        }

        IGrabbable heldObject = GetGrabState(arm).HeldObject;
        if (heldObject == null)
        {
            return;
        }

        CowboyArmSide otherArm = GetOppositeArm(arm);
        if (HasHeldObject(otherArm) && GetGrabState(otherArm).HeldObject == heldObject)
        {
            Vector2 midpoint;
            if (TryGetTwoHandMidpoint(out midpoint))
            {
                heldObject.OnAttract(midpoint);
            }
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
        IGrabbable leftObject = HasHeldObject(CowboyArmSide.Left) ? leftGrab.HeldObject : null;
        IGrabbable rightObject = HasHeldObject(CowboyArmSide.Right) ? rightGrab.HeldObject : null;

        if (leftObject != null)
        {
            leftGrab.HeldObject = null;
            leftObject.OnRelease(Vector2.zero);
        }

        if (rightObject != null && rightObject != leftObject)
        {
            rightObject.OnRelease(Vector2.zero);
        }

        rightGrab.HeldObject = null;
        SetHandAttractorState(CowboyArmSide.Left, false);
        SetHandAttractorState(CowboyArmSide.Right, false);
    }

    /// <summary>
    /// Atomically relinquishes this controller's ownership of one precise item without
    /// invoking the item's normal release behaviour.
    /// </summary>
    public bool TryDetachHeldObject(IGrabbable item)
    {
        if (item == null)
        {
            return false;
        }

        if (item is UnityEngine.Object unityItem && unityItem == null)
        {
            return false;
        }

        IGrabControllerDetachReceiver detachReceiver =
            item as IGrabControllerDetachReceiver;
        if (detachReceiver == null)
        {
            return false;
        }

        bool detachLeft = ReferenceEquals(leftGrab.HeldObject, item);
        bool detachRight = ReferenceEquals(rightGrab.HeldObject, item);
        Inventory currentInventory = GetInventory();
        bool detachInventory = InventoryContainsReference(currentInventory, item);
        if (!detachLeft && !detachRight && !detachInventory)
        {
            return false;
        }

        if (detachLeft)
        {
            leftGrab.HeldObject = null;
        }

        if (detachRight)
        {
            rightGrab.HeldObject = null;
        }

        RemoveInventoryReferences(currentInventory, item);

        if (detachLeft)
        {
            SetHandAttractorState(CowboyArmSide.Left, false);
        }

        if (detachRight)
        {
            SetHandAttractorState(CowboyArmSide.Right, false);
        }

        detachReceiver.OnDetachedFromGrabController();

        return true;
    }

    public bool HasHeldObject()
    {
        return HasHeldObject(CowboyArmSide.Left) || HasHeldObject(CowboyArmSide.Right);
    }

    public bool HasHeldObject(CowboyArmSide arm)
    {
        ArmGrabState state = GetGrabState(arm);
        if (state.HeldObject == null)
        {
            return false;
        }

        UnityEngine.Object unityObject = state.HeldObject as UnityEngine.Object;
        if (unityObject == null)
        {
            state.HeldObject = null;
            return false;
        }

        return true;
    }

    public IGrabbable GetHeldObject(CowboyArmSide arm)
    {
        return HasHeldObject(arm) ? GetGrabState(arm).HeldObject : null;
    }

    public CowboyArmSide? GetHoldingArm()
    {
        if (HasHeldObject(CowboyArmSide.Left))
        {
            return CowboyArmSide.Left;
        }

        return HasHeldObject(CowboyArmSide.Right) ? CowboyArmSide.Right : (CowboyArmSide?)null;
    }

    private void ReleaseInternal(CowboyArmSide arm, float strength)
    {
        if (!HasHeldObject(arm))
        {
            return;
        }

        ArmGrabState state = GetGrabState(arm);
        IGrabbable releasedObject = state.HeldObject;
        state.HeldObject = null;
        SetHandAttractorState(arm, false);

        CowboyArmSide otherArm = GetOppositeArm(arm);
        if (HasHeldObject(otherArm) && GetGrabState(otherArm).HeldObject == releasedObject)
        {
            Transform remainingParent = GetHoldParent(otherArm) ?? GetGrabAnchor(otherArm);
            if (remainingParent != null)
            {
                releasedObject.OnGrab(remainingParent);
                releasedObject.OnAttract(remainingParent.position);
            }
            return;
        }

        Transform reference = GetHoldParent(arm) ?? GetGrabAnchor(arm);
        Vector2 throwForce = reference != null ? (Vector2)reference.right * strength : Vector2.zero;

        RemoveInventoryEntry(releasedObject);
        releasedObject.OnRelease(throwForce);
    }

    private bool TryGetTwoHandMidpoint(out Vector2 midpoint)
    {
        Transform leftAnchor = GetGrabAnchor(CowboyArmSide.Left);
        Transform rightAnchor = GetGrabAnchor(CowboyArmSide.Right);
        if (leftAnchor == null || rightAnchor == null)
        {
            midpoint = Vector2.zero;
            return false;
        }

        midpoint = ((Vector2)leftAnchor.position + (Vector2)rightAnchor.position) * 0.5f;
        return true;
    }

    private GrabDetection DetectGrabbable(Vector3 origin, Inventory currentInventory)
    {
        int mask = grabbableLayers.value;
        if (mask == 0)
        {
            mask = ~0;
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin, grabRadius, mask);
        IGrabbable closestBadge = null;
        Collider2D closestBadgeCollider = null;
        float closestBadgeDistance = float.MaxValue;
        IGrabbable closestOther = null;
        Collider2D closestOtherCollider = null;
        float closestOtherDistance = float.MaxValue;

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

            Vector2 closestPoint = collider.ClosestPoint(origin);
            float distance = Vector2.Distance(origin, closestPoint);
            if (grabbable is SecurityBadgePickup badge)
            {
                if (!badge.CanBeGrabbed(currentInventory))
                    continue;
                if (badge.RequiresCloseRangeWhileAttachedToEnemy()
                    && distance > livingEnemyBadgeGrabRadius)
                {
                    continue;
                }
                if (distance < closestBadgeDistance)
                {
                    closestBadgeDistance = distance;
                    closestBadge = grabbable;
                    closestBadgeCollider = collider;
                }
                continue;
            }

            if (!grabbable.CanBeGrabbed(currentInventory))
                continue;

            if (distance < closestOtherDistance)
            {
                closestOtherDistance = distance;
                closestOther = grabbable;
                closestOtherCollider = collider;
            }
        }

        return new GrabDetection
        {
            Grabbable = closestBadge ?? closestOther,
            SourceCollider = closestBadge != null ? closestBadgeCollider : closestOtherCollider
        };
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

    private void CacheRobotSystems()
    {
        if (robotBehaviour == null)
            robotBehaviour = GetComponent<RobotStateController>();
        if (energyBot == null)
            energyBot = GetComponent<EnergyBot>();
        if (playerBrain == null)
            playerBrain = GetComponent<PlayerBrain>();
    }

    private ArmGrabState GetGrabState(CowboyArmSide arm)
    {
        return arm == CowboyArmSide.Right ? rightGrab : leftGrab;
    }

    private static CowboyArmSide GetOppositeArm(CowboyArmSide arm)
    {
        return arm == CowboyArmSide.Right ? CowboyArmSide.Left : CowboyArmSide.Right;
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

    private static bool InventoryContainsReference(Inventory currentInventory, IGrabbable item)
    {
        if (currentInventory == null)
        {
            return false;
        }

        for (int i = 0; i < InventoryPickupTypes.Length; i++)
        {
            if (ReferenceEquals(currentInventory.GetItem(InventoryPickupTypes[i]), item))
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveInventoryReferences(Inventory currentInventory, IGrabbable item)
    {
        if (currentInventory == null)
        {
            return;
        }

        for (int i = 0; i < InventoryPickupTypes.Length; i++)
        {
            PickupType pickupType = InventoryPickupTypes[i];
            if (ReferenceEquals(currentInventory.GetItem(pickupType), item))
            {
                currentInventory.RemoveItem(pickupType);
            }
        }
    }
}
