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

    private bool interactHeld;
    private bool preferRightArm = true;

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

    private void OnEnable()
    {
        CacheRestPose();
    }

    private void OnDisable()
    {
        interactHeld = false;
    }

    private void Update()
    {
        interactHeld = IsRightMouseHeld();
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

    private void LateUpdate()
    {
        bool hasTargets = bodyReference != null && targetTransform != null;
        bool canDrive = hasTargets && interactHeld;

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

        bool useRightArm = ShouldUseRightArm();

        if (useRightArm && rightArmSolverTarget != null)
        {
            FollowTarget(rightArmSolverTarget, destination);
            ReturnToRest(leftArmSolverTarget, leftRestLocalPosition, leftRestLocalRotation, leftRestCaptured);
        }
        else if (!useRightArm && leftArmSolverTarget != null)
        {
            FollowTarget(leftArmSolverTarget, destination);
            ReturnToRest(rightArmSolverTarget, rightRestLocalPosition, rightRestLocalRotation, rightRestCaptured);
        }
        else
        {
            Transform fallback = rightArmSolverTarget != null ? rightArmSolverTarget : leftArmSolverTarget;
            if (fallback != null)
            {
                FollowTarget(fallback, destination);
            }
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
            SetSolverEnabled(leftArmIkSolver, false);
            SetSolverEnabled(rightArmIkSolver, false);
            return;
        }

        bool useRightArm = ShouldUseRightArm();

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
