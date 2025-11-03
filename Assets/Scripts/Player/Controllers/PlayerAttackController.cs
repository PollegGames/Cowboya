using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttackController : MonoBehaviour
{
    public List<Attack> Attacks { get; private set; } = new List<Attack>();

    [SerializeField] private PlayerMovementController movementController;
    [SerializeField] private RobotStateController robotStateController;

    private InputSystem_Actions controls;
    private bool attackHeld;
    private AttackSector currentSector = AttackSector.Right;

    private void Awake()
    {
        controls = new InputSystem_Actions();
        controls.Player.Attack.started += _ => attackHeld = true;
        controls.Player.Attack.canceled += _ => attackHeld = false;

        if (movementController == null)
            movementController = GetComponent<PlayerMovementController>();

        if (movementController != null)
        {
            currentSector = movementController.CurrentSector;
            movementController.SectorChanged += HandleSectorChanged;
        }

        if (robotStateController == null)
            robotStateController = GetComponent<RobotStateController>();
    }

    private void OnDestroy()
    {
        if (movementController != null)
            movementController.SectorChanged -= HandleSectorChanged;

        controls?.Dispose();
    }

    private void HandleSectorChanged(AttackSector sector)
    {
        currentSector = sector;
    }

    private void OnEnable() => controls.Enable();

    private void OnDisable()
    {
        controls.Disable();
        attackHeld = false;
    }

    public void InitializeAttacks(List<Attack> attacks)
    {
        Attacks = attacks ?? new List<Attack>();
    }

    private void Update()
    {
        if (!attackHeld || Attacks.Count == 0)
            return;

        AttackRequest request = BuildRequest();
        Attacks[0].Execute(request);
    }

    private AttackRequest BuildRequest()
    {
        Vector2 targetPosition = DetermineTargetPosition();
        float energyRequired = 0f;

        if (robotStateController != null && robotStateController.Stats != null)
            energyRequired = robotStateController.Stats.AttackEnergyCost;

        return new AttackRequest(targetPosition, currentSector, energyRequired);
    }

    private Vector2 DetermineTargetPosition()
    {
        if (movementController != null)
        {
            Vector2 aim = movementController.AimVector;
            if (aim.sqrMagnitude <= 0.0001f)
                aim = movementController.LookDirection;

            if (aim.sqrMagnitude > 0.0001f)
                return (Vector2)transform.position + aim.normalized;
        }

        return transform.position;
    }
}
