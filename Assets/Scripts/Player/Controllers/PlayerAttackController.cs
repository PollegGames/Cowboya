using System.Collections.Generic;
using UnityEngine;

public class PlayerAttackController : MonoBehaviour
{
    public List<Attack> Attacks { get; private set; } = new List<Attack>();

    [SerializeField] private PlayerMovementController movementController;
    private IRobotDecisionProvider decisionProvider;

    private void Awake()
    {
        if (movementController == null)
            movementController = GetComponent<PlayerMovementController>();

        if (movementController != null)
            decisionProvider = movementController;

        if (decisionProvider == null)
            decisionProvider = GetComponent<IRobotDecisionProvider>();

        if (decisionProvider == null)
            Debug.LogError("PlayerAttackController requires an IRobotDecisionProvider.");
    }

    public void InitializeAttacks(List<Attack> attacks)
    {
        Attacks = attacks ?? new List<Attack>();
    }

    private void Update()
    {
        if (decisionProvider == null || Attacks.Count == 0)
            return;

        if (decisionProvider.TryBuildAttackRequest(out AttackRequest request))
            Attacks[0].Execute(request);
    }
}
