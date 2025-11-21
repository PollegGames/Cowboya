using UnityEngine;

[RequireComponent(typeof(RobotLocomotionController), typeof(Inventory))]
public class PlayerMovementController : MonoBehaviour, ILookDirectionProvider, IRobotDecisionProvider
{
    [Header("Components")]
    [SerializeField] private RobotLocomotionController locomotion;
    [SerializeField] private RobotStateController robotBehaviour;
    [Header("Body Reference")]
    [SerializeField] private Rigidbody2D bodyReference;
    public Rigidbody2D BodyReference => bodyReference;
    [Header("Player References")]
    [SerializeField] private Transform headTransform;
    public Transform HeadTransform => headTransform;

    [SerializeField] private MonoBehaviour inputSource;
    [SerializeField] private Transform aimTarget;
    private IPlayerInput input;

    public IPlayerInput Input => input;
    private bool flipped = false;
    private float horizontalInput;
    private float verticalInput;
    private bool isCrouchingInput;

    [SerializeField] private EnergyBot energyBot;
    [SerializeField] private Inventory inventory;

    private Vector2 lookDirection = Vector2.right;

    /// <summary>
    /// Gets the current look direction.
    /// </summary>
    public Vector2 LookDirection => lookDirection;

    private void Awake()
    {
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
        }

        TryFlip();
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
            Vector2 aimDirection = GetAimDirection();
            if (aimDirection.sqrMagnitude > 0.0001f)
                return aimDirection.normalized;
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
        AttackSector sector = DetermineSector(GetAimDirection());
        request = new AttackRequest(targetPosition, sector, energyRequired);
        return true;
    }

    private Vector2 DetermineTargetPosition()
    {
        if (aimTarget != null)
            return aimTarget.position;

        Vector2 aimDirection = GetAimDirection();
        if (aimDirection.sqrMagnitude > 0.0001f)
            return (Vector2)transform.position + aimDirection.normalized;

        return transform.position;
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

    private Vector2 GetAimDirection()
    {
        if (aimTarget != null)
        {
            Vector2 targetOffset = (Vector2)aimTarget.position - (Vector2)transform.position;
            if (targetOffset.sqrMagnitude > 0.0001f)
                return targetOffset;
        }

        if (input != null)
        {
            Vector2 lookInput = input.Look;
            if (lookInput.sqrMagnitude > 0.0001f)
                return lookInput;
        }

        if (lookDirection.sqrMagnitude > 0.0001f)
            return lookDirection;

        return Vector2.right;
    }

    private void HandleStateChange(RobotState newState)
    {
        if (newState == RobotState.Dead)
        {
            Die();
        }
    }

    public void Faint()
    {
    }

    public void Die()
    {
        inventory?.DropAll();

        var jointBreaker = GetComponent<JointBreaker>();
        jointBreaker?.BreakAll();
    }

}
