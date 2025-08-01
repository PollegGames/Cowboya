using UnityEngine;

[RequireComponent(typeof(RobotLocomotionController), typeof(FacingController))]
public class PlayerMovementController : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private RobotLocomotionController locomotion;
    [SerializeField] private FacingController facing;
    [SerializeField] private BodyBalance bodyBalance;
    [SerializeField] private float forceUpward = 5f;
    [SerializeField] private float forceSide = 5f;
    [SerializeField] private LegJointLimiter legJointLimiter;
    [SerializeField] private BodyJointLimiter bodyJointLimiter;
    [SerializeField] private RobotStateController robotBehaviour;

    [Header("Body Rotation")]
    [SerializeField] private Rigidbody2D bodyReference;
    public Rigidbody2D BodyReference => bodyReference;
    [SerializeField] private float maximumLerp = 10f;

    [SerializeField] private MonoBehaviour inputSource;
    private IPlayerInput input;

    private bool flipped = false;
    private float horizontalInput;
    private float verticalInput;
    private float targetRotation;

    [SerializeField] private EnergyBot energyBot;

    private void Awake()
    {
        locomotion.OnJumpStarted += HandleJumpStart;
        locomotion.OnJumpEnded += HandleJumpEnd;

        if (robotBehaviour == null)
            robotBehaviour = GetComponent<RobotStateController>();

        robotBehaviour.OnStateChanged += HandleStateChange;

        input = inputSource as IPlayerInput;
        if (input == null)
        {
            Debug.LogError("PlayerMovementController: inputSource does not implement IPlayerInput");
        }
    }

    private void Update()
    {
        if (robotBehaviour.CurrentState != RobotState.Alive) return;
        if (input != null)
        {
            horizontalInput = input.Movement.x;
            verticalInput = input.Movement.y;
        }
        TryFlip();
        CalculateAndApplyBodyRotation();
        HandleMovement();
        HandleJump();
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
        facing.SetLegFacing(!flipped);
        legJointLimiter.SetLegRotationLimits(flipped); // true = going left
        if (bodyJointLimiter != null)
            bodyJointLimiter.SetBodyRotationLimits(flipped);
    }

    private void CalculateAndApplyBodyRotation()
    {
        targetRotation = Mathf.Approximately(horizontalInput, 0f)
            ? 0f
            : Mathf.Lerp(maximumLerp, -maximumLerp, (horizontalInput + 1f) / 2f);

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
        locomotion.HandleMovement(horizontalInput);
    }

    private void HandleJump()
    {
        if (verticalInput > 0)
        {
            locomotion.Jump();
        }
    }

    private void HandleJumpStart()
    {
        Debug.Log("Jump start");
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
        // Drop any badge the player is carrying
        SecurityBadgePickup.DropPlayerBadge();

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
