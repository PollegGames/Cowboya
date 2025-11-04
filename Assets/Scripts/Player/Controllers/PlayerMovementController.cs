using System;
using UnityEngine;

[RequireComponent(typeof(RobotLocomotionController), typeof(Inventory))]
public class PlayerMovementController : MonoBehaviour, ILookDirectionProvider, IRobotDecisionProvider
{
    [Header("Components")]
    [SerializeField] private RobotLocomotionController locomotion;
    [SerializeField] private BodyBalance bodyBalance;
    [SerializeField] private float forceUpward = 5f;
    [SerializeField] private float forceSide = 5f;
    [SerializeField] private LegJointLimiter legJointLimiter;
    [SerializeField] private BodyJointLimiter bodyJointLimiter;
    [SerializeField] private RobotStateController robotBehaviour;

    [Header("Facing/Mirroring")]
    [SerializeField] private PoleMirror2D poleMirror;
    [SerializeField] private SpriteFlip2D spriteFlipper;

    [Header("Body Rotation")]
    [SerializeField] private Rigidbody2D bodyReference;
    public Rigidbody2D BodyReference => bodyReference;

    [SerializeField, Min(0f)] private float maxLeftTiltDeg  = 20f; 
    [SerializeField, Min(0f)] private float maxRightTiltDeg = 2f;

    [SerializeField] private MonoBehaviour inputSource;
    private IPlayerInput input;

    public IPlayerInput Input => input;
    private bool flipped = false;
    private float horizontalInput;
    private float verticalInput;
    private float targetRotation;
    private bool isCrouchingInput;

    [SerializeField] private EnergyBot energyBot;
    [SerializeField] private Inventory inventory;

    private Vector2 lookDirection = Vector2.right;
    private Vector2 aimVector = Vector2.right;
    private bool aimFromLookInput = false;
    private AttackSector currentSector = AttackSector.Right;


    /// <summary>
    /// Invoked when the attack sector changes based on input.
    /// </summary>
    public event Action<AttackSector> SectorChanged;

    /// <summary>
    /// Gets the current look direction.
    /// </summary>
    public Vector2 LookDirection => lookDirection;

    /// <summary>
    /// Gets the most recent aiming vector used for sector evaluation.
    /// </summary>
    public Vector2 AimVector => aimVector;

    /// <summary>
    /// Gets the current attack sector derived from input.
    /// </summary>
    public AttackSector CurrentSector => currentSector;

    private void Awake()
    {
        locomotion.OnJumpEnded += HandleJumpEnd;

        if (robotBehaviour == null)
            robotBehaviour = GetComponent<RobotStateController>();

        robotBehaviour.OnStateChanged += HandleStateChange;

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        input = inputSource as IPlayerInput;
        if (input == null)
        {
            Debug.LogError("PlayerMovementController: inputSource does not implement IPlayerInput");
        }
        // Ensure initial facing is applied to all modules (including PoleMirror2D)
        ApplyFacingDirection();
    }

    private void Update()
    {
        if (robotBehaviour.CurrentState != RobotState.Alive) return;
        if (input != null)
        {
            horizontalInput = input.Movement.x;
            verticalInput = input.Movement.y;


            if (Mathf.Abs(horizontalInput) > 0.1f)
                lookDirection = new Vector2(Mathf.Sign(horizontalInput), 0f);

            Vector2 lookInput = input.Look;
            if (lookInput.sqrMagnitude > 0.0001f)
            {
                aimVector = lookInput;
                aimFromLookInput = true;
            }
            else if (!aimFromLookInput || aimVector.sqrMagnitude <= 0.0001f)
            {
                aimVector = lookDirection;
                aimFromLookInput = false;
            }
        }
        else
        {
            aimVector = lookDirection;
            aimFromLookInput = false;
        }

        TryFlip();
        UpdateAttackSector();
        CalculateAndApplyBodyRotation();
        HandleMovement();
        HandleJump();
        HandleCrouch();
    }

    private void TryFlip()
    {
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            bool movingLeft = horizontalInput < 0f;
            if (movingLeft != flipped)
            {
                flipped = movingLeft;
                ApplyFacingDirection();
            }
        }
    }

    private void ApplyFacingDirection()
    {
        locomotion.SetFacingDirection(!flipped);
        legJointLimiter.SetLegRotationLimits(!flipped); // true = facing right
        if (bodyJointLimiter != null)
            bodyJointLimiter.SetBodyRotationLimits(!flipped);

        // Mirror poles (arms, hands, etc.)
        if (poleMirror != null)
            poleMirror.SetFacing(!flipped);

        // Ensure all sprites flip consistently
        if (spriteFlipper != null)
            spriteFlipper.SetFacing(!flipped);
    }

    private void CalculateAndApplyBodyRotation()
    {
        if (Mathf.Approximately(horizontalInput, 0f))
        {
            targetRotation = 0f;
        }
        else
        {
            // Map [-1 .. +1] -> angle where right = negative, left = positive
            float t = (horizontalInput + 1f) * 0.5f; // -1 -> 0, +1 -> 1
            float leftDeg  = Mathf.Max(0f, maxLeftTiltDeg);
            float rightDeg = Mathf.Max(0f, maxRightTiltDeg);

            // Right limit is -rightDeg, left limit is +leftDeg
            targetRotation = Mathf.Lerp(leftDeg, -rightDeg, t);
        }

        ApplyBodyRotation();
    }

    private void ApplyBodyRotation()
    {
        foreach (var muscle in bodyBalance.muscles)
        {
            muscle.restRotation = targetRotation;
        }
    }

    private void HandleMovement()
    {
        locomotion.HandleMovement(horizontalInput, flipped);
    }

    private void HandleJump()
    {
        if (verticalInput > 0)
        {
            locomotion.Jump();
        }
    }

    private void HandleCrouch()
    {
        if (verticalInput < 0 && !isCrouchingInput)
        {
            locomotion.Crouch();
        }
        else if (verticalInput >= 0 && isCrouchingInput)
        {
            locomotion.Uncrouch();
        }

        isCrouchingInput = verticalInput < 0;
    }

    /// <inheritdoc />
    public Vector2 Movement => new Vector2(horizontalInput, verticalInput);

    /// <inheritdoc />
    public Vector2 DesiredFacing
    {
        get
        {
            if (aimVector.sqrMagnitude > 0.0001f)
                return aimVector.normalized;
            if (lookDirection.sqrMagnitude > 0.0001f)
                return lookDirection.normalized;
            return Vector2.right;
        }
    }

    /// <inheritdoc />
    public bool TryBuildAttackRequest(out AttackRequest request)
    {
        request = default;

        if (input == null || !input.PrimaryAttack || robotBehaviour == null)
            return false;

        if (robotBehaviour.CurrentState != RobotState.Alive)
            return false;

        float energyRequired = 0f;
        if (robotBehaviour.Stats != null)
            energyRequired = robotBehaviour.Stats.AttackEnergyCost;

        Vector2 targetPosition = DetermineTargetPosition();
        request = new AttackRequest(targetPosition, currentSector, energyRequired);
        return true;
    }

    private Vector2 DetermineTargetPosition()
    {
        Vector2 aim = aimVector;
        if (aim.sqrMagnitude <= 0.0001f)
            aim = lookDirection;

        if (aim.sqrMagnitude > 0.0001f)
            return (Vector2)transform.position + aim.normalized;

        return transform.position;
    }

    private void UpdateAttackSector()
    {
        AttackSector newSector = DetermineSector(aimVector);
        if (newSector == currentSector)
            return;

        currentSector = newSector;
        SectorChanged?.Invoke(currentSector);
    }

    private AttackSector DetermineSector(Vector2 vector)
    {
        if (vector.sqrMagnitude <= 0.0001f)
        {
            return flipped ? AttackSector.Left : AttackSector.Right;
        }

        if (Mathf.Abs(vector.x) >= Mathf.Abs(vector.y))
        {
            return vector.x >= 0f ? AttackSector.Right : AttackSector.Left;
        }

        return vector.y >= 0f ? AttackSector.Up : AttackSector.Down;
    }

    private void HandleJumpEnd()
    {
        if (bodyReference != null)
        {
            Vector2 upwardForce = Vector2.up * forceUpward;
            float horizontalDirection = flipped ? -1f : 1f;
            Vector2 horizontalForce = Vector2.right * horizontalDirection * forceSide;
            Vector2 combinedForce = upwardForce + horizontalForce;
            bodyReference.AddForce(combinedForce, ForceMode2D.Impulse);
        }
    }

    private void HandleStateChange(RobotState newState)
    {
        if (newState == RobotState.Faint)
        {
            Faint();
        }
        else if (newState == RobotState.Dead)
        {
            Die();
        }
        else if (newState == RobotState.Alive)
        {
            UpdateBalance(true);
        }
    }

    public void Faint()
    {
        UpdateBalance(false);
    }

    public void Die()
    {
        inventory?.DropAll();

        var jointBreaker = GetComponent<JointBreaker>();
        jointBreaker?.BreakAll();
    }

    private void UpdateBalance(bool enabledBalance)
    {
        var balance = GetComponent<BodyBalance>();
        if (balance != null)
        {
            balance.UpdateBalance(enabledBalance);
        }
    }
}
