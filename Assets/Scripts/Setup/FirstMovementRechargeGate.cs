using UnityEngine;

public class FirstMovementRechargeGate : MonoBehaviour
{
    private EnergyBot energyBot;
    private IPlayerInput input;
    private bool released;

    public void Configure(EnergyBot energyBot, IPlayerInput input)
    {
        this.energyBot = energyBot;
        this.input = input;
    }

    private void Update()
    {
        if (released || energyBot == null || input == null)
            return;

        if (input.Movement.sqrMagnitude <= 0.01f)
            return;

        released = true;
        energyBot.SetAutoRecharge(true);
        Destroy(this);
    }
}
