using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BodyBalance : MonoBehaviour
{
    public BodyPart[] muscles;
    private bool balanceEnabled = true;

    private void Update()
    {
        if (!balanceEnabled) return;

        foreach (BodyPart muscle in muscles)
        {
            muscle.ActivateBalance();
        }
    }

    public void UpdateBalance(bool enabledBalance)
    {
        balanceEnabled = enabledBalance;
    }

    public void UpdateMuscleForce(float newForce)
    {
        foreach (BodyPart muscle in muscles)
        {
            muscle.force = newForce;
        }
    }
}

[System.Serializable]
public class BodyPart
{
    public Rigidbody2D bone;
    public float restRotation;
    public float force;

    public void ActivateBalance()
    {
        bone.MoveRotation(Mathf.LerpAngle(bone.rotation, restRotation, force * Time.deltaTime));
    }
}
