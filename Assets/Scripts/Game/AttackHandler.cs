using System.Collections.Generic;
using UnityEngine;

public class AttackHandler : MonoBehaviour
{
    public List<Attack> Attacks { get; private set; } = new List<Attack>();

    public void InitializeAttacks(List<Attack> attacks)
    {
        Attacks = attacks;
    }


    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButton(0))
        {
            if (Attacks.Count > 0)
                Attacks[0].Execute();
        }
    }
}
