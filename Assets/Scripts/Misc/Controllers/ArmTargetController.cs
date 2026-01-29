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
public class ArmTargetController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bodyReference;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Transform leftArmSolverTarget;
    [SerializeField] private Transform rightArmSolverTarget;
    [SerializeField] private Behaviour leftArmIkSolver;
    [SerializeField] private Behaviour rightArmIkSolver;

    [Header("Motion")]
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private float returnSpeed = 10f;
    [SerializeField] private float rotationReturnSpeed = 720f;
    [SerializeField] private float sideSwitchThreshold = 0.1f;

[Header("Controllers")]
[SerializeField] private CowboyGrabController grabController;
[SerializeField] private SimpleAttackController attackController;
[SerializeField] private bool allowAttackAim = true;
[Header("Energy")]
[SerializeField] private PlayerBrain playerBrain;
[SerializeField] private RobotStateController stateController;
[Header("AI Attack Control")]
[SerializeField] private bool useExternalAttackInput;
private Coroutine externalAttackPulseRoutine;

    private bool interactHeld;
    private bool interactPressedThisFrame;
    private bool interactReleasedThisFrame;
    private bool attackHeld;
    private bool attackInputHeld;
private bool attackSuppressedUntilRelease;
private bool preferRightArm = true;
private CowboyArmSide? attackActiveArm;
private bool attackEnergySpentThisPress;
    private bool externalAttackInput;

    private Vector3 leftRestLocalPosition;
    private Vector3 rightRestLocalPosition;
    private Quaternion leftRestLocalRotation;
    private Quaternion rightRestLocalRotation;
    private bool leftRestCaptured;
    private bool rightRestCaptured;
    private bool leftSolverDefaultEnabled = true;
    private bool rightSolverDefaultEnabled = true;
    private bool solverDefaultsCaptured;
    private readonly Dictionary<Behaviour, Action<bool>> solverFlipSetters = new Dictionary<Behaviour, Action<bool>>();

    private void Awake()
    {
        CacheControllers();
    }

    private void OnEnable()
    {
        CacheControllers();
        CacheRestPose();
        attackController?.DeactivateAll();
        SubscribeToAttackEvents();
    }

    private void OnDisable()
    {
        interactHeld = false;
        interactPressedThisFrame = false;
        interactReleasedThisFrame = false;
        attackHeld = false;
        attackInputHeld = false;
        attackSuppressedUntilRelease = false;
        attackActiveArm = null;
        attackEnergySpentThisPress = false;
        attackController?.DeactivateAll();
        grabController?.ReleaseAllImmediate();
        UnsubscribeFromAttackEvents();
    }

    private void Update()
    {
        bool currentlyHeld = IsRightMouseHeld();
        bool attackInput = useExternalAttackInput ? externalAttackInput : IsLeftMouseHeld();

        attackInputHeld = attackInput;
        if (!attackInputHeld)
        {
            attackSuppressedUntilRelease = false;
            attackEnergySpentThisPress = false;
        }

        attackHeld = allowAttackAim && attackInputHeld && !attackSuppressedUntilRelease;

        interactPressedThisFrame = !interactHeld && currentlyHeld;
        interactReleasedThisFrame = interactHeld && !currentlyHeld;
        interactHeld = currentlyHeld;

        HandleGrabInput();
        HandleAttackInput();
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

    private void CacheControllers()
    {
        if (grabController == null)
        {
            grabController = GetComponent<CowboyGrabController>();
        }

        if (grabController == null)
        {
            grabController = GetComponentInParent<CowboyGrabController>();
        }

        if (attackController == null)
        {
            attackController = GetComponent<SimpleAttackController>();
        }

        if (attackController == null)
        {
            attackController = GetComponentInParent<SimpleAttackController>();
        }

        if (playerBrain == null)
        {
            playerBrain = GetComponent<PlayerBrain>();
        }

        if (playerBrain == null)
        {
            playerBrain = GetComponentInParent<PlayerBrain>();
        }

        if (stateController == null)
        {
            stateController = GetComponent<RobotStateController>();
        }

        if (stateController == null)
        {
            stateController = GetComponentInParent<RobotStateController>();
        }
    }

    private void SubscribeToAttackEvents()
    {
        UnsubscribeFromAttackEvents();
        if (attackController != null)
        {
            attackController.AttackHit += OnAttackHit;
        }
    }

    private void UnsubscribeFromAttackEvents()
    {
        if (attackController != null)
        {
            attackController.AttackHit -= OnAttackHit;
        }
    }

    private CowboyArmSide DetermineArmForGrab()
    {
        CowboyArmSide? holdingArm = grabController?.GetHoldingArm();
        if (holdingArm.HasValue)
        {
            return holdingArm.Value;
        }

        if (bodyReference != null && targetTransform != null)
        {
            UpdatePreferredSide();
        }

        return ShouldUseRightArm() ? CowboyArmSide.Right : CowboyArmSide.Left;
    }

    private CowboyArmSide GetCurrentActiveArmSide()
    {
        CowboyArmSide? holdingArm = grabController?.GetHoldingArm();
        if (holdingArm.HasValue)
        {
            return holdingArm.Value;
        }

        if (bodyReference != null && targetTransform != null)
        {
            UpdatePreferredSide();
        }

        return ShouldUseRightArm() ? CowboyArmSide.Right : CowboyArmSide.Left;
    }

    private void HandleGrabInput()
    {
        if (grabController == null)
        {
            return;
        }

        CowboyArmSide armForGrab = DetermineArmForGrab();
        bool hasHeldObject = grabController.HasHeldObject();

        if (!hasHeldObject)
        {
            bool highlightActive = interactHeld || interactPressedThisFrame;
            grabController.SetHandAttractorState(armForGrab, highlightActive);
            grabController.SetHandAttractorState(GetOppositeArm(armForGrab), false);
        }

        if (interactPressedThisFrame && !hasHeldObject)
        {
            grabController.TryGrab(armForGrab);
            hasHeldObject = grabController.HasHeldObject();
        }

        CowboyArmSide? holdingArm = grabController.GetHoldingArm();
        if (!holdingArm.HasValue)
        {
            if (!interactHeld && !interactPressedThisFrame)
            {
                grabController.SetAllHandAttractorsInactive();
            }

            return;
        }

        if (interactHeld)
        {
            grabController.MaintainHold(holdingArm.Value);
        }

        if (interactReleasedThisFrame)
        {
            grabController.Release(holdingArm.Value);
        }
    }

    private void HandleAttackInput()
    {
        if (attackController == null)
        {
            return;
        }

        if (attackSuppressedUntilRelease)
        {
            if (attackActiveArm.HasValue)
            {
                attackController.SetArmAttackActive(attackActiveArm.Value, false);
                attackActiveArm = null;
            }

            return;
        }

        if (!attackInputHeld)
        {
            if (attackActiveArm.HasValue)
            {
                attackController.SetArmAttackActive(attackActiveArm.Value, false);
                attackActiveArm = null;
            }

            return;
        }

        CowboyArmSide desiredArm = GetCurrentActiveArmSide();

        if (!attackActiveArm.HasValue)
        {
            if (!attackEnergySpentThisPress && !TrySpendEnergyForAttack())
            {
                attackSuppressedUntilRelease = true;
                return;
            }

            attackController.SetArmAttackActive(desiredArm, true);
            attackActiveArm = desiredArm;
            attackEnergySpentThisPress = true;
            return;
        }

        if (attackActiveArm.Value == desiredArm)
        {
            return;
        }

        attackController.SetArmAttackActive(attackActiveArm.Value, false);
        attackController.SetArmAttackActive(desiredArm, true);
        attackActiveArm = desiredArm;
    }

    private void OnAttackHit(CowboyArmSide arm)
    {
        attackSuppressedUntilRelease = true;
        attackHeld = false;

        if (attackController != null)
        {
            attackController.SetArmAttackActive(arm, false);
        }

        if (attackActiveArm.HasValue)
        {
            attackActiveArm = null;
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

        bool useRightArm = grabController != null && grabController.HasHeldObject()
            ? grabController.GetHoldingArm() == CowboyArmSide.Right
            : ShouldUseRightArm();
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

        bool useRightArm = grabController != null && grabController.HasHeldObject()
            ? grabController.GetHoldingArm() == CowboyArmSide.Right
            : ShouldUseRightArm();

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
        return preferRightArm;
    }

    private static CowboyArmSide GetOppositeArm(CowboyArmSide arm)
    {
        return arm == CowboyArmSide.Right ? CowboyArmSide.Left : CowboyArmSide.Right;
    }

    private bool TrySpendEnergyForAttack()
    {
        if (stateController == null || stateController.CurrentState != RobotState.Alive)
        {
            return false;
        }

        if (stateController.Stats == null)
        {
            return false;
        }

        float energyCost = stateController.Stats.AttackEnergyCost;

        if (energyCost <= 0f)
        {
            return false;
        }

        if (playerBrain != null)
            return playerBrain.TrySpendEnergy(EnergyAction.Attack, 0f, energyCost);

        return stateController.PerformAttackByEnergy(energyCost);
    }

    /// <summary>
    /// Allows AI/Brain to drive attack input instead of player mouse.
    /// </summary>
    public void SetExternalAttackInput(bool active)
    {
        externalAttackInput = active;
    }

    /// <summary>
    /// Triggers a short attack input pulse for AI-driven attacks.
    /// </summary>
    public void TriggerExternalAttackPulse()
    {
        useExternalAttackInput = true;
        if (externalAttackPulseRoutine != null)
            StopCoroutine(externalAttackPulseRoutine);
        externalAttackPulseRoutine = StartCoroutine(ExternalAttackPulse());
    }

    private System.Collections.IEnumerator ExternalAttackPulse()
    {
        externalAttackInput = true;
        yield return null; // one frame
        externalAttackInput = false;
        externalAttackPulseRoutine = null;
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
