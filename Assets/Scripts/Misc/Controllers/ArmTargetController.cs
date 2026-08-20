using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Resolves independent player arm modes and drives active arms toward the shared radial target.
/// </summary>
public class ArmTargetController : MonoBehaviour
{
    private sealed class ArmRuntimeState
    {
        public bool DriveInput;
        public bool HeldInput;
        public bool WasHeldInput;
        public PlayerArmMode Mode;
        public float NextHoldEnergyTime;
        public float RegrabLockedUntil;
        public Vector3 LastTargetPosition;
        public bool HasLastTargetPosition;
        public float AimSpeed;
        public bool AttackActive;
        public bool AttackEnergySpent;
        public float AttackGraceUntil;
        public float AttackLockedUntil;
        public bool AttackHitConsumed;
    }

    [Header("References")]
    [SerializeField] private MonoBehaviour inputSource;
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

    [Header("Controllers")]
    [SerializeField] private CowboyGrabController grabController;
    [SerializeField] private SimpleAttackController attackController;
    [SerializeField] private bool allowAttackAim = true;

    [Header("Energy")]
    [SerializeField] private PlayerBrain playerBrain;
    [SerializeField] private RobotStateController stateController;
    [SerializeField] private float holdEnergyInterval = 1f;
    [SerializeField] private float holdEnergyCostPerInterval = 1f;

    [Header("Attack")]
    [SerializeField] private float attackSpeedThreshold = 60f;
    [SerializeField] private float attackHoldAfterSlowDuration = 0.1f;
    [SerializeField] private float attackCooldownAfterHoldReleaseDuration = 2f;

    [Header("Grab")]
    [SerializeField] private float regrabLockoutDuration = 2f;

    [Header("AI Attack Control")]
    [SerializeField] private bool useExternalAttackInput;
    private Coroutine externalAttackPulseRoutine;
    private bool externalAttackInput;

    private readonly ArmRuntimeState leftArm = new ArmRuntimeState();
    private readonly ArmRuntimeState rightArm = new ArmRuntimeState();
    private IPlayerInput input;

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

    public PlayerArmMode LeftArmMode => leftArm.Mode;
    public PlayerArmMode RightArmMode => rightArm.Mode;

    private void Awake()
    {
        CacheControllers();
        CacheInput();
    }

    private void OnEnable()
    {
        CacheControllers();
        CacheRestPose();
        ResetArmState(leftArm, leftArmSolverTarget);
        ResetArmState(rightArm, rightArmSolverTarget);
        attackController?.DeactivateAll();
        SubscribeToAttackEvents();
        SubscribeToStateEvents();
    }

    private void OnDisable()
    {
        leftArm.DriveInput = false;
        rightArm.DriveInput = false;
        leftArm.HeldInput = false;
        rightArm.HeldInput = false;
        DeactivateAttack(CowboyArmSide.Left, leftArm);
        DeactivateAttack(CowboyArmSide.Right, rightArm);
        attackController?.DeactivateAll();
        grabController?.ReleaseAllImmediate();
        UnsubscribeFromAttackEvents();
        UnsubscribeFromStateEvents();
    }

    private void Update()
    {
        CacheInput();
        PlayerArmMode leftMode = ResolveMode(CowboyArmSide.Left);
        PlayerArmMode rightMode = ResolveMode(CowboyArmSide.Right);

        if (useExternalAttackInput && externalAttackInput)
            leftMode = PlayerArmMode.Attack;

        UpdateArmInput(CowboyArmSide.Left, leftArm, leftMode);
        UpdateArmInput(CowboyArmSide.Right, rightArm, rightMode);

        HandleHoldEnergy(CowboyArmSide.Left, leftArm);
        HandleHoldEnergy(CowboyArmSide.Right, rightArm);

        HandleGrabInput(CowboyArmSide.Left, leftArm);
        HandleGrabInput(CowboyArmSide.Right, rightArm);

        MaintainHeldObjects();
    }

    private void LateUpdate()
    {
        bool hasTargets = bodyReference != null && targetTransform != null;

        if (hasTargets)
        {
            ApplySolverFlip(targetTransform.position.x >= bodyReference.position.x);
        }

        ApplySolverStates();

        if (!hasTargets)
        {
            return;
        }

        DriveArm(CowboyArmSide.Left, leftArm, leftArmSolverTarget,
            leftRestLocalPosition, leftRestLocalRotation, leftRestCaptured);
        DriveArm(CowboyArmSide.Right, rightArm, rightArmSolverTarget,
            rightRestLocalPosition, rightRestLocalRotation, rightRestCaptured);

        UpdateAimSpeed(leftArm, leftArmSolverTarget);
        UpdateAimSpeed(rightArm, rightArmSolverTarget);

        HandleAttackState(CowboyArmSide.Left, leftArm);
        HandleAttackState(CowboyArmSide.Right, rightArm);
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

    private void CacheInput()
    {
        if (input != null)
            return;

        input = inputSource as IPlayerInput;
        if (input == null)
        {
            input = GetComponent<IPlayerInput>();
        }

        if (input == null)
        {
            input = GetComponentInParent<IPlayerInput>();
        }
    }

    private PlayerArmMode ResolveMode(CowboyArmSide arm)
    {
        if (!IsAlive() || input == null)
            return PlayerArmMode.Rest;

        if (arm == CowboyArmSide.Left)
        {
            return PlayerArmModeResolver.Resolve(
                input.LeftGrabHeld,
                input.LeftGrabPressSequence,
                input.LeftAttackHeld,
                input.LeftAttackPressSequence);
        }

        return PlayerArmModeResolver.Resolve(
            input.RightGrabHeld,
            input.RightGrabPressSequence,
            input.RightAttackHeld,
            input.RightAttackPressSequence);
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

    private void SubscribeToStateEvents()
    {
        UnsubscribeFromStateEvents();
        if (stateController != null)
        {
            stateController.OnStateChanged += HandleRobotStateChanged;
        }
    }

    private void UnsubscribeFromStateEvents()
    {
        if (stateController != null)
        {
            stateController.OnStateChanged -= HandleRobotStateChanged;
        }
    }

    private void UpdateArmInput(CowboyArmSide arm, ArmRuntimeState state, PlayerArmMode mode)
    {
        PlayerArmMode previousMode = state.Mode;
        state.Mode = mode;
        state.DriveInput = mode != PlayerArmMode.Rest;
        state.WasHeldInput = state.HeldInput;
        state.HeldInput = mode == PlayerArmMode.Grab;

        if (state.HeldInput && !state.WasHeldInput)
        {
            state.NextHoldEnergyTime = Time.time + Mathf.Max(0.01f, holdEnergyInterval);
            if (!TrySpendHoldEnergy())
            {
                state.Mode = PlayerArmMode.Rest;
                state.DriveInput = false;
                state.HeldInput = false;
                grabController?.SetHandAttractorState(arm, false);
            }
        }

        if (!state.HeldInput && state.WasHeldInput && ArmHasHeldObject(arm))
        {
            grabController?.Release(arm);
            state.RegrabLockedUntil = Time.time + Mathf.Max(0f, regrabLockoutDuration);
        }

        if (!state.HeldInput && state.WasHeldInput && !ArmHasHeldObject(arm))
        {
            grabController?.SetHandAttractorState(arm, false);
        }

        if (previousMode == PlayerArmMode.Attack && mode != PlayerArmMode.Attack)
            DeactivateAttack(arm, state);
    }

    private void HandleHoldEnergy(CowboyArmSide arm, ArmRuntimeState state)
    {
        if (!state.HeldInput || holdEnergyCostPerInterval <= 0f)
        {
            return;
        }

        float interval = Mathf.Max(0.01f, holdEnergyInterval);
        if (Time.time < state.NextHoldEnergyTime)
        {
            return;
        }

        if (!TrySpendHoldEnergy())
        {
            state.HeldInput = false;
            grabController?.SetHandAttractorState(arm, false);
            return;
        }

        state.NextHoldEnergyTime = Time.time + interval;
    }

    private void HandleGrabInput(CowboyArmSide arm, ArmRuntimeState state)
    {
        if (grabController == null)
        {
            return;
        }

        if (grabController.HasHeldObject(arm))
        {
            grabController.SetHandAttractorState(arm, true);
            return;
        }

        if (!state.HeldInput)
        {
            grabController.SetHandAttractorState(arm, false);
            return;
        }

        bool canTryGrab = Time.time >= state.RegrabLockedUntil;
        grabController.SetHandAttractorState(arm, canTryGrab);
        if (canTryGrab)
        {
            grabController.TryGrab(arm);
        }
    }

    private void MaintainHeldObjects()
    {
        if (grabController == null)
        {
            return;
        }

        grabController.MaintainHold(CowboyArmSide.Left);
        grabController.MaintainHold(CowboyArmSide.Right);
    }

    private void DriveArm(
        CowboyArmSide arm,
        ArmRuntimeState state,
        Transform solverTarget,
        Vector3 restLocalPosition,
        Quaternion restLocalRotation,
        bool hasRest)
    {
        _ = arm;

        if (solverTarget == null)
        {
            return;
        }

        if (state.DriveInput)
        {
            FollowTarget(solverTarget, targetTransform.position);
            return;
        }

        ReturnToRest(solverTarget, restLocalPosition, restLocalRotation, hasRest);
    }

    private void UpdateAimSpeed(ArmRuntimeState state, Transform solverTarget)
    {
        if (solverTarget == null)
        {
            state.AimSpeed = 0f;
            state.HasLastTargetPosition = false;
            return;
        }

        Vector3 currentPosition = solverTarget.position;
        if (!state.HasLastTargetPosition || Time.deltaTime <= 0f)
        {
            state.AimSpeed = 0f;
            state.LastTargetPosition = currentPosition;
            state.HasLastTargetPosition = true;
            return;
        }

        state.AimSpeed = Vector3.Distance(currentPosition, state.LastTargetPosition) / Time.deltaTime;
        state.LastTargetPosition = currentPosition;
    }

    private void HandleAttackState(CowboyArmSide arm, ArmRuntimeState state)
    {
        if (attackController == null)
        {
            return;
        }

        bool canAttack = allowAttackAim
            && state.Mode == PlayerArmMode.Attack
            && Time.time >= state.AttackLockedUntil
            && !ArmHasHeldObject(arm)
            && IsAlive();

        if (!canAttack)
        {
            DeactivateAttack(arm, state);
            return;
        }

        if (state.AttackHitConsumed)
        {
            if (state.AimSpeed < attackSpeedThreshold)
                state.AttackHitConsumed = false;

            return;
        }

        if (state.AttackActive)
        {
            if (state.AimSpeed >= attackSpeedThreshold)
            {
                state.AttackGraceUntil = Time.time + Mathf.Max(0f, attackHoldAfterSlowDuration);
            }

            if (Time.time > state.AttackGraceUntil)
            {
                DeactivateAttack(arm, state);
            }

            return;
        }

        if (state.AimSpeed < attackSpeedThreshold)
        {
            return;
        }

        if (!state.AttackEnergySpent && !TrySpendEnergyForAttack())
        {
            return;
        }

        state.AttackActive = true;
        state.AttackEnergySpent = true;
        state.AttackHitConsumed = false;
        state.AttackGraceUntil = Time.time + Mathf.Max(0f, attackHoldAfterSlowDuration);
        attackController.SetArmAttackActive(arm, true);
    }

    private void DeactivateAttack(CowboyArmSide arm, ArmRuntimeState state)
    {
        if (attackController != null && state.AttackActive)
        {
            attackController.SetArmAttackActive(arm, false);
        }

        state.AttackActive = false;
        state.AttackEnergySpent = false;
        if (state.Mode != PlayerArmMode.Attack)
            state.AttackHitConsumed = false;
        state.AttackGraceUntil = 0f;
    }

    private void OnAttackHit(CowboyArmSide arm)
    {
        ArmRuntimeState state = GetArmState(arm);
        state.AttackHitConsumed = true;

        if (attackController != null)
        {
            attackController.SetArmAttackActive(arm, false);
        }

        state.AttackActive = false;
        state.AttackEnergySpent = false;
        state.AttackLockedUntil = Time.time + Mathf.Max(0f, attackCooldownAfterHoldReleaseDuration);
    }

    private void HandleRobotStateChanged(RobotState newState)
    {
        if (newState == RobotState.Alive)
        {
            return;
        }

        leftArm.HeldInput = false;
        rightArm.HeldInput = false;
        leftArm.DriveInput = false;
        rightArm.DriveInput = false;
        DeactivateAttack(CowboyArmSide.Left, leftArm);
        DeactivateAttack(CowboyArmSide.Right, rightArm);
        grabController?.ReleaseAllImmediate();
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

    private void ApplySolverStates()
    {
        SetSolverEnabled(leftArmIkSolver, leftSolverDefaultEnabled);
        SetSolverEnabled(rightArmIkSolver, rightSolverDefaultEnabled);
    }

    private static void SetSolverEnabled(Behaviour solver, bool enabled)
    {
        if (solver == null)
        {
            return;
        }

        solver.enabled = enabled;
    }

    private bool TrySpendHoldEnergy()
    {
        if (!IsAlive())
        {
            return false;
        }

        if (playerBrain != null)
        {
            return playerBrain.TrySpendEnergy(EnergyAction.Grab, 0f, holdEnergyCostPerInterval);
        }

        if (stateController != null)
        {
            if (!stateController.CanPerformEnergy(holdEnergyCostPerInterval))
            {
                return false;
            }

            stateController.ConsumeEnergy(holdEnergyCostPerInterval);
            return true;
        }

        return true;
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

    private bool IsAlive()
    {
        return stateController == null || stateController.CurrentState == RobotState.Alive;
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
        yield return null;
        externalAttackInput = false;
        externalAttackPulseRoutine = null;
    }

    private void ApplySolverFlip(bool targetIsRightSide)
    {
        SetSolverFlip(leftArmIkSolver, targetIsRightSide);
        SetSolverFlip(rightArmIkSolver, targetIsRightSide);
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

    private ArmRuntimeState GetArmState(CowboyArmSide arm)
    {
        return arm == CowboyArmSide.Right ? rightArm : leftArm;
    }

    private bool ArmHasHeldObject(CowboyArmSide arm)
    {
        return grabController != null && grabController.HasHeldObject(arm);
    }

    private void ResetArmState(ArmRuntimeState state, Transform solverTarget)
    {
        state.DriveInput = false;
        state.HeldInput = false;
        state.WasHeldInput = false;
        state.Mode = PlayerArmMode.Rest;
        state.NextHoldEnergyTime = Time.time + Mathf.Max(0.01f, holdEnergyInterval);
        state.RegrabLockedUntil = 0f;
        state.LastTargetPosition = solverTarget != null ? solverTarget.position : Vector3.zero;
        state.HasLastTargetPosition = solverTarget != null;
        state.AimSpeed = 0f;
        state.AttackActive = false;
        state.AttackEnergySpent = false;
        state.AttackGraceUntil = 0f;
        state.AttackLockedUntil = 0f;
        state.AttackHitConsumed = false;
    }

}
