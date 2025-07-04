using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CowBotController : MonoBehaviour, IController
{
    [Header("Animator")]
    public Animator animator;
    [Header("Input Settings")]
    public bool PlayerInput = true;

    [Header("Movement")]
    public float moveSpeed = 10f;
    public float acceleration = 2f;
    public float deceleration = 2f;
    private float currentSpeed = 0f;
    public float jumpForce = 200f;
    private string IsWalking = "IsWalking";
    private string IsJumping = "IsJumping";
    private string DirectionString = "Direction";
    private float Direction;

    [Header("Attack Settings")]
    [SerializeField] private AttackHitbox punchHitbox;
    public float attackCooldown = 1f;
    public float attackForce = 50f;

    private bool isAttacking = false;
    private int currentAttackIndex = 0;
    private List<AttackType> attackOrder = new List<AttackType> { AttackType.Punch };
    private string IsAttacking = "IsAttacking";

    [Header("Dependencies")]
    [SerializeField] private RobotBehaviour robotBehaviour;

    [Header("Flip Settings")]
    [SerializeField] private Transform points;
    [SerializeField] private Transform poles;
    private bool flipped = false;

    private void Awake()
    {
        if (robotBehaviour == null)
        {
            robotBehaviour = GetComponent<RobotBehaviour>();
        }

        robotBehaviour.OnStateChanged += HandleStateChange;

    }
    private void Update()
    {
        if (!PlayerInput || robotBehaviour.CurrentState != RobotState.Alive) return;

        // Handle input
        if (Input.GetKeyDown(KeyCode.Space)) Jump();
        if (Input.GetKeyDown(KeyCode.F)) Attack();

        float horizontalInput = Input.GetAxis("Horizontal");
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            Move(horizontalInput);
            TryFlip(horizontalInput);
            animator.SetBool(IsWalking, true);
        }
        else
        {
            animator.SetBool(IsWalking, false);
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.deltaTime);
        }
    }
    public void Move(float direction)
    {
        if (!PlayerInput) return;
        currentSpeed = Mathf.MoveTowards(currentSpeed, moveSpeed, acceleration * Time.deltaTime);

        // Handle movement
        transform.Translate(Vector2.right * direction * currentSpeed * Time.deltaTime);
        SetDirection(direction);
    }

    private void SetDirection(float direction)
    {
        Direction = direction;
        animator.SetFloat(DirectionString, Direction);
    }

    public void Jump()
    {
        if (!PlayerInput || !robotBehaviour.CanJump()) return;
        {
            robotBehaviour.HandleJump(jumpForce);
            animator.SetBool(IsJumping, true);
            StartCoroutine(ResetJumpAnimation());
        }
    }
    private IEnumerator ResetJumpAnimation()
    {
        yield return new WaitForSeconds(0.5f); // Adjust duration based on jump animation length
        animator.SetBool(IsJumping, false);
    }

    public void Attack()
    {
        if (!PlayerInput || isAttacking || !robotBehaviour.CanPerformAttack()) return;
        {
            StartCoroutine(PerformAttack());
        }
    }

    private IEnumerator PerformAttack()
    {
        isAttacking = true;
        animator.SetBool(IsAttacking, true);

        AttackType currentAttack = attackOrder[currentAttackIndex];
        currentAttackIndex = (currentAttackIndex + 1) % attackOrder.Count;

        Debug.Log($"Performing {currentAttack} attack");

        robotBehaviour.PerformAttack(currentAttack);
        // 👉 Active hitbox si attaque = punch
        if (currentAttack == AttackType.Punch)
        {
            punchHitbox?.Activate();
            yield return new WaitForSeconds(0.2f); // durée du coup
            punchHitbox?.Deactivate();
        }


        yield return new WaitForSeconds(attackCooldown);

        isAttacking = false;
    }

    private void HandleStateChange(RobotState newState)
    {
        if (newState == RobotState.Faint || newState == RobotState.Dead)
        {
            animator.SetBool("IsFaint", newState == RobotState.Faint);
            Faint();
        }
        else if (newState == RobotState.Dead)
        {
            animator.SetBool("IsDead", newState == RobotState.Dead);
        }
        else if (newState == RobotState.Alive)
        {
            UpdateBalance(true);
            animator.SetBool("IsFaint", false);
            animator.SetBool("IsDead", false);
        }
    }

    public void Faint()
    {
        // Disable BodyBalance script
        UpdateBalance(false);
    }

    private void UpdateBalance(bool enabledBalance)
    {
        // Re-enable BodyBalance script
        var bodyBalance = GetComponent<BodyBalance>();
        if (bodyBalance != null)
        {
            bodyBalance.UpdateBalance(enabledBalance);
        }
    }

    public void OnPunchEnd()
    {
        animator.SetBool(IsAttacking, false);
    }

    private void TryFlip(float direction)
    {
        if ((direction > 0 && flipped) || (direction < 0 && !flipped))
        {
            flipped = !flipped;

            // Adjust points and poles if additional flipping is necessary
            if (points != null)
            {
                Vector3 pointsScale = points.localScale;
                pointsScale.x *= -1;
                points.localScale = pointsScale;
            }

            if (poles != null)
            {
                Vector3 polesScale = poles.localScale;
                polesScale.x *= -1;
                poles.localScale = polesScale;
            }
        }
    }
}