using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerAttackController : MonoBehaviour
{
    public List<Attack> Attacks { get; private set; } = new List<Attack>();

    private InputSystem_Actions controls;
    private bool attackHeld;

    private void Awake()
    {
        controls = new InputSystem_Actions();
        controls.Player.Attack.started += _ => attackHeld = true;
        controls.Player.Attack.canceled += _ => attackHeld = false;
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    public void InitializeAttacks(List<Attack> attacks)
    {
        Attacks = attacks;
    }


    // Update is called once per frame
    void Update()
    {
        if (attackHeld)
        {
            if (Attacks.Count > 0)
                Attacks[0].Execute();
        }
    }
}
