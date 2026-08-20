using System.Collections.Generic;
using CowBoya.Robots;
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
    private bool isCrouchingInput;
    private const float DirectionDeadzone = 0.1f;

    [SerializeField] private EnergyBot energyBot;
    [SerializeField] private PlayerBrain playerBrain;
    [SerializeField] private Inventory inventory;

    [Header("Faint Handling")]
    [SerializeField] private SimplePuppetBinder puppetBinder;
    [SerializeField] private List<Rigidbody2D> faintRigidbodies2D = new List<Rigidbody2D>();
    [SerializeField] private List<Rigidbody> faintRigidbodies3D = new List<Rigidbody>();

    private Vector2 lookDirection = Vector2.right;
    private readonly Dictionary<Rigidbody2D, bool> defaultFreezeRotation2D = new Dictionary<Rigidbody2D, bool>();
    private readonly Dictionary<Rigidbody, RigidbodyConstraints> defaultConstraints3D = new Dictionary<Rigidbody, RigidbodyConstraints>();
    private bool isFaint;

    /// <summary>
    /// Gets the current look direction.
    /// </summary>
    public Vector2 LookDirection => lookDirection;

    private void Awake()
    {
        if (robotBehaviour == null)
            robotBehaviour = GetComponent<RobotStateController>();

        if (playerBrain == null)
            playerBrain = GetComponent<PlayerBrain>();

        robotBehaviour.OnStateChanged += HandleStateChange;

        if (inventory == null)
            inventory = GetComponent<Inventory>();

        if (puppetBinder == null)
            puppetBinder = GetComponent<SimplePuppetBinder>();

        input = inputSource as IPlayerInput;
        if (input == null)
        {
            Debug.LogError("PlayerMovementController: inputSource does not implement IPlayerInput");
        }
        // Ensure initial facing is applied to all modules (including PoleMirror2D)
        ApplyFacingDirection();
        CacheDefaultFreezeSettings();
    }

    private void Update()
    {
        if (robotBehaviour.CurrentState != RobotState.Alive) return;
        if (input != null)
        {
            horizontalInput = input.Movement.x;

            if (Mathf.Abs(horizontalInput) > DirectionDeadzone)
                lookDirection = new Vector2(Mathf.Sign(horizontalInput), 0f);
        }

        TryFlip();
        HandleCrouch();
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
    }

    private void HandleMovement()
    {
        locomotion.HandleMovement(isCrouchingInput ? 0f : horizontalInput, flipped);
    }

    private void HandleJump()
    {
        if (input != null && input.JumpDown && !isCrouchingInput
            && Mathf.Abs(horizontalInput) > DirectionDeadzone)
        {
            locomotion.Jump();
        }
    }

    private void HandleCrouch()
    {
        bool crouchHeld = input != null && input.CrouchHeld;
        if (crouchHeld && !isCrouchingInput)
        {
            locomotion.Crouch();
        }
        else if (!crouchHeld && isCrouchingInput)
        {
            locomotion.Uncrouch();
        }

        isCrouchingInput = crouchHeld;
    }

    /// <inheritdoc />
    public Vector2 Movement => new Vector2(isCrouchingInput ? 0f : horizontalInput, 0f);

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

        if (input == null || (!input.LeftAttackHeld && !input.RightAttackHeld) || robotBehaviour == null)
            return false;

        if (robotBehaviour.CurrentState != RobotState.Alive)
            return false;

        float energyRequired = 0f;
        if (robotBehaviour.Stats != null)
            energyRequired = robotBehaviour.Stats.AttackEnergyCost;

        if (playerBrain != null)
        {
            if (!playerBrain.TrySpendEnergy(EnergyAction.Attack, 0f, energyRequired))
                return false;
        }
        else
        {
            if (!robotBehaviour.PerformAttackByEnergy(energyRequired))
                return false;
        }

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
            Vector2 lookInput = input.Aim;
            if (!input.AimIsScreenPosition && lookInput.sqrMagnitude > 0.0001f)
                return lookInput;
        }

        if (lookDirection.sqrMagnitude > 0.0001f)
            return lookDirection;

        return Vector2.right;
    }

    private void HandleStateChange(RobotState newState)
    {
        switch (newState)
        {
            case RobotState.Alive:
                RecoverFromFaint();
                break;
            case RobotState.Faint:
                Faint();
                break;
            case RobotState.Dead:
                Die();
                break;
        }
    }

    public void Faint()
    {
        if (isFaint)
            return;

        isFaint = true;
        DisablePuppetBinder();
        SetFreezeRotationForFaint(false);
    }

    private void RecoverFromFaint()
    {
        if (!isFaint)
            return;

        isFaint = false;
        RestorePuppetBinder();
        SetFreezeRotationForFaint(true);
    }

    public void Die()
    {
        inventory?.DropAll();

        var jointBreaker = GetComponent<JointBreaker>();
        jointBreaker?.BreakAll();
    }

    private void CacheDefaultFreezeSettings()
    {
        foreach (Rigidbody2D body in faintRigidbodies2D)
        {
            if (body != null && !defaultFreezeRotation2D.ContainsKey(body))
                defaultFreezeRotation2D.Add(body, body.freezeRotation);
        }

        foreach (Rigidbody body in faintRigidbodies3D)
        {
            if (body != null && !defaultConstraints3D.ContainsKey(body))
                defaultConstraints3D.Add(body, body.constraints);
        }
    }

    private void SetFreezeRotationForFaint(bool useDefaultConstraints)
    {
        foreach (Rigidbody2D body in faintRigidbodies2D)
        {
            if (body == null)
                continue;

            if (!defaultFreezeRotation2D.ContainsKey(body))
                defaultFreezeRotation2D.Add(body, body.freezeRotation);

            body.freezeRotation = useDefaultConstraints ? defaultFreezeRotation2D[body] : false;
        }

        foreach (Rigidbody body in faintRigidbodies3D)
        {
            if (body == null)
                continue;

            if (!defaultConstraints3D.ContainsKey(body))
                defaultConstraints3D.Add(body, body.constraints);

            body.constraints = useDefaultConstraints
                ? defaultConstraints3D[body]
                : RemoveRotationConstraints(body.constraints);
        }
    }

    private RigidbodyConstraints RemoveRotationConstraints(RigidbodyConstraints constraints)
    {
        return constraints & ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ);
    }

    private void DisablePuppetBinder()
    {
        if (puppetBinder == null)
            puppetBinder = GetComponent<SimplePuppetBinder>();

        if (puppetBinder != null)
            puppetBinder.enabled = false;
    }

    private void RestorePuppetBinder()
    {
        if (puppetBinder == null)
            puppetBinder = GetComponent<SimplePuppetBinder>();

        if (puppetBinder != null && (robotBehaviour == null || robotBehaviour.CurrentState == RobotState.Alive))
            puppetBinder.enabled = true;
    }

    private void OnDestroy()
    {
        if (robotBehaviour != null)
            robotBehaviour.OnStateChanged -= HandleStateChange;
    }
}
