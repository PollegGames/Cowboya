using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Moves the Cowboy master's arm solver targets toward a cursor/target prefab while the interact button (right click) is held.
/// The arm that is selected is based on the horizontal offset of the target relative to the body reference and hands return to
/// their default pose as soon as the button is released.
/// </summary>
public class CowboyArmTargetController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bodyReference;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Transform leftArmSolverTarget;
    [SerializeField] private Transform rightArmSolverTarget;
    [SerializeField] private Behaviour leftArmIkSolver;
    [SerializeField] private Behaviour rightArmIkSolver;
    [SerializeField] private bool swapArms;

    [Header("Motion")]
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private float returnSpeed = 10f;
    [SerializeField] private float rotationReturnSpeed = 720f;
    [SerializeField] private float sideSwitchThreshold = 0.1f;

    [Header("Grab Settings")]
    [SerializeField] private float grabRadius = 0.4f;
    [SerializeField] private LayerMask grabbableLayers = ~0;
    [SerializeField] private float throwStrength = 5f;
    [SerializeField] private Transform leftHandGrabAnchor;
    [SerializeField] private Transform rightHandGrabAnchor;
    [SerializeField] private Transform leftHandHoldParent;
    [SerializeField] private Transform rightHandHoldParent;
    [SerializeField] private Inventory inventory;
    [SerializeField] private bool allowAttackAim = true;

    private bool interactHeld;
    private bool interactPressedThisFrame;
    private bool interactReleasedThisFrame;
    private bool attackHeld;
    private bool preferRightArm = true;
    private IGrabbable heldObject;
    private bool holdingRightHand;

    private Vector3 leftRestLocalPosition;
    private Vector3 rightRestLocalPosition;
    private Quaternion leftRestLocalRotation;
    private Quaternion rightRestLocalRotation;
    private bool leftRestCaptured;
    private bool rightRestCaptured;
    private bool leftSolverDefaultEnabled = true;
    private bool rightSolverDefaultEnabled = true;
    private bool solverDefaultsCaptured;
    private Transform activeArm;
    private readonly Dictionary<Behaviour, Action<bool>> solverFlipSetters = new Dictionary<Behaviour, Action<bool>>();

    private void Awake()
    {
        CacheGrabAnchors();
        CacheInventory();
    }

    private void OnEnable()
    {
        CacheGrabAnchors();
        CacheInventory();
        CacheRestPose();
    }

    private void OnDisable()
    {
        interactHeld = false;
        interactPressedThisFrame = false;
        interactReleasedThisFrame = false;
        attackHeld = false;
        ReleaseHeldObject(0f);
    }

    private void Update()
    {
        bool currentlyHeld = IsRightMouseHeld();
        attackHeld = allowAttackAim && IsLeftMouseHeld();
        interactPressedThisFrame = !interactHeld && currentlyHeld;
        interactReleasedThisFrame = interactHeld && !currentlyHeld;
        interactHeld = currentlyHeld;

        UpdateGrabState();
    }

    private void CacheRestPose()
    {
        if (leftArmSolverTarget != null && !leftRestCaptured)
        {
            leftRestLocalPosition = leftArmSolverTarget.localPosition;
            leftRestLocalRotation = leftArmSolverTarget.localRotation;
            leftRestCaptured = true;
        }

        if (rightArmSolverTarget != null && !rightRestCaptured)
        {
            rightRestLocalPosition = rightArmSolverTarget.localPosition;
            rightRestLocalRotation = rightArmSolverTarget.localRotation;
            rightRestCaptured = true;
        }

        CacheSolverDefaults();
    }

    private void UpdateGrabState()
    {
        if (interactPressedThisFrame && !HasHeldObject())
        {
            TryGrabActiveHand();
        }

        if (HasHeldObject() && interactHeld)
        {
            MaintainHold();
        }

        if (HasHeldObject() && interactReleasedThisFrame)
        {
            ReleaseHeldObject(throwStrength);
        }
    }

    private void TryGrabActiveHand()
    {
        bool useRightHand = DetermineRightHandForGrab();
        Transform anchor = GetGrabAnchor(useRightHand);
        if (anchor == null)
        {
            return;
        }

        IGrabbable candidate = DetectGrabbable(anchor.position);
        if (candidate == null)
        {
            return;
        }

        Inventory currentInventory = GetInventory();
        if (!candidate.CanBeGrabbed(currentInventory))
        {
            return;
        }

        PickupType? slot = ResolvePickupSlot(candidate);
        if (slot.HasValue && currentInventory != null && currentInventory.HasItem(slot.Value))
        {
            return;
        }

        Transform parent = GetHoldParent(useRightHand) ?? anchor;
        candidate.OnGrab(parent);

        if (currentInventory != null && slot.HasValue)
        {
            currentInventory.SetItem(slot.Value, candidate);
        }

        if (IsInventoryOnlyPickup(candidate))
        {
            return;
        }

        heldObject = candidate;
        holdingRightHand = useRightHand;

        if (interactHeld)
        {
            MaintainHold();
        }
    }

    private bool DetermineRightHandForGrab()
    {
        if (HasHeldObject())
        {
            return holdingRightHand;
        }

        if (bodyReference != null && targetTransform != null)
        {
            UpdatePreferredSide();
        }

        return ShouldUseRightArm();
    }

    private void MaintainHold()
    {
        if (!HasHeldObject() || !interactHeld)
        {
            return;
        }

        Transform anchor = GetGrabAnchor(holdingRightHand);
        if (anchor == null)
        {
            return;
        }

        heldObject.OnAttract(anchor.position);
    }

    private void ReleaseHeldObject(float strength)
    {
        if (!HasHeldObject())
        {
            return;
        }

        Transform reference = GetHoldParent(holdingRightHand) ?? GetGrabAnchor(holdingRightHand);
        Vector2 throwForce = reference != null ? (Vector2)reference.right * strength : Vector2.zero;

        RemoveInventoryEntry(heldObject);
        heldObject.OnRelease(throwForce);
        heldObject = null;
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

    private Transform GetGrabAnchor(bool forRightHand)
    {
        return forRightHand
            ? (rightHandGrabAnchor != null ? rightHandGrabAnchor : rightArmSolverTarget)
            : (leftHandGrabAnchor != null ? leftHandGrabAnchor : leftArmSolverTarget);
    }

    private Transform GetHoldParent(bool forRightHand)
    {
        return forRightHand
            ? (rightHandHoldParent != null ? rightHandHoldParent : rightArmSolverTarget)
            : (leftHandHoldParent != null ? leftHandHoldParent : leftArmSolverTarget);
    }

    private bool HasHeldObject()
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

    private void CacheGrabAnchors()
    {
        if (leftHandGrabAnchor == null)
        {
            leftHandGrabAnchor = leftArmSolverTarget;
        }

        if (rightHandGrabAnchor == null)
        {
            rightHandGrabAnchor = rightArmSolverTarget;
        }

        if (leftHandHoldParent == null)
        {
            leftHandHoldParent = leftArmSolverTarget;
        }

        if (rightHandHoldParent == null)
        {
            rightHandHoldParent = rightArmSolverTarget;
        }
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

    private void LateUpdate()
    {
        bool hasTargets = bodyReference != null && targetTransform != null;
        bool canDrive = hasTargets && (interactHeld || attackHeld);

        if (hasTargets)
        {
            UpdatePreferredSide();
            ApplySolverFlip(preferRightArm);
        }

        ApplySolverStates(canDrive);

        if (!hasTargets)
        {
            return;
        }

        if (!canDrive)
        {
            ReturnToRest(leftArmSolverTarget, leftRestLocalPosition, leftRestLocalRotation, leftRestCaptured);
            ReturnToRest(rightArmSolverTarget, rightRestLocalPosition, rightRestLocalRotation, rightRestCaptured);
            return;
        }

        Vector3 destination = targetTransform.position;

        bool useRightArm = HasHeldObject() ? holdingRightHand : ShouldUseRightArm();
        Transform activeArm = useRightArm ? rightArmSolverTarget : leftArmSolverTarget;
        Transform inactiveArm = useRightArm ? leftArmSolverTarget : rightArmSolverTarget;

        if (activeArm == null && inactiveArm != null)
        {
            // Fallback to whichever arm exists, but still rest the non-active one.
            activeArm = inactiveArm;
            inactiveArm = null;
        }

        if (activeArm != null)
        {
            FollowTarget(activeArm, destination);
        }

        if (inactiveArm != null)
        {
            ReturnToRest(inactiveArm,
                inactiveArm == leftArmSolverTarget ? leftRestLocalPosition : rightRestLocalPosition,
                inactiveArm == leftArmSolverTarget ? leftRestLocalRotation : rightRestLocalRotation,
                inactiveArm == leftArmSolverTarget ? leftRestCaptured : rightRestCaptured);
        }
    }

    private void UpdatePreferredSide()
    {
        float delta = targetTransform.position.x - bodyReference.position.x;
        if (delta > sideSwitchThreshold)
        {
            preferRightArm = true;
        }
        else if (delta < -sideSwitchThreshold)
        {
            preferRightArm = false;
        }
    }

    private void FollowTarget(Transform arm, Vector3 destination)
    {
        arm.position = Vector3.MoveTowards(arm.position, destination, followSpeed * Time.deltaTime);
    }

    private void ReturnToRest(Transform arm, Vector3 restLocalPosition, Quaternion restLocalRotation, bool hasRest)
    {
        if (arm == null || !hasRest)
        {
            return;
        }

        arm.localPosition = Vector3.MoveTowards(arm.localPosition, restLocalPosition, returnSpeed * Time.deltaTime);
        arm.localRotation = Quaternion.RotateTowards(arm.localRotation, restLocalRotation, rotationReturnSpeed * Time.deltaTime);
    }

    private bool IsRightMouseHeld()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.rightButton.isPressed;
        }

        return Input.GetMouseButton(1);
    }

    private bool IsLeftMouseHeld()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }

        return Input.GetMouseButton(0);
    }

    private void CacheSolverDefaults()
    {
        if (solverDefaultsCaptured)
        {
            return;
        }

        if (leftArmIkSolver != null)
        {
            leftSolverDefaultEnabled = leftArmIkSolver.enabled;
        }

        if (rightArmIkSolver != null)
        {
            rightSolverDefaultEnabled = rightArmIkSolver.enabled;
        }

        solverDefaultsCaptured = true;
    }

    private void ApplySolverStates(bool allowActiveSolver)
    {
        if (leftArmIkSolver == null && rightArmIkSolver == null)
        {
            return;
        }

        if (!allowActiveSolver)
        {
            SetSolverEnabled(leftArmIkSolver, leftSolverDefaultEnabled);
            SetSolverEnabled(rightArmIkSolver, rightSolverDefaultEnabled);
            return;
        }

        bool useRightArm = HasHeldObject() ? holdingRightHand : ShouldUseRightArm();

        if (useRightArm)
        {
            SetSolverEnabled(rightArmIkSolver, rightSolverDefaultEnabled);
            SetSolverEnabled(leftArmIkSolver, false);
        }
        else
        {
            SetSolverEnabled(leftArmIkSolver, leftSolverDefaultEnabled);
            SetSolverEnabled(rightArmIkSolver, false);
        }
    }

    private static void SetSolverEnabled(Behaviour solver, bool enabled)
    {
        if (solver == null)
        {
            return;
        }

        solver.enabled = enabled;
    }

    private bool ShouldUseRightArm()
    {
        bool desired = !preferRightArm; // target on left -> use right arm
        return swapArms ? !desired : desired;
    }

    private void ApplySolverFlip(bool targetIsRightSide)
    {
        SetSolverFlip(leftArmIkSolver, targetIsRightSide);
        SetSolverFlip(rightArmIkSolver, false);
    }

    private void SetSolverFlip(Behaviour solver, bool flipValue)
    {
        if (solver == null)
        {
            return;
        }

        if (!solverFlipSetters.TryGetValue(solver, out var setter) || setter == null)
        {
            setter = CreateFlipSetter(solver);
            solverFlipSetters[solver] = setter;
        }

        setter?.Invoke(flipValue);
    }

    private Action<bool> CreateFlipSetter(Behaviour solver)
    {
        if (solver == null)
        {
            return null;
        }

        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        PropertyInfo property = solver.GetType().GetProperty("flip", Flags);
        if (property != null && property.PropertyType == typeof(bool) && property.GetSetMethod(true) != null)
        {
            return value => property.SetValue(solver, value);
        }

        FieldInfo field = solver.GetType().GetField("m_Flip", Flags);
        if (field != null && field.FieldType == typeof(bool))
        {
            return value => field.SetValue(solver, value);
        }

        return null;
    }
}
