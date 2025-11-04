using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Coordinates combat-related dependencies for a robot including stats,
/// available attacks and animator overrides.
/// </summary>
[DisallowMultipleComponent]
public class RobotCombatController : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private RobotStateController robotStateController;
    [SerializeField] private AttackRequestController attackRequestController;
    [SerializeField] private Animator animator;

    [Header("Defaults")]
    [SerializeField] private RuntimeAnimatorController defaultAnimatorController;
    [SerializeField] private List<Attack> defaultAttacks = new();

    /// <summary>
    /// Provides access to the <see cref="RobotStateController"/> used by this robot.
    /// </summary>
    public RobotStateController StateController => robotStateController;

    /// <summary>
    /// Provides access to the <see cref="AttackRequestController"/> used by this robot.
    /// </summary>
    public AttackRequestController AttackController => attackRequestController;

    /// <summary>
    /// Attempts to execute the provided <paramref name="request"/> using the
    /// configured <see cref="AttackRequestController"/>.
    /// </summary>
    /// <param name="request">Requested attack details.</param>
    /// <returns>True when the request was accepted.</returns>
    public bool TryHandleAttack(AttackRequest request)
    {
        if (attackRequestController == null)
        {
            return false;
        }

        return attackRequestController.TryHandleAttack(request);
    }

    /// <summary>
    /// Executes the provided <paramref name="request"/> disregarding the
    /// success flag from <see cref="TryHandleAttack(AttackRequest)"/>.
    /// </summary>
    /// <param name="request">Requested attack details.</param>
    public void HandleAttackRequest(AttackRequest request)
    {
        attackRequestController?.HandleAttackRequest(request);
    }

    private void Awake()
    {
        if (robotStateController == null)
        {
            robotStateController = GetComponent<RobotStateController>();
        }

        if (attackRequestController == null)
        {
            attackRequestController = GetComponent<AttackRequestController>();
            if (attackRequestController == null)
            {
                attackRequestController = GetComponentInChildren<AttackRequestController>();
            }
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        if (animator != null && defaultAnimatorController != null)
        {
            animator.runtimeAnimatorController = defaultAnimatorController;
        }
    }

    /// <summary>
    /// Assigns stats, attacks and animator overrides to the robot.
    /// </summary>
    /// <param name="stats">Stats to assign to the <see cref="RobotStateController"/>.</param>
    /// <param name="attacks">Optional attack list to override the defaults.</param>
    /// <param name="animatorController">Optional animator override.</param>
    public void Configure(RobotStats stats, IEnumerable<Attack> attacks = null, RuntimeAnimatorController animatorController = null)
    {
        if (robotStateController != null && stats != null)
        {
            robotStateController.Stats = stats;
        }

        List<Attack> resolvedAttacks = BuildAttackList(attacks);
        if (robotStateController != null && robotStateController.Stats != null)
        {
            robotStateController.Stats.Attacks = resolvedAttacks;
        }

        PlayerAttackController playerAttackController = GetComponent<PlayerAttackController>();
        if (playerAttackController != null)
        {
            playerAttackController.InitializeAttacks(resolvedAttacks);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null && animatorController != null)
        {
            animator.runtimeAnimatorController = animatorController;
        }
    }

    /// <summary>
    /// Clears the active attack if possible.
    /// </summary>
    public void AbortActiveAttack()
    {
        attackRequestController?.AbortActiveAttack();
    }

    private List<Attack> BuildAttackList(IEnumerable<Attack> overrides)
    {
        List<Attack> attacks = new();

        if (overrides != null)
        {
            foreach (Attack attack in overrides)
            {
                if (attack != null)
                {
                    attacks.Add(attack);
                }
            }
        }

        if (attacks.Count == 0 && defaultAttacks != null)
        {
            foreach (Attack attack in defaultAttacks)
            {
                if (attack != null)
                {
                    attacks.Add(attack);
                }
            }
        }

        return attacks;
    }
}
