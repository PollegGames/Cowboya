using UnityEngine;

[DisallowMultipleComponent]
public class PlayerBrain : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RobotStateController stateController;
    [SerializeField] private EnergyBot energyBot;
    [SerializeField] private HealthBot healthBot;
    [SerializeField] private PlayerMovementController movementController;
    [SerializeField] private CowboyGrabController grabController;

    private void Awake()
    {
        if (stateController == null)
            stateController = GetComponent<RobotStateController>();
        if (energyBot == null)
            energyBot = GetComponent<EnergyBot>();
        if (healthBot == null)
            healthBot = GetComponent<HealthBot>();
        if (movementController == null)
            movementController = GetComponent<PlayerMovementController>();
        if (grabController == null)
            grabController = GetComponent<CowboyGrabController>();

        if (energyBot != null)
            energyBot.SetPlayerMode(true);
    }

    private void OnEnable()
    {
        if (energyBot != null)
            energyBot.OnEnergyChanged += HandleEnergyChanged;
        if (healthBot != null)
            healthBot.OnHealthChanged += HandleHealthChanged;
    }

    private void OnDisable()
    {
        if (energyBot != null)
            energyBot.OnEnergyChanged -= HandleEnergyChanged;
        if (healthBot != null)
            healthBot.OnHealthChanged -= HandleHealthChanged;
    }

    public bool TrySpendEnergy(EnergyAction action, float deltaTime = 0f, float? overrideCost = null)
    {
        if (energyBot == null)
            return true;

        return energyBot.TryConsume(action, deltaTime, overrideCost);
    }

    public void OnPlayerDamaged(int damage)
    {
        if (stateController == null || stateController.CurrentState == RobotState.Dead)
            return;

        if (stateController.Stats != null && stateController.Stats.CurrentHealth - damage <= 0f)
            stateController.UpdateState(RobotState.Dead);
    }

    private void HandleEnergyChanged(float currentEnergy, float maxEnergy)
    {
        if (stateController == null)
            return;

        float faintRecoveryThreshold = energyBot != null ? energyBot.FaintRecoveryThreshold : 0f;

        if (currentEnergy <= 0f)
        {
            stateController.UpdateState(RobotState.Faint);
            return;
        }

        if (stateController.CurrentState == RobotState.Faint)
        {
            if (faintRecoveryThreshold <= 0f || currentEnergy >= faintRecoveryThreshold)
                stateController.UpdateState(RobotState.Alive);
        }
    }

    private void HandleHealthChanged(float delta)
    {
        if (stateController == null || stateController.Stats == null)
            return;

        if (stateController.Stats.CurrentHealth <= 0f)
            stateController.UpdateState(RobotState.Dead);
    }
}
