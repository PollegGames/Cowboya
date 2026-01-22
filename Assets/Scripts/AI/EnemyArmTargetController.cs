using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Enemy arm driver that mirrors ArmTargetController movement using AI-driven attack input.
/// targetTransform should already track the player (e.g., FollowPlayer).
/// </summary>
[DisallowMultipleComponent]
public class EnemyArmTargetController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform bodyReference;
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Transform leftArmSolverTarget;
    [SerializeField] private Transform rightArmSolverTarget;
    [SerializeField] private Behaviour leftArmIkSolver;
    [SerializeField] private Behaviour rightArmIkSolver;
    [SerializeField] private SimpleAttackController attackController;

    [Header("Motion")]
    [SerializeField] private float followSpeed = 15f;
    [SerializeField] private float returnSpeed = 10f;
    [SerializeField] private float rotationReturnSpeed = 720f;
    [SerializeField] private float sideSwitchThreshold = 0.1f;

    [Header("Attack")]
    [SerializeField] private float attackPulseDuration = 0.2f;

    public event Action AttackFinished;

    private bool attackInputHeld;
    private bool attackSuppressedUntilRelease;
    private bool preferRightArm = true;
    private CowboyArmSide? attackActiveArm;
    private Coroutine attackPulseRoutine;

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
        CacheRestPose();
        CacheSolverDefaults();
        SubscribeToAttackEvents();
    }

    private void OnEnable()
    {
        CacheRestPose();
        CacheSolverDefaults();
        SubscribeToAttackEvents();
    }

    private void OnDisable()
    {
        ResetState();
        UnsubscribeFromAttackEvents();
    }

    private void Update()
    {
        HandleAttackInput();
    }

    private void LateUpdate()
    {
        bool hasTargets = bodyReference != null && targetTransform != null;
        bool canDrive = hasTargets && attackInputHeld && !attackSuppressedUntilRelease;

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
        Transform activeArm = useRightArm ? rightArmSolverTarget : leftArmSolverTarget;
        Transform inactiveArm = useRightArm ? leftArmSolverTarget : rightArmSolverTarget;

        if (activeArm == null && inactiveArm != null)
        {
            activeArm = inactiveArm;
            inactiveArm = null;
        }

        if (activeArm != null)
        {
            FollowTarget(activeArm, destination);
        }

        if (inactiveArm != null)
        {
            ReturnToRest(
                inactiveArm,
                inactiveArm == leftArmSolverTarget ? leftRestLocalPosition : rightRestLocalPosition,
                inactiveArm == leftArmSolverTarget ? leftRestLocalRotation : rightRestLocalRotation,
                inactiveArm == leftArmSolverTarget ? leftRestCaptured : rightRestCaptured);
        }
    }

    /// <summary>
    /// Directly set attack input from the brain (use for hold-style requests).
    /// </summary>
    public void SetAttackRequested(bool active)
    {
        attackInputHeld = active;

        if (!active)
        {
            attackSuppressedUntilRelease = false;
            StopAttackIfActive();
        }
    }

    /// <summary>
    /// Trigger a short attack request pulse (use for 1-shot attacks).
    /// </summary>
    public void TriggerAttackPulse(float durationSeconds = -1f)
    {
        if (durationSeconds <= 0f)
        {
            durationSeconds = attackPulseDuration;
        }

        if (attackPulseRoutine != null)
            StopCoroutine(attackPulseRoutine);

        attackPulseRoutine = StartCoroutine(AttackPulse(durationSeconds));
    }

    private IEnumerator AttackPulse(float durationSeconds)
    {
        SetAttackRequested(true);
        float elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetAttackRequested(false);
        attackPulseRoutine = null;
    }

    private void HandleAttackInput()
    {
        if (attackController == null)
        {
            return;
        }

        if (attackSuppressedUntilRelease)
        {
            StopAttackIfActive();
            return;
        }

        if (!attackInputHeld)
        {
            StopAttackIfActive();
            return;
        }

        CowboyArmSide desiredArm = GetCurrentActiveArmSide();

        if (!attackActiveArm.HasValue)
        {
            attackController.SetArmAttackActive(desiredArm, true);
            attackActiveArm = desiredArm;
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
        if (attackController != null)
        {
            attackController.SetArmAttackActive(arm, false);
        }

        StopAttackIfActive();
    }

    private void StopAttackIfActive()
    {
        if (attackActiveArm.HasValue && attackController != null)
        {
            attackController.SetArmAttackActive(attackActiveArm.Value, false);
            attackActiveArm = null;
            AttackFinished?.Invoke();
        }
    }

    private void ResetState()
    {
        if (attackPulseRoutine != null)
            StopCoroutine(attackPulseRoutine);
        attackPulseRoutine = null;

        attackInputHeld = false;
        attackSuppressedUntilRelease = false;
        StopAttackIfActive();
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

    private CowboyArmSide GetCurrentActiveArmSide()
    {
        if (bodyReference != null && targetTransform != null)
        {
            UpdatePreferredSide();
        }

        return ShouldUseRightArm() ? CowboyArmSide.Right : CowboyArmSide.Left;
    }

    private bool ShouldUseRightArm()
    {
        return preferRightArm;
    }

    private void SubscribeToAttackEvents()
    {
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

    private static void SetSolverEnabled(Behaviour solver, bool enabled)
    {
        if (solver == null)
        {
            return;
        }

        solver.enabled = enabled;
    }
}
