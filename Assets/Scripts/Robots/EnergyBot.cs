using System;
using System.Collections;
using UnityEngine;

public class EnergyBot : MonoBehaviour
{
    public float rechargeRate = 2f;
    public float tickDelay = 1f;
    private float totalNeeded = 0f;    // total amount we need to recharge
    private Coroutine rechargeCoroutine;

     public event Action<float> OnEnergyNeeded;

    public void RechargingEnergy(float energyCost)
    {
        OnEnergyNeeded?.Invoke(energyCost);

        float amountToRecharge = Mathf.Abs(energyCost);
        totalNeeded += amountToRecharge;

         // Start the coroutine if it's not already running
        if (rechargeCoroutine == null)
            rechargeCoroutine = StartCoroutine(RechargeCoroutine());
    }
    private IEnumerator RechargeCoroutine()
    {
        // While there's something to recharge
        while (totalNeeded > 0f)
        {
            float amountThisTick = Mathf.Min(rechargeRate, totalNeeded);
            OnEnergyNeeded?.Invoke(amountThisTick);

            totalNeeded -= amountThisTick;
            yield return new WaitForSeconds(tickDelay);
        }

        rechargeCoroutine = null;
    }
}
