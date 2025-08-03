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

    private bool isWalking = false;
    private bool isJumping = false;
    private Coroutine walkRoutine;
    public event Action OnJumpStarted;
    public event Action OnJumpEnded;
    private RobotStateController robotBehaviour;
    private InputSystem_Actions controls;
    [SerializeField] public bool isPlayerControlled = false;

    [SerializeField] private float energyCostPerStep = 1f;
    [SerializeField] private float energyCostPerJump = 3f;
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
        if (isJumping) return;

        bool walking = Mathf.Abs(horizontalInput) > 0.2f;

        if (walking)
        {
            bool shouldFlip = horizontalInput < 0;
            if (shouldFlip != _flipped)
            {
                _flipped = shouldFlip;
                SetFacingDirection(!_flipped);
            }

            if (!isWalking)
                StartWalking();
        }
        else if (isWalking)
        {
            StopWalking();
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
        StopWalking();
        robotBehaviour?.ConsumeEnergy(energyCostPerJump);
        OnJumpStarted?.Invoke();

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

    private void OnJumpEndedInternal()
    {
        isJumping = false;
        StopWalking();

        if (isPlayerControlled && controls != null)
        {
            float input = controls.Player.Move.ReadValue<Vector2>().x;
            if (Mathf.Abs(input) > 0.2f)
                HandleMovement(input, _flipped);
        }

        OnJumpEnded?.Invoke();
    }

    #endregion

    #region Facing

    public void SetFacingDirection(bool isRight)
    {
        leftFoot?.SetFacingDirection(isRight);
        rightFoot?.SetFacingDirection(isRight);
    }

    #endregion
}
