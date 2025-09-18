using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class RobotLocomotionController : MonoBehaviour
{
    [Header("Feet Stepper References")]
    public FootStepper leftFoot;
    public FootStepper rightFoot;

    [Header("Jump Settings")]
    [SerializeField] private float jumpUpDuration = 18f;
    [SerializeField] private float jumpDownDuration = 18f;
    [SerializeField] private float crouchUpDuration = 18f;
    [SerializeField] private float crouchDownDuration = 18f;

    private bool isWalking = false;
    private bool isJumping = false;
    private bool isCrouching = false;
    private Coroutine walkRoutine;
    public event Action OnJumpStarted;
    public event Action OnJumpEnded;
    public event Action OnCrouchStarted;
    public event Action OnCrouchEnded;
    private RobotStateController robotBehaviour;
    private InputSystem_Actions controls;
    [SerializeField] public bool isPlayerControlled = false;
    
    [Header("Animator (Legs Mode)")]
    [SerializeField] private bool useAnimatorLegs = false;
    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float inputWalkThreshold = 0.2f;
    [SerializeField, Min(0f)] private float walkAnimSpeedMin = 0.6f;
    [SerializeField, Min(0f)] private float walkAnimSpeedMax = 1.2f;
    [SerializeField, Min(0f)] private float walkAnimSpeedSmoothing = 10f;
    private float currentAnimSpeed = 0f;
    [SerializeField] private AnimationCurve walkSpeedCurve = AnimationCurve.Linear(0,0,1,1);
    private float speedVel;
    private int walkStateHash;
    private int walkBackStateHash;

    [SerializeField] private float energyCostPerStep = 1f;
    [SerializeField] private float energyCostPerJump = 3f;
    [SerializeField] private float energyCostPerCrouch = 3f;
    [SerializeField] private bool waitStep = true;
    private bool _flipped = false;
    [SerializeField] private float timeout = 0.5f;

    private void Awake()
    {
        robotBehaviour = GetComponent<RobotStateController>();
        if (robotBehaviour == null)
            Debug.LogError("RobotLocomotionController: PlayerStateController not found.");

        if (isPlayerControlled)
        {
            controls = new InputSystem_Actions();
        }

        if (animator == null)
            animator = GetComponent<Animator>();

        walkStateHash = Animator.StringToHash("Base Layer.Walk");
        walkBackStateHash = Animator.StringToHash("Base Layer.WalkBack");
    }

    private void OnEnable()
    {
        if (isPlayerControlled && controls != null)
            controls.Enable();
    }

    private void OnDisable()
    {
        if (isPlayerControlled && controls != null)
            controls.Disable();
    }

    #region Movement

    public void HandleMovement(float horizontalInput, bool flipped)
    {
        _flipped = flipped;
        if (isJumping)
        {
            if (useAnimatorLegs && animator != null)
            {
                animator.SetFloat("Direction", _flipped ? -1f : 1f);
                animator.SetFloat("Speed", 0f);
                animator.SetBool("IsWalking", false);
            }
            return;
        }

        float inputMag = Mathf.Abs(horizontalInput);
        bool walking = inputMag > inputWalkThreshold;

        if (walking)
        {
            bool shouldFlip = horizontalInput < 0;
            if (shouldFlip != _flipped)
            {
                _flipped = shouldFlip;
                SetFacingDirection(!_flipped);
            }

            if (useAnimatorLegs)
            {
                if (animator != null)
                {
                    float dir = horizontalInput >= 0 ? 1f : -1f;
                    animator.SetFloat("Direction", dir);

                    // Map input to target speed using curve
                    float lin = Mathf.InverseLerp(inputWalkThreshold, 1f, inputMag);
                    float curved = walkSpeedCurve != null ? walkSpeedCurve.Evaluate(Mathf.Clamp01(lin)) : lin;
                    float targetAnimSpeed = Mathf.Lerp(walkAnimSpeedMin, walkAnimSpeedMax, curved);

                    // Only update speed near cycle boundary for stability
                    var s = animator.GetCurrentAnimatorStateInfo(0);
                    bool inWalk = s.fullPathHash == walkStateHash || s.fullPathHash == walkBackStateHash;
                    float phase = s.normalizedTime - Mathf.Floor(s.normalizedTime);
                    if (!inWalk || phase < 0.1f)
                    {
                        currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, targetAnimSpeed, ref speedVel, 1f / Mathf.Max(0.0001f, walkAnimSpeedSmoothing));
                        animator.SetFloat("Speed", currentAnimSpeed);
                    }
                    animator.SetBool("IsWalking", true);
                    animator.SetBool("IsCrouching", false);
                }
            }
            else
            {
                if (!isWalking)
                    StartWalking();
            }
        }
        else
        {
            if (useAnimatorLegs)
            {
                if (animator != null)
                {
                    currentAnimSpeed = Mathf.SmoothDamp(currentAnimSpeed, 0f, ref speedVel, 1f / Mathf.Max(0.0001f, walkAnimSpeedSmoothing));
                    animator.SetFloat("Speed", currentAnimSpeed);
                    animator.SetBool("IsWalking", false);
                }
            }
            else if (isWalking)
            {
                StopWalking();
            }
        }
    }

    private void StartWalking()
    {
        if (isWalking) return;

        isWalking = true;
        if (walkRoutine != null) StopCoroutine(walkRoutine);

        var footA = _flipped ? rightFoot : leftFoot;
        var footB = _flipped ? leftFoot : rightFoot;

        walkRoutine = StartCoroutine(StepChain(footA, footB));
    }

    private void StopWalking()
    {
        isWalking = false;
        if (walkRoutine != null)
        {
            StopCoroutine(walkRoutine);
            walkRoutine = null;
        }

        rightFoot.InterruptAndReset();
        leftFoot.InterruptAndReset();
    }

    private IEnumerator StepChain(FootStepper footA, FootStepper footB)
    {
        if (waitStep)
            yield return WaitUntilWithTimeout(() => footA.IsGrounded && footB.IsGrounded, timeout);

        if (!robotBehaviour.CanPerformEnergy(energyCostPerStep)) yield break;

        while (isWalking)
        {
            bool notifiedToBack = false;
            footA.ToPeak(() => notifiedToBack = true);
            yield return WaitUntilWithTimeout(() => notifiedToBack, timeout);

            bool notifiedToFar = false;
            footB.ToBack(() => notifiedToFar = true);
            yield return WaitUntilWithTimeout(() => notifiedToFar, timeout);

            bool aArrivedFar = false;
            footA.ToFar(() => aArrivedFar = true);

            if (waitStep)
                yield return WaitUntilWithTimeout(() => aArrivedFar && footA.IsGrounded, timeout);

            bool aDone = false, bDone = false;
            footA.ToStartFromFarOrBack(() => aDone = true);
            footB.ToStartFromFarOrBack(() => bDone = true);
            yield return WaitUntilWithTimeout(() => aDone && bDone, timeout);

            robotBehaviour?.ConsumeEnergy(energyCostPerStep);

            (footA, footB) = (footB, footA);
        }
    }

    private IEnumerator WaitUntilWithTimeout(Func<bool> predicate, float timeout)
    {
        float timer = 0f;
        while (!predicate() && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }
    }

    #endregion

    #region Jump

    public void Jump()
    {
        if (isJumping) return;

        if (!robotBehaviour.CanPerformEnergy(energyCostPerJump)) return;

        isJumping = true;
        if (!useAnimatorLegs)
            StopWalking();
        robotBehaviour?.ConsumeEnergy(energyCostPerJump);
        OnJumpStarted?.Invoke();

        if (useAnimatorLegs && animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsCrouching", false);
            animator.SetBool("IsJumping", true);
            StartCoroutine(JumpSimpleRoutine());
        }
        else
        {
            int feetLanded = 0;
            Action onFootLanded = () =>
            {
                feetLanded++;
                if (feetLanded >= 2)
                    OnJumpEndedInternal();
            };

            leftFoot.Jump(jumpUpDuration, jumpDownDuration, onFootLanded);
            rightFoot.Jump(jumpUpDuration, jumpDownDuration, onFootLanded);
        }
    }

    private void OnJumpEndedInternal()
    {
        isJumping = false;
        if (!useAnimatorLegs)
            StopWalking();

        if (isPlayerControlled && controls != null)
        {
            float input = controls.Player.Move.ReadValue<Vector2>().x;
            if (Mathf.Abs(input) > 0.2f)
                HandleMovement(input, _flipped);
        }

        if (useAnimatorLegs && animator != null)
        {
            animator.SetBool("IsJumping", false);
        }

        OnJumpEnded?.Invoke();
    }

    #endregion

    #region Crouch
    public void Crouch()
    {
        if (isCrouching) return;

        if (!robotBehaviour.CanPerformEnergy(energyCostPerCrouch)) return;

        isCrouching = true;
        if (!useAnimatorLegs)
            StopWalking();
        robotBehaviour?.ConsumeEnergy(energyCostPerCrouch);
        OnCrouchStarted?.Invoke();

        if (useAnimatorLegs && animator != null)
        {
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsCrouching", true);
        }
        else
        {
            int feetFinished = 0;
            Action onFootFinished = () =>
            {
                feetFinished++;
                if (feetFinished >= 2)
                {
                    isCrouching = false;
                    OnCrouchEnded?.Invoke();
                }
            };

            leftFoot.Crouch(crouchUpDuration, crouchDownDuration, onFootFinished);
            rightFoot.Crouch(crouchUpDuration, crouchDownDuration, onFootFinished);
        }
    }

    /// <summary>
    /// Returns the robot to a standing position.
    /// </summary>
    public void Uncrouch()
    {
        if (!useAnimatorLegs)
        {
            leftFoot?.InterruptAndReset();
            rightFoot?.InterruptAndReset();
        }
        isCrouching = false;
        if (useAnimatorLegs && animator != null)
        {
            animator.SetBool("IsCrouching", false);
        }
        OnCrouchEnded?.Invoke();
    }
    #endregion Crouch

    #region Facing

    public void SetFacingDirection(bool isRight)
    {
        leftFoot?.SetFacingDirection(isRight);
        rightFoot?.SetFacingDirection(isRight);
    }

    #endregion
    private IEnumerator JumpSimpleRoutine()
    {
        float wait = Mathf.Max(0f, jumpUpDuration) + Mathf.Max(0f, jumpDownDuration);
        yield return new WaitForSeconds(wait);
        OnJumpEndedInternal();
    }
}
